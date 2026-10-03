// Трасса погони (ChaseTrack): показать, где сейчас ход. Широкая трасса (до 40 локаций) прокручивается в своём блоке; на
// портрете iPad видно шесть локаций из восьми, а в конце погони все бегущие стоят справа за краем (H4). Прокручивается
// только сам блок — страница под пальцем не двигается. Без плавности: за столом нужен мгновенный переход.

export function centerActive(container) {
    requestAnimationFrame(() => {
        if (!container || container.scrollWidth <= container.clientWidth) {
            return;
        }

        // Тот, чей ход; нет его (расстановка, конец) — самая дальняя занятая локация.
        const active = container.querySelector(".chase-location-active");
        const occupied = [...container.querySelectorAll(".chase-location")].filter(l => l.querySelector(".chase-runner"));
        const target = active ?? occupied[occupied.length - 1];
        if (!target) {
            return;
        }

        const left = target.offsetLeft - (container.clientWidth - target.offsetWidth) / 2;
        container.scrollLeft = Math.max(0, left);
    });
}
