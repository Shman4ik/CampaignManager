// Плеер фонотеки Хранителя.
//
// Три вещи определяют устройство этого модуля, и все три легко сломать правкой «на вид безобидной»:
//
// 1. ВЕСЬ звук живёт в синглтоне на window, а <audio> и контейнер YouTube — в
//    <div id="cm-music-host" data-permanent> из App.razor, не в дереве Blazor. Пауза circuit
//    (вкладка скрыта 30 с, js/circuit-persistence.js) при возобновлении пересобирает страницу
//    заново; всё, что рендерил компонент, пересоздалось бы вместе с ней, и музыка оборвалась бы
//    посреди сцены. Страница при resume не перезагружается, поэтому синглтон и элементы паузу
//    переживают. Не переносить элементы внутрь .razor и не вешать обратно на <body> —
//    почему именно в этот контейнер, написано над playerHost().
//
// 2. Громкость — только через Web Audio GainNode. iOS игнорирует HTMLMediaElement.volume
//    (значение выставляется, звук остаётся прежним), а целевое устройство — iPad Pro.
//
// 3. iframe нельзя переносить по DOM: любой reparent перезагружает плеер и обрывает трек.
//    Поэтому контейнер YouTube зафиксирован на body, а «спрятан» он уводом за экран, а не
//    display:none (он бы остановил воспроизведение).

const SINGLETON_KEY = '__cmMusicPlayer';
const FADE_SECONDS = 1.2;
const GESTURE_PROBE_MS = 2000;
const TICK_MS = 250;

// Идентификаторы элементов, которыми модуль управляет напрямую. Blazor рисует их один раз
// с неизменной разметкой и больше не трогает, поэтому значения, выставленные отсюда, переживают
// перерисовки панели.
const SEEK_ID = 'cm-music-seek';
const ELAPSED_ID = 'cm-music-elapsed';
const DURATION_ID = 'cm-music-duration';
const UNLOCK_ID = 'cm-music-unlock';
const START_TRIGGER_SELECTOR = '.cm-music-start';

// Постоянный дом для <audio> и окошка YouTube — <div id="cm-music-host" data-permanent>
// в App.razor.
const HOST_ID = 'cm-music-host';

// Пустой WAV: им «расчехляем» аудио-элемент внутри настоящего жеста пользователя, иначе
// iOS не даст воспроизвести первый трек по команде из Blazor (round-trip съедает активацию).
const SILENT_WAV = 'data:audio/wav;base64,UklGRiQAAABXQVZFZm10IBAAAAABAAEAgD4AAAB9AAACABAAZGF0YQAAAAA=';

function getState() {
    let state = window[SINGLETON_KEY];
    if (state) return state;

    state = {
        audio: null,
        ctx: null,
        gain: null,
        source: null,
        ytHost: null,
        ytPlayer: null,
        ytReady: null,
        ytApi: null,
        dotNet: null,
        track: null,
        master: 0.6,
        muted: false,
        unlocked: false,
        gestureTimer: null,
        ticker: null,
        scrubbing: false,
        barObserver: null,
        observedBar: null,
        barHeight: 0
    };
    window[SINGLETON_KEY] = state;
    return state;
}

// ───────────────────────── Аудио-элемент и граф Web Audio ─────────────────────────

// Элементы плеера кладутся сюда, а НЕ на <body> напрямую. Enhanced-навигация Blazor на каждом
// переходе сливает <body> с разметкой серверного ответа и всё, чего в ответе нет, из него
// удаляет — добавленное из JS срезалось на первой же смене страницы. Удаление <audio> из
// документа браузер по спецификации доводит внутренними шагами паузы, то есть музыка просто
// замолкала; iframe YouTube от удаления умирал насовсем, а state.ytPlayer продолжал ссылаться
// на труп, поэтому кнопки панели больше ничего не делали и плеер оживал только через F5.
// Спасает единственное: контейнер, который есть в серверной разметке (App.razor) и помечен
// data-permanent — дочерние узлы такого элемента сравнение DOM не трогает вообще. Атрибуты же
// оно синхронизирует и у него, поэтому состояние (src трека, класс «спрятан») живёт на
// вложенных элементах, а на самом контейнере не должно появляться ничего меняющегося.
function playerHost() {
    // Запасной вариант на случай, если разметка контейнера почему-то не дошла: на текущей
    // странице звук будет, а на переходе оборвётся — это лучше, чем совсем без звука.
    return document.getElementById(HOST_ID) ?? document.body;
}

