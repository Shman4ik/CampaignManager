// Прокрутка таблицы справочника (DataTable): к началу после смены страницы, фильтра или порядка и к раскрытой по
// ссылке (?open=) строке. Без плавности: плавная прокрутка не идёт, пока вкладка не рисует кадры.

/** Таблица — свой скроллпорт (шапка липкая), на узком экране вместо неё карточки: тогда прокручивается страница. */
function visible(element) {
    return element && element.offsetParent !== null;
}

export function scrollToStart(wrap, cards) {
    requestAnimationFrame(() => {
        if (visible(wrap)) {
            wrap.scrollTop = 0;
            // Страница могла остаться прокрученной ниже таблицы (кнопка «2» стоит под ней).
            if (wrap.getBoundingClientRect().top < 56) {
                wrap.scrollIntoView({ block: "start" });
            }
        } else if (cards && cards.getBoundingClientRect().top < 56) {
            cards.scrollIntoView({ block: "start" });
        }
    });
}

export function revealOpen(wrap, cards) {
    requestAnimationFrame(() => {
        if (visible(wrap)) {
            const row = wrap.querySelector("tr.cm-row-expanded");
            if (!row) {
                return;
            }

            // Липкая шапка закрывает верх скроллпорта: считаем видимую область под ней.
            const head = wrap.querySelector("thead")?.getBoundingClientRect().height ?? 0;
            const port = wrap.getBoundingClientRect();
            const rowBox = row.getBoundingClientRect();
            const detail = row.nextElementSibling?.classList.contains("cm-row-detail") ? row.nextElementSibling : null;
            const bottom = (detail ?? row).getBoundingClientRect().bottom;
            const top = rowBox.top - port.top - head;
            if (top < 0 || bottom > port.bottom) {
                wrap.scrollTop += top;
            }

            // И сама страница: таблица может стоять ниже экрана (портрет, длинная панель фильтров).
            const after = row.getBoundingClientRect();
            if (after.top < 56 || after.bottom > window.innerHeight) {
                wrap.scrollIntoView({ block: "start" });
            }
        } else if (cards) {
            const card = cards.querySelector('[data-open="true"]');
            if (card) {
                card.scrollIntoView({ block: "nearest" });
            }
        }
    });
}
