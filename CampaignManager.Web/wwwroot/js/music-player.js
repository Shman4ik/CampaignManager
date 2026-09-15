// Плеер фонотеки Хранителя.
//
// Три вещи определяют устройство этого модуля, и все три легко сломать правкой «на вид безобидной»:
//
// 1. ВЕСЬ звук живёт в синглтоне на window, а <audio> и контейнер YouTube висят прямо на
//    document.body — не в дереве Blazor. Пауза circuit (вкладка скрыта 30 с, js/circuit-persistence.js)
//    при возобновлении пересобирает страницу заново; всё, что рендерил компонент, пересоздаётся,
//    и музыка оборвалась бы посреди сцены. Страница при resume не перезагружается, поэтому
//    синглтон и элементы переживают паузу. Не переносить элементы внутрь .razor.
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
        gestureTimer: null
    };
    window[SINGLETON_KEY] = state;
    return state;
}

// ───────────────────────── Аудио-элемент и граф Web Audio ─────────────────────────

function ensureAudio(state) {
    if (state.audio) return state.audio;

    const audio = document.createElement('audio');
    audio.id = 'cm-music-audio';
    audio.preload = 'none';
    // Не хочется, чтобы iOS показывал наш эмбиент в «Сейчас играет» как видео.
    audio.setAttribute('playsinline', '');
    document.body.appendChild(audio);

    audio.addEventListener('ended', () => {
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

// Вешается один раз и срабатывает на первом же касании страницы. Внутри настоящего жеста
// и AudioContext переводится в running, и аудио-элемент помечается «пользователь разрешил» —
// после этого play() из Blazor проходит, хотя сам по себе жестом уже не считается.
function installUnlock(state) {
    if (state.unlocked || state.unlockInstalled) return;
    state.unlockInstalled = true;

    const unlock = () => {
        state.unlocked = true;
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

    document.addEventListener('pointerdown', unlock, { capture: true, once: true });
    document.addEventListener('touchend', unlock, { capture: true, once: true });
    document.addEventListener('keydown', unlock, { capture: true, once: true });
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
    document.body.appendChild(host);

    state.ytHost = host;
    return host;
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
    ensureAudio(state);
    installUnlock(state);
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
