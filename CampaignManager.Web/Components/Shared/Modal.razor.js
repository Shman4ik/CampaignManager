export function focusElement(element) {
    if (element) {
        // Small delay to ensure the element is rendered
        setTimeout(() => {
            element.focus();
        }, 100);
    }
}

export function preventBodyScroll(prevent) {
    if (prevent) {
        // Отступ под полосу прокрутки не добавляем: у html стоит scrollbar-gutter: stable, место
        // под неё и так держится, а лишние 15px сужали страницу и перестраивали сетку под модалкой.

        // Store current scroll position
        const scrollY = window.scrollY;
        document.body.style.position = 'fixed';
        document.body.style.top = `-${scrollY}px`;
        document.body.style.width = '100%';
        // В design-system.css у body height: 100% — высота окна. Зафиксированное и сдвинутое
        // на top: -scrollY тело осталось бы высотой в один экран, и overflow: hidden срезал бы
        // всё ниже «высота окна − прокрутка»: под модалкой пропадала нижняя часть страницы.
        document.body.style.height = 'auto';
        document.body.style.overflow = 'hidden';
    } else {
        // Restore scroll position
        const scrollY = document.body.style.top;
        document.body.style.position = '';
        document.body.style.top = '';
        document.body.style.width = '';
        document.body.style.height = '';
        document.body.style.overflow = '';
        if (scrollY) {
            window.scrollTo(0, parseInt(scrollY || '0') * -1);
        }
    }
}
