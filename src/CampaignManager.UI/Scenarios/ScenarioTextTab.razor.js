// «Содержание» вкладки «Текст»: касание названия прокручивает страницу к заголовку Markdown. Заголовков в разметке столько
// же, сколько у MarkdownText.Headings, и идут они в том же порядке, поэтому ищем по номеру — id у них нет.
// Без плавности: за столом нужен мгновенный переход; поле над заголовком — под липкой шапкой (scroll-margin в scenario.css).
export function scrollToHeading(container, index) {
    const heading = container?.querySelectorAll("h1, h2, h3, h4, h5, h6")[index];
    heading?.scrollIntoView({ block: "start" });
}
