const SILENT_ATTEMPT_KEY = "cm.silentAttempt";
const SKIP_SILENT_KEY = "cm.skipSilentLogin";

function getStorage() {
    try {
        return window.sessionStorage;
    } catch {
        return null;
    }
}

export function shouldSkipSilentLogin() {
    const storage = getStorage();
    if (!storage) {
        return true;
    }

    const attemptState = storage.getItem(SILENT_ATTEMPT_KEY);
    if (attemptState === "inflight" || attemptState === "failed") {
        return true;
    }

    return storage.getItem(SKIP_SILENT_KEY) === "true";
}

export function markSilentAttempt() {
    const storage = getStorage();
    if (!storage) {
        return;
    }

    storage.setItem(SILENT_ATTEMPT_KEY, "inflight");
}

export function noteSilentFailure() {
    const storage = getStorage();
    if (!storage) {
        return;
    }

    storage.setItem(SILENT_ATTEMPT_KEY, "failed");
}

export function clearSilentFlags() {
    const storage = getStorage();
    if (!storage) {
        return;
    }

    storage.removeItem(SILENT_ATTEMPT_KEY);
    storage.removeItem(SKIP_SILENT_KEY);
}

export function setSkipSilentLogin() {
    const storage = getStorage();
    if (!storage) {
        return;
    }

    storage.setItem(SKIP_SILENT_KEY, "true");
}
