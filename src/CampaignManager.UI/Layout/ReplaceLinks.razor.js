// Обычный клик по <a data-replace> — переход Blazor с заменой записи в истории; средний клик (auxclick) и клик с Ctrl,
// Cmd, Shift, Alt сюда не попадают или пропускаются — их обрабатывает браузер (новая вкладка, окно). Слушатель — в фазе
// захвата на document: он раньше перехвата ссылок Blazor, а stopPropagation не даёт тому положить запись в историю.
let installed = false;

export function install() {
    if (installed) return;
    installed = true;

    document.addEventListener('click', event => {
        if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        const link = event.target instanceof Element ? event.target.closest('a[data-replace][href]') : null;
        if (!link || (link.target && link.target !== '_self') || link.hasAttribute('download')) return;
        if (link.origin !== location.origin) return;

        event.preventDefault();
        event.stopPropagation();
        Blazor.navigateTo(link.href, { replaceHistoryEntry: true });
    }, true);
}
