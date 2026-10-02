// Звук плеера фонотеки. Перенос music-player.js v1 без обходов circuit: в WebAssembly состояние плеера
// (что играет, пул, громкость) держит синглтон MusicPlayer в C#, а модуль — только звук.
//
// Три вещи держат всё остальное — их легко сломать правкой «на вид безобидной»:
//
// 1. Состояние — на уровне модуля (модуль импортируется один раз на вкладку, это и есть синглтон),
//    а <audio> и окошко YouTube — в #cm-music-host прямо на <body>, вне дерева Blazor. Панель может
//    пересоздаться (выход и вход, ошибка страницы) — звук от этого не обрывается.
//
// 2. Громкость — только через Web Audio GainNode. iOS игнорирует HTMLMediaElement.volume (значение
//    выставляется, звук прежний), а целевое устройство — iPad. createMediaElementSource можно звать один
//    раз на элемент — отсюда один элемент на вкладку. Источник обязан быть нашего origin
//    (/api/v1/files/{id}): на элементе с чужого origin без CORS Web Audio отдаёт тишину.
//
// 3. iframe YouTube нельзя переносить по DOM: любой reparent перезагружает плеер и обрывает трек.
//    Окошко «спрятано» уводом за экран, а не display:none — скрытый так iframe перестаёт играть.

const FADE_SECONDS = 1.2;
const GESTURE_PROBE_MS = 2000;
const TICK_MS = 250;

// Элементы, которыми модуль управляет напрямую. Blazor рисует их с неизменной разметкой и больше не
// патчит, поэтому значения, выставленные отсюда, переживают перерисовки панели.
const SEEK_ID = 'cm-music-seek';
const ELAPSED_ID = 'cm-music-elapsed';
const DURATION_ID = 'cm-music-duration';
const UNLOCK_ID = 'cm-music-unlock';
const START_TRIGGER_SELECTOR = '.cm-music-start';
const HOST_ID = 'cm-music-host';

// Пустой WAV: им «расчехляем» аудио-элемент внутри настоящего жеста пользователя.
const SILENT_WAV = 'data:audio/wav;base64,UklGRiQAAABXQVZFZm10IBAAAAABAAEAgD4AAAB9AAACABAAZGF0YQAAAAA=';

const state = {
    audio: null,
    ctx: null,
    gain: null,
    ytHost: null,
    ytPlayer: null,
    ytReady: null,
    ytApi: null,
    dotNet: null,
    track: null,
    master: 0.6,
    muted: false,
    unlocked: false,
    unlockInstalled: false,
    controlsInstalled: false,
    gestureTimer: null,
    ticker: null,
    scrubbing: false,
};

// ───────────────────────── Аудио-элемент и граф Web Audio ─────────────────────────

// Свой контейнер на <body>, вне корня Blazor: его не трогает ни сравнение DOM, ни пересоздание панели.
function playerHost() {
    let host = document.getElementById(HOST_ID);
    if (!host) {
        host = document.createElement('div');
        host.id = HOST_ID;
        document.body.appendChild(host);
    }
    return host;
}

function ensureAudio() {
    if (state.audio) {
        // Элемент могли вынести из документа — браузер такой ставит на паузу. Возвращаем на место.
        if (!state.audio.isConnected) playerHost().appendChild(state.audio);
        return state.audio;
    }

    const audio = document.createElement('audio');
    audio.id = 'cm-music-audio';
    audio.preload = 'none';
    audio.setAttribute('playsinline', '');
    playerHost().appendChild(audio);

    audio.addEventListener('ended', () => {
        // Пустой WAV из installUnlock «заканчивается» сразу же; его конец нельзя принимать за конец
        // трека — иначе панель показывала «на паузе» при играющей музыке, на первом же касании.
        if (state.track?.kind !== 'file') return;
        if (audio.currentSrc === SILENT_WAV) return;
        // Зацикленный трек сюда не приходит — его крутит сам браузер.
        notify('NotifyEnded');
    });
    audio.addEventListener('error', () => {
        if (state.track?.kind === 'file' && audio.currentSrc !== SILENT_WAV) {
            notify('NotifyError', 'Не удалось загрузить файл трека.');
        }
    });

    state.audio = audio;
    return audio;
}

