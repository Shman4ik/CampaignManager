# CampaignManager Design System для Зова Ктулху

## Руководство по цветовой палитре

Данный документ описывает стандартизированную цветовую палитру для приложения CampaignManager с тематикой "Зов Ктулху", обеспечивая руководство для создания консистентного дизайна для всех компонентов.

### Основные цвета (Primary)

Наш основной цвет представляет собой глубокий сине-зеленый оттенок (#1A9697), напоминающий бездонные морские глубины – владения Ктулху. Этот цвет служит основой идентичности приложения.

### Вторичные цвета (Secondary)

Вторичные цвета (#C3A06C) имитируют тона выцветшего пергамента и старинных манускриптов – идеально для текстур и фоновых элементов, не требующих акцентирования внимания.

### Акцентные цвета (Accent)

Акцентные цвета (#8F3BC7) – это мистические фиолетовые оттенки, символизирующие сверхъестественную космическую энергию и элдритч-магию. Используются для выделения важных элементов и призыва внимания пользователя.

### Статусные цвета

- **Success**: #2C9D49 - Мистический зеленый цвет для положительных результатов, завершений и одобрений
- **Warning**: #D97706 - Цвет древних фолиантов для предупреждений, предостережений или потенциальных проблем
- **Error**: #C71D20 - Кроваво-красный цвет ритуальных жертвоприношений для ошибок или критических проблем
- **Info**: #2A6BC9 - Тайный синий цвет для информационных сообщений и нейтральных состояний

## Как использовать дизайн-систему

### Классы Tailwind

Дизайн-система реализована с использованием Tailwind CSS с настраиваемой цветовой палитрой. Вы можете использовать эти классы цветов непосредственно в ваших компонентах Razor:

```html
<!-- Цвета фона -->
<div class="bg-primary-500">Основной фон</div>
<div class="bg-secondary-500">Вторичный фон</div>
<div class="bg-accent-500">Акцентный фон</div>
<div class="bg-success-500">Успешный фон</div>
<div class="bg-warning-500">Предупреждающий фон</div>
<div class="bg-error-500">Фон ошибки</div>
<div class="bg-info-500">Информационный фон</div>

<!-- Цвета текста -->
<p class="text-primary-500">Основной текст</p>
<p class="text-secondary-500">Вторичный текст</p>
<p class="text-accent-500">Акцентный текст</p>
<p class="text-success-500">Текст успеха</p>
<p class="text-warning-500">Предупреждающий текст</p>
<p class="text-error-500">Текст ошибки</p>
<p class="text-info-500">Информационный текст</p>

<!-- Цвета границ -->
<div class="border border-primary-500">Основная граница</div>
<div class="border border-secondary-500">Вторичная граница</div>
<div class="border border-accent-500">Акцентная граница</div>
<div class="border border-success-500">Успешная граница</div>
<div class="border border-warning-500">Предупреждающая граница</div>
<div class="border border-error-500">Граница ошибки</div>
<div class="border border-info-500">Информационная граница</div>
```

Каждый цвет имеет 11 оттенков, от 50 (самый светлый) до 950 (самый темный), с 500 в качестве базового цвета.

### CSS-переменные

Для более сложного стилизования или когда классы Tailwind недостаточны, вы можете использовать CSS-переменные:

```css
.my-custom-element {
  background-color: var(--color-primary-500);
  color: var(--color-secondary-700);
  border: 1px solid var(--color-accent-300);
}
```

### Компоненты-утилиты

Мы создали несколько компонентов-утилит, чтобы упростить использование дизайн-системы:

#### StatusBadge

```html
<StatusBadge Status="success">Успех</StatusBadge>
<StatusBadge Status="warning">Предупреждение</StatusBadge>
<StatusBadge Status="error">Ошибка</StatusBadge>
<StatusBadge Status="info">Информация</StatusBadge>
<StatusBadge Status="primary">Основной</StatusBadge>
<StatusBadge Status="secondary">Вторичный</StatusBadge>
<StatusBadge Status="accent">Акцентный</StatusBadge>

<!-- С обводкой -->
<StatusBadge Status="success" Outline="true">Успех с обводкой</StatusBadge>

<!-- Маленький размер -->
<StatusBadge Status="success" Small="true">Маленький успех</StatusBadge>
```

#### ColorButton

```html
<ColorButton Color="primary">Основная кнопка</ColorButton>
<ColorButton Color="secondary">Вторичная кнопка</ColorButton>
<ColorButton Color="accent">Акцентная кнопка</ColorButton>
<ColorButton Color="success">Кнопка успеха</ColorButton>
<ColorButton Color="warning">Предупреждающая кнопка</ColorButton>
<ColorButton Color="error">Кнопка ошибки</ColorButton>
<ColorButton Color="info">Информационная кнопка</ColorButton>

<!-- С обводкой -->
<ColorButton Color="primary" Outline="true">Кнопка с обводкой</ColorButton>

<!-- Маленький размер -->
<ColorButton Color="primary" Small="true">Маленькая кнопка</ColorButton>

<!-- Отключенное состояние -->
<ColorButton Color="primary" Disabled="true">Отключенная кнопка</ColorButton>
```

#### Alert

```html
<Alert Type="success" Title="Успех">Эта операция была успешной.</Alert>
<Alert Type="warning" Title="Предупреждение">Будьте осторожны с этим действием.</Alert>
<Alert Type="error" Title="Ошибка">Что-то пошло не так.</Alert>
<Alert Type="info" Title="Информация">Вот полезная информация.</Alert>
<Alert Type="primary">Основное примечание.</Alert>
<Alert Type="secondary">Вторичное примечание.</Alert>
<Alert Type="accent">Акцентное примечание.</Alert>
```

## Удаление жестко закодированных цветов

При рефакторинге существующих компонентов замените любые жестко закодированные значения цветов на соответствующие альтернативы из дизайн-системы:

### До

```html
<button class="bg-blue-500 hover:bg-blue-600 text-white rounded-md p-2">
  Нажми меня
</button>
```

### После

```html
<button class="bg-primary-500 hover:bg-primary-600 text-white rounded-md p-2">
  Нажми меня
</button>
```

Или еще лучше, используйте компонент-утилиту:

```html
<ColorButton Color="primary">Нажми меня</ColorButton>
```

## Страница дизайн-системы

Вы можете просмотреть все доступные цвета и компоненты на странице дизайн-системы по адресу `/design-system`. Используйте эту страницу в качестве справочника при реализации новых элементов UI.
