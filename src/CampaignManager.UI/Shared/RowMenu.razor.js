// Меню «⋯» (RowMenu): ставит fixed-панель у кнопки и закрывает её прокруткой. Панель в верхней части страницы
// не должна обрезаться таблицей с overflow, поэтому она fixed, а координаты считаются здесь.

const GAP = 4;
const EDGE = 8;

export function place(button, panel, dotnet) {
    const anchor = button.getBoundingClientRect();
    const width = panel.offsetWidth;
    const height = panel.offsetHeight;

    // Правый край меню — по правому краю кнопки; не вылезает за экран.
    const left = Math.max(EDGE, Math.min(anchor.right - width, window.innerWidth - width - EDGE));
    // Снизу не помещается — открываем вверх.
    const below = anchor.bottom + GAP;
    const top = below + height > window.innerHeight - EDGE
        ? Math.max(EDGE, anchor.top - height - GAP)
        : below;

    panel.style.left = `${left}px`;
    panel.style.top = `${top}px`;
    panel.style.visibility = "visible";
    panel.querySelector("[role^=menuitem]:not([disabled])")?.focus({ preventScroll: true });

    const onScroll = (event) => {
        if (!panel.contains(event.target)) {
            dotnet.invokeMethodAsync("CloseFromScript");
        }
    };
    window.addEventListener("scroll", onScroll, true);
    window.addEventListener("resize", onScroll);

    return {
        release() {
            window.removeEventListener("scroll", onScroll, true);
            window.removeEventListener("resize", onScroll);
            if (button.isConnected) {
                button.focus({ preventScroll: true });
            }
        },
    };
}
