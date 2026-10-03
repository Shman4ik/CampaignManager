// Ряд вкладок рабочего места прокручивается по горизонтали (на портрете iPad десять вкладок не помещаются):
// открытую вкладку подводим в видимую область, иначе «НПС» стояла обрезанной у края. Только по горизонтали —
// страницу по вертикали не двигаем.
export function revealActiveTab() {
    const tab = document.querySelector('.cm-tabs [role="tab"][aria-selected="true"]');
    tab?.scrollIntoView({ block: 'nearest', inline: 'center' });
}
