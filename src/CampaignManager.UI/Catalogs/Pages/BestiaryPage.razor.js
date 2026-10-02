// Прокрутка к раскрытой твари. Отступ под липкие шапку и фильтры — scroll-margin-top карточки (CSS),
// а не расчёт здесь. false — карточки ещё нет в DOM (адрес сменился, рендер не дошёл): страница
// попробует в следующем рендере.
export function reveal(id) {
    const element = document.getElementById(id);
    if (!element) {
        return false;
    }

    // Без плавности: плавная прокрутка не идёт, пока вкладка не рисует кадры, и раскрытая карточка
    // оставалась ниже края.
    element.scrollIntoView({ block: "start" });
    return true;
}