function ensureAudio(state) {
    if (state.audio) {
        // Элемент могли вынести из документа — браузер такой ставит на паузу. Возвращаем
        // на место, иначе панель осталась бы с кнопками, которые ничего не включают.
        if (!state.audio.isConnected) playerHost().appendChild(state.audio);
        return state.audio;
    }

    const audio = document.createElement('audio');
    audio.id = 'cm-music-audio';
    audio.preload = 'none';
    // Не хочется, чтобы iOS показывал наш эмбиент в «Сейчас играет» как видео.
    audio.setAttribute('playsinline', '');
    playerHost().appendChild(audio);

    audio.addEventListener('ended', () => {
        // Пустой WAV из installUnlock «заканчивается» сразу же, и его конец сервер принимал
        // за конец трека: HandleTrackEndedAsync снимал IsPlaying, панель показывала «играет
        // на паузе», хотя музыка шла. Ловилось это на первом же касании страницы — том самом,
        // которым Хранитель и включает трек. Отсюда и две проверки: играть должен именно файл
        // из хранилища, и закончиться должен он, а не заглушка.
        if (state.track?.sourceType !== 'Storage') return;
        if (audio.currentSrc === SILENT_WAV) return;

        // Зацикленный трек сюда не приходит — его крутит сам браузер.
        notify('NotifyEnded');
    });
    audio.addEventListener('error', () => {
        if (state.track && state.track.sourceType === 'Storage') {
            notify('NotifyError', 'Не удалось загрузить файл трека.');
        }
    });

    state.audio = audio;
    return audio;
}

function ensureGraph(state) {
    if (state.ctx) return state.ctx;

    const Ctx = window.AudioContext || window.webkitAudioContext;
    if (!Ctx) return null;

    const ctx = new Ctx();
    const gain = ctx.createGain();
    gain.gain.value = 0.0001;
    gain.connect(ctx.destination);

    // createMediaElementSource можно звать по одному разу на элемент — отсюда и синглтон.
    const source = ctx.createMediaElementSource(ensureAudio(state));
    source.connect(gain);

    state.ctx = ctx;
    state.gain = gain;
    state.source = source;
    return ctx;
}

function effectiveGain(state) {
    if (state.muted) return 0;
    const trackVolume = state.track && typeof state.track.volume === 'number'
        ? Math.min(Math.max(state.track.volume, 0), 100) / 100
        : 1;
    return state.master * trackVolume;
}

function fadeTo(state, target, seconds) {
    if (!state.ctx || !state.gain) return;
    const param = state.gain.gain;
    const now = state.ctx.currentTime;
    const floor = 0.0001;
    param.cancelScheduledValues(now);
    param.setValueAtTime(Math.max(param.value, floor), now);
    param.linearRampToValueAtTime(Math.max(target, floor), now + seconds);
}

