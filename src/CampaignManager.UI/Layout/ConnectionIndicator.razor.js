// События online/offline браузера для ApiActivity. Возвращает объект с dispose(),
// который снимает подписку, — его держит ConnectionIndicator.
export function watch(dotnet) {
    const report = () => dotnet.invokeMethodAsync("SetBrowserOnline", navigator.onLine);
    window.addEventListener("online", report);
    window.addEventListener("offline", report);
    if (!navigator.onLine) {
        report();
    }

    return {
        dispose() {
            window.removeEventListener("online", report);
            window.removeEventListener("offline", report);
        },
    };
}
