// Прокрутка к первому полю с ошибкой и фокус в него (ValidationScroll). Без плавности: плавная прокрутка не идёт,
// пока вкладка не рисует кадры, а за столом нужен мгновенный переход.

const INVALID = '[aria-invalid="true"], .cm-input-invalid';
const FIELD = "input, select, textarea, button";

export function scrollToFirstInvalid(marker, container) {
    // Ждём кадр: красная рамка появляется в том же рендере, что и вызов.
    requestAnimationFrame(() => {
        const root = (container ? marker.closest(container) : null)
            ?? marker.closest("dialog, form, [role=dialog]")
            ?? marker.parentElement
            ?? document;
        const first = [...root.querySelectorAll(INVALID)].find(isVisible);
        if (!first) {
            return;
        }

        const target = first.matches(FIELD) ? first : first.querySelector(FIELD) ?? first;
        // Липкая шапка (56px) и подвал не должны закрыть поле: центрируем.
        first.scrollIntoView({ block: "center", inline: "nearest" });
        target.focus({ preventScroll: true });
    });
}

function isVisible(element) {
    return element.getClientRects().length > 0;
}