function ensureGraph() {
    if (state.ctx) return state.ctx;

    const Ctx = window.AudioContext || window.webkitAudioContext;
    if (!Ctx) return null;

    const ctx = new Ctx();
    const gain = ctx.createGain();
    gain.gain.value = 0.0001;
    gain.connect(ctx.destination);
    ctx.createMediaElementSource(ensureAudio()).connect(gain);

    state.ctx = ctx;
    state.gain = gain;
    return ctx;
}

function effectiveGain() {
    if (state.muted) return 0;
    const trackVolume = state.track && typeof state.track.volume === 'number'
        ? Math.min(Math.max(state.track.volume, 0), 100) / 100
        : 1;
    return state.master * trackVolume;
}

function fadeTo(target, seconds) {
    if (!state.ctx || !state.gain) return;
    const param = state.gain.gain;
    const now = state.ctx.currentTime;
    const floor = 0.0001;
    param.cancelScheduledValues(now);
    param.setValueAtTime(Math.max(param.value, floor), now);
    param.linearRampToValueAtTime(Math.max(target, floor), now + seconds);
}

const wait = ms => new Promise(resolve => setTimeout(resolve, ms));

// ───────────────────────── Снятие блокировки iOS ─────────────────────────

// Срабатывает только на кнопке, которая действительно запускает музыку (класс cm-music-start), и в
// фазе захвата — раньше обработчика Blazor. Внутри настоящего жеста AudioContext переходит в running,
// а аудио-элемент помечается «пользователь разрешил»: после этого play() из C# проходит, хотя сам уже
// не жест. На произвольное касание страницы не реагируем: на iPad даже пустой WAV захватывает системную
// аудиосессию и глушит музыку другого приложения, хотя сайт ещё ничего не играет.
function installUnlock() {
    if (state.unlocked || state.unlockInstalled) return;
    state.unlockInstalled = true;

    const removeListeners = () => {
        document.removeEventListener('pointerdown', unlock, true);
        document.removeEventListener('touchend', unlock, true);
        document.removeEventListener('keydown', unlock, true);
    };

    function unlock(event) {
        if (event.type === 'keydown' && event.key !== 'Enter' && event.key !== ' ') return;
        if (!event.target?.closest?.(START_TRIGGER_SELECTOR)) return;

        state.unlocked = true;
        removeListeners();
        const ctx = ensureGraph();
        if (ctx && ctx.state === 'suspended') ctx.resume().catch(() => { });

        const audio = ensureAudio();
        if (!audio.src) {
            audio.src = SILENT_WAV;
            const played = audio.play();
            // Ответ play() асинхронный; за это время C# может зарядить настоящий трек в тот же элемент —
            // ставим на паузу, только если играет всё ещё заглушка.
            played?.then?.(() => {
                if (audio.currentSrc === SILENT_WAV) audio.pause();
            }).catch(() => { });
        }
    }

    document.addEventListener('pointerdown', unlock, true);
    // Старые версии iOS не посылают Pointer Events.
    document.addEventListener('touchend', unlock, true);
    document.addEventListener('keydown', unlock, true);
}

// ───────────────────────── YouTube ─────────────────────────

