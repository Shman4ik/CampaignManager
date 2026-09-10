// Пауза и возобновление circuit (Blazor Server, .NET 10).
//
// Состояние страницы живёт в circuit на сервере. Пока circuit «на паузе», Blazor держит
// его состояние в браузере, поэтому сессия переживает и обрыв связи, и перезапуск сервера:
//  - вкладка ушла в фон  -> Safari на iPad всё равно уронит WebSocket, лучше встать на паузу сами;
//  - сервер выключается  -> ActiveCircuitTracker дёргает pauseForShutdown перед остановкой;
//  - вкладка вернулась   -> пробуем возобновиться, при неудаче — с нарастающей паузой.
//
// Состояния диалога переподключения приходят событием components-reconnect-state-changed
// на элемент #components-reconnect-modal (разметка в App.razor).
(function () {
    'use strict';

    // Пауза дешёвой только кажется: она рвёт соединение и поднимает диалог переподключения.
    // Заглянуть в соседнюю вкладку и вернуться — обычное дело за столом, такое пережидаем.
    // Полминуты — примерно столько браузер ещё крутит таймеры в фоновой вкладке; если он
    // соберётся заморозить страницу раньше, сработают freeze/pagehide ниже.
    const PAUSE_AFTER_HIDDEN_MS = 30000;

    // Возобновление после деплоя: новый экземпляр поднимается не сразу. Последняя попытка
    // примерно через две с половиной минуты, дальше остаётся кнопка «Продолжить».
    const RESUME_DELAYS_MS = [1000, 3000, 6000, 10000, 15000, 20000, 30000, 30000, 30000];

    let pauseTimerId = 0;
    let resumeTimerId = 0;
    let resumeAttempt = 0;
    let isPaused = false;

    function hasBlazorMethod(name) {
        return typeof window.Blazor !== 'undefined' && typeof window.Blazor[name] === 'function';
    }

    function clearPauseTimer() {
        if (pauseTimerId) {
            clearTimeout(pauseTimerId);
            pauseTimerId = 0;
        }
    }

    function clearResumeTimer() {
        if (resumeTimerId) {
            clearTimeout(resumeTimerId);
            resumeTimerId = 0;
        }
    }

    function pauseCircuit() {
        clearPauseTimer();
        if (!hasBlazorMethod('pauseCircuit')) return;

        window.Blazor.pauseCircuit().catch(function () {
            // Соединение уже могло закрыться — тогда паузу ставить не над чем.
        });
    }

    function resumeNow() {
        clearResumeTimer();
        if (!hasBlazorMethod('resumeCircuit')) return;

        window.Blazor.resumeCircuit()
            .then(function (resumed) {
                if (!resumed) scheduleResume();
            })
            .catch(function () {
                scheduleResume();
            });
    }

    function scheduleResume() {
        clearResumeTimer();

        // В фоне возобновляться незачем: вернёмся к этому, когда вкладка станет видимой.
        if (document.visibilityState === 'hidden') return;
        if (resumeAttempt >= RESUME_DELAYS_MS.length) return;

        const delay = RESUME_DELAYS_MS[resumeAttempt];
        resumeAttempt += 1;
        resumeTimerId = setTimeout(resumeNow, delay);
    }

    function handleReconnectStateChanged(event) {
        const state = event.detail && event.detail.state;

        if (state === 'paused') {
            isPaused = true;
            resumeAttempt = 0;
            scheduleResume();
        } else if (state === 'failed') {
            // Переподключение исчерпало попытки: circuit на сервере не нашёлся.
            // Пробуем поднять его заново из состояния, сохранённого в браузере.
            resumeAttempt = 0;
            scheduleResume();
        } else if (state === 'hide') {
            isPaused = false;
            resumeAttempt = 0;
            clearResumeTimer();
        }
    }

    function handleVisibilityChange() {
        if (document.visibilityState === 'hidden') {
            clearResumeTimer();
            clearPauseTimer();
            pauseTimerId = setTimeout(pauseCircuit, PAUSE_AFTER_HIDDEN_MS);
            return;
        }

        clearPauseTimer();
        if (isPaused) {
            resumeAttempt = 0;
            resumeNow();
        }
    }

    function onClick(elementId, handler) {
        const element = document.getElementById(elementId);
        if (element) element.addEventListener('click', handler);
    }

    function init() {
        const dialog = document.getElementById('components-reconnect-modal');
        if (dialog) {
            dialog.addEventListener('components-reconnect-state-changed', handleReconnectStateChanged);
        }

        onClick('cm-reconnect-resume', function () {
            resumeAttempt = 0;
            resumeNow();
        });

        onClick('cm-reconnect-retry', function () {
            if (hasBlazorMethod('reconnect')) window.Blazor.reconnect();
        });

        onClick('cm-reconnect-reload', function () {
            window.location.reload();
        });

        document.addEventListener('visibilitychange', handleVisibilityChange);

        // Браузер вот-вот остановит JS на странице: Chrome шлёт freeze замороженной вкладке,
        // Safari на iPad — pagehide с persisted при сворачивании приложения. Дожидаться
        // таймера тут уже поздно, ставим паузу немедленно.
        document.addEventListener('freeze', pauseCircuit);
        window.addEventListener('pagehide', function (event) {
            if (event.persisted || document.visibilityState === 'hidden') pauseCircuit();
        });
    }

    // Вызывается с сервера перед остановкой приложения (ActiveCircuitTracker).
    // Возвращаемся сразу: пауза закрывает то самое соединение, по которому пришёл вызов.
    window.campaignManagerCircuit = {
        pauseForShutdown: function () {
            pauseCircuit();
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
