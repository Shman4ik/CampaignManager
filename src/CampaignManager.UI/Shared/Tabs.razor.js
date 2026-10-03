// Tabs (Scrolls): открытая вкладка в видимой части прокручиваемой строки. Прокручивается сама строка — страница под пальцем
// не двигается; без плавности: за столом нужен мгновенный переход.

export function reveal(container) {
    requestAnimationFrame(() => {
        const tab = container?.querySelector('.cm-tab[aria-selected="true"]');
        if (!tab || container.scrollWidth <= container.clientWidth) {
            return;
        }

        const left = tab.offsetLeft - (container.clientWidth - tab.offsetWidth) / 2;
        container.scrollLeft = Math.max(0, left);
    });
}