function ensureYouTubeHost() {
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

// Окошко вынесли из документа — iframe внутри уже мёртв, а объект YT.Player отвечает в пустоту. Сбрасываем
// всё, следующий load() соберёт плеер заново: лучше один оборванный трек, чем немые кнопки до F5.
function dropDetachedYouTubePlayer() {
    if (!state.ytHost || state.ytHost.isConnected) return;
    try {
        state.ytPlayer?.destroy?.();
    } catch {
        // iframe уже недоступен.
    }
    state.ytHost.remove();
    state.ytHost = null;
    state.ytPlayer = null;
    state.ytReady = null;
}

function loadYouTubeApi() {
    if (state.ytApi) return state.ytApi;

    state.ytApi = new Promise((resolve, reject) => {
        if (window.YT?.Player) {
            resolve(window.YT);
            return;
        }

        // Скрипт YouTube зовёт глобальный колбэк — другого способа узнать о готовности API нет.
        const previous = window.onYouTubeIframeAPIReady;
        window.onYouTubeIframeAPIReady = () => {
            if (typeof previous === 'function') previous();
            resolve(window.YT);
        };

        const script = document.createElement('script');
        script.src = 'https://www.youtube.com/iframe_api';
        script.async = true;
        script.onerror = () => {
            state.ytApi = null;
            reject(new Error('Не удалось загрузить плеер YouTube.'));
        };
        document.head.appendChild(script);
    });

    return state.ytApi;
}

async function ensureYouTubePlayer() {
    dropDetachedYouTubePlayer();

    if (state.ytPlayer) {
        await state.ytReady;
        return state.ytPlayer;
    }

    ensureYouTubeHost();
    const YT = await loadYouTubeApi();

    state.ytReady = new Promise(resolve => {
        state.ytPlayer = new YT.Player('cm-music-yt-mount', {
            width: '160',
            height: '90',
            playerVars: { playsinline: 1, rel: 0, modestbranding: 1 },
            events: {
                onReady: () => resolve(state.ytPlayer),
                onStateChange: event => {
                    if (event.data === YT.PlayerState.PLAYING) {
                        clearGestureProbe();
                        setYouTubeTucked(true);
                    }
                    if (event.data === YT.PlayerState.ENDED) notify('NotifyEnded');
                },
                onError: () => {
                    clearGestureProbe();
                    setYouTubeTucked(false);
                    notify('NotifyError',
                        'Этот ролик нельзя воспроизвести встроенным плеером — владелец запретил встраивание. Выберите другой трек или загрузите файл.');
                },
            },
        });
    });

    await state.ytReady;
    return state.ytPlayer;
}

function setYouTubeTucked(tucked) {
    state.ytHost?.classList.toggle('cm-yt-host--tucked', tucked);
}

// iOS может отказать в программном playVideo(), пока пользователь ни разу не тронул сам плеер. Через
// две секунды без звука показываем окошко и просим «Включить звук».
function startGestureProbe() {
    clearGestureProbe();
    state.gestureTimer = setTimeout(() => {
        state.gestureTimer = null;
        const player = state.ytPlayer;
        if (typeof player?.getPlayerState !== 'function') return;
        const playing = player.getPlayerState() === 1 || player.getPlayerState() === 3;
        if (playing) return;
        setYouTubeTucked(false);
        notify('NotifyNeedsGesture');
    }, GESTURE_PROBE_MS);
}

function clearGestureProbe() {
    if (state.gestureTimer) {
        clearTimeout(state.gestureTimer);
        state.gestureTimer = null;
    }
}

// ───────────────────── Позиция и перемотка ─────────────────────

function currentTimes() {
    if (!state.track) return null;

    if (state.track.kind === 'youtube') {
        const player = state.ytPlayer;
        if (typeof player?.getDuration !== 'function') return null;
        return { position: player.getCurrentTime() || 0, duration: player.getDuration() || 0 };
    }

    const audio = state.audio;
    if (!audio) return null;
    // У ещё не разобранного файла длительность — NaN или Infinity.
    return { position: audio.currentTime || 0, duration: Number.isFinite(audio.duration) ? audio.duration : 0 };
}

function formatTime(seconds) {
    const total = Math.max(0, Math.floor(seconds));
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const pad = value => String(value).padStart(2, '0');
    return hours > 0 ? `${hours}:${pad(minutes)}:${pad(total % 60)}` : `${minutes}:${pad(total % 60)}`;
}

function renderProgress() {
    const slider = document.getElementById(SEEK_ID);
    if (!slider) return;

    const elapsed = document.getElementById(ELAPSED_ID);
    const duration = document.getElementById(DURATION_ID);
    const times = currentTimes();

    // Длительности нет — прямой эфир или файл ещё не разобран. Перематывать нечего.
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

function startTicker() {
    if (!state.ticker) state.ticker = setInterval(renderProgress, TICK_MS);
}

function stopTicker() {
    if (state.ticker) {
        clearInterval(state.ticker);
        state.ticker = null;
    }
}

function seekToFraction(fraction) {
    const times = currentTimes();
    if (!times || times.duration <= 0) return;

    // Полсекунды от конца: точное попадание в конец тут же роняет трек в «закончился».
    const target = Math.max(0, Math.min(times.duration - 0.5, fraction * times.duration));
    if (state.track.kind === 'youtube') {
        state.ytPlayer?.seekTo?.(target, true);
        return;
    }

    try {
        // Файл отдаётся с Range (206): браузер запросит только отрезок вокруг новой точки.
        state.audio.currentTime = target;
    } catch {
        // Источник не перематывается — ползунок вернётся на место следующим тиком.
    }
}

// Всё синхронно и без await: активация жеста живёт только в синхронной части обработчика.
function unlockPlayback() {
    state.unlocked = true;
    const ctx = ensureGraph();
    if (ctx && ctx.state === 'suspended') ctx.resume().catch(() => { });

    if (state.track?.kind === 'youtube') {
        if (typeof state.ytPlayer?.playVideo === 'function') {
            state.ytPlayer.playVideo();
            startGestureProbe();
        }
    } else if (state.audio) {
        state.audio.play()?.then?.(() => fadeTo(effectiveGain(), FADE_SECONDS / 2)).catch(() => { });
    }

    notify('NotifyGestureResolved');
}

// Слушатели делегированы на document и ставятся один раз: панель перерисовывается, и привязка к
// конкретным элементам этого бы не пережила.
function installControls() {
    if (state.controlsInstalled) return;
    state.controlsInstalled = true;

    document.addEventListener('input', event => {
        if (event.target?.id !== SEEK_ID) return;
        state.scrubbing = true;
        const times = currentTimes();
        const elapsed = document.getElementById(ELAPSED_ID);
        if (times && times.duration > 0 && elapsed) {
            elapsed.textContent = formatTime((event.target.value / 1000) * times.duration);
        }
    });

    document.addEventListener('change', event => {
        if (event.target?.id !== SEEK_ID) return;
        state.scrubbing = false;
        seekToFraction(event.target.value / 1000);
    });

    // «Включить звук» — прямой слушатель, а не @onclick: запуск обязан случиться внутри жеста, синхронно.
    document.addEventListener('click', event => {
        if (!event.target?.closest?.('#' + UNLOCK_ID)) return;
        unlockPlayback();
    }, true);
}

// ───────────────────────── Обратная связь в C# ─────────────────────────

function notify(method, arg) {
    if (!state.dotNet) return;
    const call = arg === undefined ? state.dotNet.invokeMethodAsync(method) : state.dotNet.invokeMethodAsync(method, arg);
    call.catch(() => { });
}

// ───────────────────────── Экспорт для панели ─────────────────────────

/** Подключить панель. Аудиограф лениво — только при намерении включить трек (иначе iOS захватит аудиосессию). */
export function attach(dotNetRef) {
    state.dotNet = dotNetRef;
    installUnlock();
    installControls();
    if (state.track) startTicker();
    // Панель могла пересоздаться поверх играющего трека — она не должна перезаряжать его.
    return { hasTrack: !!state.track, trackId: state.track ? state.track.id : null };
}

export function detach(dotNetRef) {
    // Отцепляем только «свою» ссылку: новая панель могла уже записать себя.
    if (!dotNetRef || state.dotNet === dotNetRef) state.dotNet = null;
}

export async function load(track, master, muted) {
    state.track = track;
    state.master = Math.min(Math.max(master, 0), 100) / 100;
    state.muted = !!muted;
    clearGestureProbe();
    startTicker();

    if (track.kind === 'youtube') {
        await stopFile();
        await playYouTube(track);
        return;
    }

    stopYouTube();
    await playFile(track);
}

async function playFile(track) {
    const audio = ensureAudio();
    const ctx = ensureGraph();
    if (ctx && ctx.state === 'suspended') await ctx.resume().catch(() => { });

    // Уводим громкость вниз, меняем источник и поднимаем — смена сцены без щелчка.
    if (!audio.paused) {
        fadeTo(0, FADE_SECONDS / 2);
        await wait(FADE_SECONDS * 500);
    }

    audio.loop = !!track.loop;
    audio.src = track.url;
    if (track.startSeconds > 0) {
        audio.addEventListener('loadedmetadata', () => {
            try { audio.currentTime = track.startSeconds; } catch { /* источник не перематывается */ }
        }, { once: true });
    }

    try {
        await audio.play();
        fadeTo(effectiveGain(), FADE_SECONDS);
    } catch {
        notify('NotifyNeedsGesture');
    }
}

async function stopFile() {
    const audio = state.audio;
    if (!audio || audio.paused) return;
    fadeTo(0, FADE_SECONDS / 2);
    await wait(FADE_SECONDS * 500);
    audio.pause();
}

async function playYouTube(track) {
    let player;
    try {
        player = await ensureYouTubePlayer();
    } catch {
        notify('NotifyError', 'Не удалось загрузить плеер YouTube.');
        return;
    }

    player.loadVideoById({ videoId: track.youtubeId, startSeconds: track.startSeconds || 0 });
    applyYouTubeVolume(player);
    startGestureProbe();
}

function stopYouTube() {
    clearGestureProbe();
    if (typeof state.ytPlayer?.stopVideo === 'function') {
        state.ytPlayer.stopVideo();
        setYouTubeTucked(true);
    }
}

// На iPad вызов игнорируется — громкость ролика там аппаратная (iframe чужой, Web Audio к нему не подключить).
function applyYouTubeVolume(player) {
    if (typeof player?.setVolume === 'function') player.setVolume(Math.round(effectiveGain() * 100));
}

export function pause() {
    if (state.audio && !state.audio.paused) state.audio.pause();
    state.ytPlayer?.pauseVideo?.();
}

export async function resume() {
    if (!state.track) return;

    if (state.track.kind === 'youtube') {
        if (typeof state.ytPlayer?.playVideo === 'function') {
            state.ytPlayer.playVideo();
            startGestureProbe();
        }
        return;
    }

    const ctx = ensureGraph();
    if (ctx && ctx.state === 'suspended') await ctx.resume().catch(() => { });
    try {
        await state.audio.play();
        fadeTo(effectiveGain(), FADE_SECONDS / 2);
    } catch {
        notify('NotifyNeedsGesture');
    }
}

export async function stop() {
    state.track = null;
    state.scrubbing = false;
    stopTicker();
    clearGestureProbe();
    stopYouTube();
    await stopFile();
    state.audio?.removeAttribute('src');
}

export function setVolume(master, muted) {
    state.master = Math.min(Math.max(master, 0), 100) / 100;
    state.muted = !!muted;
    if (state.track?.kind === 'youtube') {
        applyYouTubeVolume(state.ytPlayer);
        return;
    }
    // Короткая рампа вместо скачка — иначе на ползунке слышны щелчки.
    fadeTo(effectiveGain(), 0.08);
}

/** Перемотка из C# (кнопка «в начало»); обычная перемотка ползунком идёт мимо C#. */
export function seek(fraction) {
    seekToFraction(fraction);
}
