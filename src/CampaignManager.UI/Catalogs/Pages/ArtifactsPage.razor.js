// Прокрутка к раскрытому артефакту: карточка встаёт во всю ширину сетки отдельным рядом и уезжает от места касания.
// Отступ сверху — шапка (scroll-padding-top страницы) и липкая панель фильтров по её настоящей высоте: в портрете
// панель в две строки, и отступ на одну строку прятал название под ней. При прямом заходе по ?open= список ещё
// грузится — карточку ждём до двух секунд (таймером: кадры невидимая вкладка не рисует).
export async function reveal(id) {
    const deadline = Date.now() + 2000;
    let element = document.getElementById(id);
    while (!element && Date.now() < deadline) {
        await new Promise(resolve => setTimeout(resolve, 50));
        element = document.getElementById(id);
    }

    if (!element) {
        return false;
    }

    const page = element.closest(".cm-page");
    const sticky = page && [...page.children].find(child => getComputedStyle(child).position === "sticky");
    element.style.scrollMarginTop = sticky ? `${sticky.offsetHeight + 8}px` : "0px";
    // Без плавности: плавная прокрутка не идёт, пока вкладка не рисует кадры.
    element.scrollIntoView({ block: "start" });
    return true;
}
