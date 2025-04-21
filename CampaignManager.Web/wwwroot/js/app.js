// Функция для плавной прокрутки к элементу
// Функция для плавной прокрутки к элементу с учетом фиксированной верхней панели
window.scrollToElement = function (elementId) {
    const element = document.getElementById(elementId);
    if (!element) return;

    // Получаем высоту фиксированной верхней панели
    // Находим элемент с классом sticky
    const stickyHeader = document.querySelector('.sticky.top-0');
    // Вычисляем отступ (высота панели + небольшой дополнительный отступ)
    const offset = stickyHeader ? stickyHeader.offsetHeight + 16 : 80;

    // Получаем текущую позицию прокрутки
    const elementPosition = element.getBoundingClientRect().top + window.pageYOffset;

    // Выполняем прокрутку с учетом отступа
    window.scrollTo({
        top: elementPosition - offset,
        behavior: 'smooth'
    });
};

// Инициализация подсказок и других компонентов flowbite
document.addEventListener("DOMContentLoaded", function () {
    // Здесь можно добавить другие инициализации JS-компонентов
    initTooltips();
});

// Инициализация всплывающих подсказок
function initTooltips() {
    const tooltipTriggerList = document.querySelectorAll('[data-tooltip-target]');
    tooltipTriggerList.forEach(function (tooltipTriggerEl) {
        const tooltipId = tooltipTriggerEl.getAttribute('data-tooltip-target');
        const tooltipEl = document.getElementById(tooltipId);

        if (tooltipEl) {
            tooltipTriggerEl.addEventListener('mouseenter', function () {
                tooltipEl.classList.remove('hidden');
            });

            tooltipTriggerEl.addEventListener('mouseleave', function () {
                tooltipEl.classList.add('hidden');
            });
        }
    });
}

// Функция для отображения модальных окон
window.showModal = function (modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.classList.remove('hidden');
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('overflow-hidden');
    }
};

// Функция для скрытия модальных окон
window.hideModal = function (modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.classList.add('hidden');
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('overflow-hidden');
    }
};

// Функция для работы с локальным хранилищем
window.localStorage = {
    setItem: function (key, value) {
        localStorage.setItem(key, value);
    },
    getItem: function (key) {
        return localStorage.getItem(key);
    },
    removeItem: function (key) {
        localStorage.removeItem(key);
    }
};

// Функция для работы с сессионным хранилищем
window.sessionStorage = {
    setItem: function (key, value) {
        sessionStorage.setItem(key, value);
    },
    getItem: function (key) {
        return sessionStorage.getItem(key);
    },
    removeItem: function (key) {
        sessionStorage.removeItem(key);
    }
};