function wait(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

// ───────────────────────── Снятие блокировки iOS ─────────────────────────

// Вешается один раз и срабатывает только на кнопке, которая действительно запускает музыку.
// Нельзя «расчехлять» звук на первом произвольном касании страницы: на iPad даже пустой WAV
// захватывает системную аудиосессию и останавливает YouTube/музыку из другого приложения,
// хотя пользователь ещё не просил сайт ничего воспроизводить. Внутри настоящего жеста
// AudioContext переводится в running, а аудио-элемент помечается «пользователь разрешил» —
// после этого play() из Blazor проходит, хотя сам по себе жестом уже не считается.
function installUnlock(state) {
    if (state.unlocked || state.unlockInstalled) return;
    state.unlockInstalled = true;

    const removeListeners = () => {
        document.removeEventListener('pointerdown', unlock, true);
        document.removeEventListener('touchend', unlock, true);
        document.removeEventListener('keydown', unlock, true);
    };

    const unlock = event => {
        if (event.type === 'keydown' && event.key !== 'Enter' && event.key !== ' ') return;

        const target = event.target;
        if (!target || !target.closest || !target.closest(START_TRIGGER_SELECTOR)) return;

        state.unlocked = true;
        removeListeners();
        const ctx = ensureGraph(state);
        if (ctx && ctx.state === 'suspended') ctx.resume().catch(() => { });

        const audio = ensureAudio(state);
        if (!audio.src) {
            audio.src = SILENT_WAV;
            const played = audio.play();
            if (played && typeof played.then === 'function') {
                played.then(() => audio.pause()).catch(() => { });
            }
        }
    };

    document.addEventListener('pointerdown', unlock, true);
    // Старые версии iOS не посылают Pointer Events.
    document.addEventListener('touchend', unlock, true);
    document.addEventListener('keydown', unlock, true);
}

// ───────────────────────── YouTube ─────────────────────────

function ensureYouTubeHost(state) {
    if (state.ytHost) return state.ytHost;

    const host = document.createElement('div');
    host.id = 'cm-music-yt-host';
    host.className = 'cm-yt-host cm-yt-host--tucked';
    const mount = document.createElement('div');
    mount.id = 'cm-music-yt-mount';
    host.appendChild(mount);
    playerHost().appendChild(host);

    state.ytHost = host;
    return host;
}

// Окошко вынесли из документа — значит, iframe внутри уже мёртв: вернуть его на место нельзя,
// любой reparent iframe перезагружает, а объект YT.Player после этого отвечает в пустоту.
// Поэтому сбрасываем всё и даём следующему load() собрать плеер заново: лучше один оборванный
// трек, чем панель, которая до перезагрузки страницы молча не реагирует на кнопки.
function dropDetachedYouTubePlayer(state) {
    if (!state.ytHost || state.ytHost.isConnected) return;

    try {
        state.ytPlayer?.destroy?.();
    } catch {
        // iframe уже недоступен — жаловаться некому.
    }

    state.ytHost.remove();
    state.ytHost = null;
    state.ytPlayer = null;
    state.ytReady = null;
}

function loadYouTubeApi(state) {
    if (state.ytApi) return state.ytApi;

    state.ytApi = new Promise((resolve, reject) => {
        if (window.YT && window.YT.Player) {
            resolve(window.YT);
            return;
        }

        const previous = window.onYouTubeIframeAPIReady;
        window.onYouTubeIframeAPIReady = () => {
            if (typeof previous === 'function') previous();
            resolve(window.YT);
        };

        const script = document.createElement('script');
        script.src = 'https://www.youtube.com/iframe_api';
        script.async = true;
        script.onerror = () => reject(new Error('Не удалось загрузить плеер YouTube.'));
        document.head.appendChild(script);
    });

    return state.ytApi;
}

async function ensureYouTubePlayer(state) {
    dropDetachedYouTubePlayer(state);

    if (state.ytPlayer) {
        await state.ytReady;
        return state.ytPlayer;
    }

    ensureYouTubeHost(state);
    const YT = await loadYouTubeApi(state);

    state.ytReady = new Promise(resolve => {
        state.ytPlayer = new YT.Player('cm-music-yt-mount', {
            width: '160',
            height: '90',
            playerVars: {
                playsinline: 1,
                rel: 0,
                modestbranding: 1
            },
            events: {
                onReady: () => resolve(state.ytPlayer),
                onStateChange: event => {
                    if (event.data === YT.PlayerState.PLAYING) {
                        clearGestureProbe(state);
                        setYouTubeTucked(state, true);
                    }
                    if (event.data === YT.PlayerState.ENDED) notify('NotifyEnded');
                },
                onError: () => {
                    clearGestureProbe(state);
                    setYouTubeTucked(state, false);
                    notify('NotifyError',
                        'Этот ролик нельзя воспроизвести встроенным плеером — владелец запретил встраивание. Выберите другой трек или сохраните файл.');
                }
            }
        });
    });

    await state.ytReady;
    return state.ytPlayer;
}

function setYouTubeTucked(state, tucked) {
    if (!state.ytHost) return;
    state.ytHost.classList.toggle('cm-yt-host--tucked', tucked);
}

// iOS может отказать в программном playVideo(), пока пользователь ни разу не тронул сам плеер.
// Если через две секунды воспроизведение не началось — показываем окошко и просим коснуться его.
function startGestureProbe(state) {
    clearGestureProbe(state);
    state.gestureTimer = setTimeout(() => {
        state.gestureTimer = null;
        const player = state.ytPlayer;
        if (!player || typeof player.getPlayerState !== 'function') return;

        const playing = player.getPlayerState() === 1 || player.getPlayerState() === 3;
        if (playing) return;

        setYouTubeTucked(state, false);
        notify('NotifyNeedsGesture');
    }, GESTURE_PROBE_MS);
}

function clearGestureProbe(state) {
    if (state.gestureTimer) {
        clearTimeout(state.gestureTimer);
        state.gestureTimer = null;
    }
}

// ───────────────────── Позиция, перемотка и снятие блокировки ─────────────────────

// Полоса позиции обновляется отсюда, а не из Blazor: четыре перерисовки в секунду через
// SignalR — это постоянный трафик circuit и рывки на планшете. Blazor рисует элементы один раз
// с неизменной разметкой и больше их не патчит, поэтому значения, выставленные здесь, живут.

function currentTimes(state) {
    if (!state.track) return null;

    if (state.track.sourceType === 'YouTube') {
        const player = state.ytPlayer;
        if (!player || typeof player.getDuration !== 'function') return null;
        return { position: player.getCurrentTime() || 0, duration: player.getDuration() || 0 };
    }

    const audio = state.audio;
    if (!audio) return null;
    // У потока и у ещё не разобранного файла длительность — NaN или Infinity.
    return {
        position: audio.currentTime || 0,
        duration: Number.isFinite(audio.duration) ? audio.duration : 0
    };
}

function formatTime(seconds) {
    const total = Math.max(0, Math.floor(seconds));
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const rest = total % 60;
    const pad = value => String(value).padStart(2, '0');
    return hours > 0 ? `${hours}:${pad(minutes)}:${pad(rest)}` : `${minutes}:${pad(rest)}`;
}

function renderProgress(state) {
    const slider = document.getElementById(SEEK_ID);
    if (!slider) return;

    const elapsed = document.getElementById(ELAPSED_ID);
    const duration = document.getElementById(DURATION_ID);
    const times = currentTimes(state);

    // Длительности нет — это прямой эфир либо файл ещё не разобран. Перематывать нечего.
    if (!times || times.duration <= 0) {
        slider.disabled = true;
        if (elapsed) elapsed.textContent = formatTime(times ? times.position : 0);
        if (duration) duration.textContent = '--:--';
        return;
    }

    slider.disabled = false;
    if (duration) duration.textContent = formatTime(times.duration);

    // Пока ползунок тянут пальцем, позицию не перебиваем — иначе он вырывается из-под руки.
    if (state.scrubbing) return;

    slider.value = String(Math.round((times.position / times.duration) * 1000));
    if (elapsed) elapsed.textContent = formatTime(times.position);
}

function startTicker(state) {
    if (state.ticker) return;
    state.ticker = setInterval(() => renderProgress(state), TICK_MS);
}

function stopTicker(state) {
    if (!state.ticker) return;
    clearInterval(state.ticker);
    state.ticker = null;
}

function seekToFraction(state, fraction) {
    const times = currentTimes(state);
    if (!times || times.duration <= 0) return;

    // Полсекунды от конца: точное попадание в конец тут же роняет трек в «закончился».
    const target = Math.max(0, Math.min(times.duration - 0.5, fraction * times.duration));

    if (state.track.sourceType === 'YouTube') {
        if (state.ytPlayer && typeof state.ytPlayer.seekTo === 'function') state.ytPlayer.seekTo(target, true);
        return;
    }

    try {
        state.audio.currentTime = target;
    } catch {
        // Источник не перематывается — ползунок вернётся на место следующим тиком.
    }
}

// Высота панели плавает: пустая — одна строка, с полосой позиции — две, с раскрытым рядом
// настроений и предупреждением — четыре. Фиксированной величиной в CSS это не покрыть, поэтому
// меряем настоящую высоту и отдаём её стилям переменной. Переменная ставится на <html>, а не на
// <body>: enhanced-навигация Blazor сливает body с серверным ответом и правки на нём срезает.
function applyBarHeight(state, height) {
    const rounded = Math.round(height);
    if (rounded <= 0) return;
    state.barHeight = rounded;
    // Значение переставляется каждый раз, без памяти о прежнем: enhanced-навигация Blazor
    // сливает разметку с серверным ответом и инлайновый стиль срезает, а с проверкой
    // «высота не менялась» восстановить его было бы уже некому. Повторная запись того же
    // значения браузеру ничего не стоит.
    document.documentElement.style.setProperty('--cm-music-bar-height', rounded + 'px');
}

function ensureBarObserved(state) {
    const bar = document.querySelector('.cm-music-bar');

    if (!bar) {
        // Панель скрылась — отдаём отступ обратно объявленному в таблице значению.
        if (state.observedBar) {
            state.observedBar = null;
            state.barHeight = 0;
            document.documentElement.style.removeProperty('--cm-music-bar-height');
        }
        return;
    }

    if (state.observedBar === bar) {
        // Элемент тот же — но переменную могло срезать навигацией, поэтому переставляем.
        applyBarHeight(state, bar.getBoundingClientRect().height);
        return;
    }

    // Blazor пересоздал элемент — старое наблюдение умерло вместе с ним.
    state.observedBar = bar;
    if (!state.barObserver) {
        state.barObserver = new ResizeObserver(entries => {
            for (const entry of entries) {
                // Именно borderBoxSize: contentRect не считает padding и рамку панели,
                // и отступ выходил ровно на эти пиксели меньше нужного.
                const box = entry.borderBoxSize && entry.borderBoxSize[0];
                applyBarHeight(state, box ? box.blockSize : entry.target.getBoundingClientRect().height);
            }
        });
    }
    state.barObserver.disconnect();
    state.barObserver.observe(bar);
    applyBarHeight(state, bar.getBoundingClientRect().height);
}

// Всё синхронно и без await: активация жеста живёт только в синхронной части обработчика,
// первый же await её теряет — а весь смысл кнопки в том, чтобы запустить звук внутри жеста.
function unlockPlayback(state) {
    state.unlocked = true;

    const ctx = ensureGraph(state);
    if (ctx && ctx.state === 'suspended') ctx.resume().catch(() => { });

    if (state.track && state.track.sourceType === 'YouTube') {
        if (state.ytPlayer && typeof state.ytPlayer.playVideo === 'function') {
            state.ytPlayer.playVideo();
            startGestureProbe(state);
        }
    } else if (state.audio) {
        const played = state.audio.play();
        if (played && typeof played.catch === 'function') played.catch(() => { });
    }

    notify('NotifyGestureResolved');
}

// Слушатели делегированные и ставятся один раз: панель перерисовывается и пересобирается
// при каждой паузе circuit, а привязка к конкретным элементам это бы не пережила.
function installControls(state) {
    if (state.controlsInstalled) return;
    state.controlsInstalled = true;

    // Раз в секунду хватает: запрос одного селектора, зато высота отступа не разъезжается
    // даже когда музыка не играет, а Хранитель раскрыл ряд настроений.
    ensureBarObserved(state);
    setInterval(() => ensureBarObserved(state), 1000);

    document.addEventListener('input', event => {
        if (!event.target || event.target.id !== SEEK_ID) return;
        state.scrubbing = true;

        const times = currentTimes(state);
        const elapsed = document.getElementById(ELAPSED_ID);
        if (times && times.duration > 0 && elapsed) {
            elapsed.textContent = formatTime((event.target.value / 1000) * times.duration);
        }
    });

    document.addEventListener('change', event => {
        if (!event.target || event.target.id !== SEEK_ID) return;
        state.scrubbing = false;
        seekToFraction(state, event.target.value / 1000);
    });

    // Кнопка «Включить звук» обрабатывается здесь, а не через @onclick: нажатие в Blazor Server
    // уходит на сервер и возвращается, активация жеста к тому моменту истекает — и playVideo()
    // снова упирается в тот же запрет, из-за которого кнопка и понадобилась.
    document.addEventListener('click', event => {
        if (!event.target || !event.target.closest) return;
        if (!event.target.closest('#' + UNLOCK_ID)) return;
        unlockPlayback(state);
    }, true);
}

// ───────────────────────── Обратная связь в Blazor ─────────────────────────

function notify(method, arg) {
    const state = getState();
    if (!state.dotNet) return;
    const call = arg === undefined
        ? state.dotNet.invokeMethodAsync(method)
        : state.dotNet.invokeMethodAsync(method, arg);
    // Circuit мог уйти на паузу — звук от этого не страдает, ошибку глушим.
    call.catch(() => { });
}

// ───────────────────────── Экспорт для компонента ─────────────────────────

export function attach(dotNetRef) {
    const state = getState();
    state.dotNet = dotNetRef;
    // Само подключение панели не должно даже создавать аудиограф. Всё аудио инициализируется
    // лениво только при намерении включить трек — это не даёт iOS повода захватить аудиосессию.
    installUnlock(state);
    installControls(state);
    // Circuit мог возобновиться поверх уже играющего трека — полосу позиции надо оживить.
    if (state.track) startTicker(state);
    return {
        // Компонент после возобновления circuit должен понять, играет ли что-то уже,
        // и не перезаряжать трек поверх звучащего.
        hasTrack: !!state.track,
        trackId: state.track ? state.track.id : null
    };
}

export function detach(dotNetRef) {
    const state = window[SINGLETON_KEY];
    if (!state) return;
    // Отцепляем только «свою» ссылку: панель могла быть пересоздана, и новая уже записала себя.
    if (!dotNetRef || state.dotNet === dotNetRef) state.dotNet = null;
}

export async function load(track, master, muted) {
    const state = getState();
    state.track = track;
    state.master = Math.min(Math.max(master, 0), 100) / 100;
    state.muted = !!muted;
    clearGestureProbe(state);

    startTicker(state);

    if (track.sourceType === 'YouTube') {
        await stopFile(state);
        await playYouTube(state, track);
        return;
    }

    stopYouTube(state);
    await playFile(state, track);
}

async function playFile(state, track) {
    const audio = ensureAudio(state);
    const ctx = ensureGraph(state);
    if (ctx && ctx.state === 'suspended') await ctx.resume().catch(() => { });

    // Уводим громкость вниз, меняем источник и поднимаем обратно — смена сцены без щелчка.
    if (!audio.paused) {
        fadeTo(state, 0, FADE_SECONDS / 2);
        await wait(FADE_SECONDS * 500);
    }

    audio.loop = !!track.loop;
    audio.src = `/api/minio/audio/${track.source.split('/').map(encodeURIComponent).join('/')}`;

    const seek = () => {
        if (track.startSeconds > 0) {
            try { audio.currentTime = track.startSeconds; } catch { /* источник не перематывается */ }
        }
    };
    audio.addEventListener('loadedmetadata', seek, { once: true });

    try {
        await audio.play();
        fadeTo(state, effectiveGain(state), FADE_SECONDS);
    } catch {
        notify('NotifyNeedsGesture');
    }
}

async function stopFile(state) {
    const audio = state.audio;
    if (!audio || audio.paused) return;
    fadeTo(state, 0, FADE_SECONDS / 2);
    await wait(FADE_SECONDS * 500);
    audio.pause();
}

async function playYouTube(state, track) {
    let player;
    try {
        player = await ensureYouTubePlayer(state);
    } catch {
        notify('NotifyError', 'Не удалось загрузить плеер YouTube.');
        return;
    }

    player.loadVideoById({ videoId: track.source, startSeconds: track.startSeconds || 0 });
    applyYouTubeVolume(state, player);
    startGestureProbe(state);
}

function stopYouTube(state) {
    clearGestureProbe(state);
    if (state.ytPlayer && typeof state.ytPlayer.stopVideo === 'function') {
        state.ytPlayer.stopVideo();
        setYouTubeTucked(state, true);
    }
}

function applyYouTubeVolume(state, player) {
    if (!player || typeof player.setVolume !== 'function') return;
    // На iPad iOS этот вызов игнорируется — громкость ролика остаётся аппаратной.
    player.setVolume(Math.round(effectiveGain(state) * 100));
}

export function pause() {
    const state = getState();
    if (state.audio && !state.audio.paused) state.audio.pause();
    if (state.ytPlayer && typeof state.ytPlayer.pauseVideo === 'function') state.ytPlayer.pauseVideo();
}

export async function resume() {
    const state = getState();
    if (!state.track) return;

    if (state.track.sourceType === 'YouTube') {
        if (state.ytPlayer && typeof state.ytPlayer.playVideo === 'function') {
            state.ytPlayer.playVideo();
            startGestureProbe(state);
        }
        return;
    }

    const ctx = ensureGraph(state);
    if (ctx && ctx.state === 'suspended') await ctx.resume().catch(() => { });
    try {
        await state.audio.play();
        fadeTo(state, effectiveGain(state), FADE_SECONDS / 2);
    } catch {
        notify('NotifyNeedsGesture');
    }
}

export async function stop() {
    const state = getState();
    state.track = null;
    state.scrubbing = false;
    stopTicker(state);
    clearGestureProbe(state);
    stopYouTube(state);
    await stopFile(state);
    if (state.audio) state.audio.removeAttribute('src');
}

export function setVolume(master, muted) {
    const state = getState();
    state.master = Math.min(Math.max(master, 0), 100) / 100;
    state.muted = !!muted;

    if (state.track && state.track.sourceType === 'YouTube') {
        applyYouTubeVolume(state, state.ytPlayer);
        return;
    }
    // Короткая рампа вместо мгновенного скачка — иначе на ползунке слышны щелчки.
    fadeTo(state, effectiveGain(state), 0.08);
}

// Перемотка «снаружи», из C#. Обычная перемотка ползунком идёт мимо Blazor,
// но точка входа нужна, например, чтобы вернуться к началу трека кнопкой.
export function seek(fraction) {
    seekToFraction(getState(), fraction);
}
