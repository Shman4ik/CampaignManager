# CampaignManager Design System

Tailwind CSS + custom CSS utilities in `wwwroot/css/design-system.css`. Colors defined in `tailwind.config.js`.

## Colors

7 palettes × 11 shades (50–950). Use Tailwind classes: `bg-{palette}-{shade}`, `text-{palette}-{shade}`, `border-{palette}-{shade}`.
CSS variables `--color-{palette}-{shade}` available for primary, secondary, accent.

| Palette | 500 | Usage |
|---------|-----|-------|
| primary | #64748B | Nav, headings, primary buttons |
| secondary | #78716C | Backgrounds, secondary accents |
| accent | #4B7FAF | Highlights, badges |
| success | #2C9D49 | Positive feedback |
| warning | #D97706 | Caution |
| error | #C71D20 | Errors, destructive actions |
| info | #4B7FAF | Info messages (= accent) |

## CSS Variables

```
--cm-background: #F8F9FA          --cm-background-alpha: rgba(248,249,250,0.95)
--cm-text-foreground: #0f172a     --cm-overlay: rgba(0,0,0,0.5)
--cm-border-color: #e2e8f0        --cm-border-color-hover: #cbd5e1
--cm-radius-md: 0.375rem          --cm-shadow-sm: 0 1px 2px 0 rgba(0,0,0,0.05)
--cm-shadow-error: 0 4px 12px rgba(199,29,32,0.25)
--cm-gradient-primary: linear-gradient(135deg, var(--color-primary-700), var(--color-primary-800))
--cm-gradient-error: linear-gradient(135deg, #A8191B, #C71D20)
--font-family-primary: 'Inter', system-ui, sans-serif
--font-family-serif: 'Bookman Old Style', 'Book Antiqua', Georgia, serif
```

## Typography — Heading Classes

h1–h3: serif font (RPG book style). h4–h6: sans-serif. All have responsive sizes at <640px.

| Class | Size | Mobile | Weight | Line-height | Margin-bottom |
|-------|------|--------|--------|-------------|---------------|
| cm-h1 | 3rem | 2.25rem | 700 | 1.2 | 0.5rem |
| cm-h2 | 2.25rem | 1.875rem | 700 | 1.2 | 0.375rem |
| cm-h3 | 1.875rem | 1.5rem | 600 | 1.3 | 0.25rem |
| cm-h4 | 1.5rem | 1.25rem | 600 | 1.3 | 0.25rem |
| cm-h5 | 1.25rem | 1.125rem | 500 | 1.4 | 0.125rem |
| cm-h6 | 1.125rem | 1rem | 500 | 1.4 | 0.125rem |

Usage: `<h1 class="cm-h1">Title</h1>`

## Page Shell

Every routable page has the same three-part shell — the main page (`Components/Pages/Home.razor`)
is the reference:

```razor
<PageTitle>Кампании</PageTitle>

<PageHeader Title="Кампании">
    <Actions>
        <button class="cm-btn cm-btn-primary cm-btn-sm">…</button>
    </Actions>
</PageHeader>

<div class="cm-page">
    …
</div>
```

- `PageHeader` is the 56px sticky topbar. Every page has one, including info pages. Page-level
  actions go in its `Actions` slot — never floating in the body — and are always `cm-btn-sm`
  (the topbar is only 56px tall).
- `cm-page` is the content container: full width, 20px padding. Do **not** use
  `max-w-*` + `mx-auto` here — those are flex items of a column flex container, so `mx-auto`
  disables stretch and collapses sparse pages to their content width.
- Inside, group content with `cm-section` + `cm-section-header` > `cm-section-title`
  (the small uppercase label), and put content in `cm-card` > `cm-card-header` /
  `cm-card-body` / `cm-card-footer`.

| Class | Use |
|-------|-----|
| cm-page | Page content container, directly under `<PageHeader>` |
| cm-section | Vertical group; `cm-section-divided` adds a top rule |
| cm-section-title | Small uppercase group label (inside `cm-section-header`) |
| cm-card-title | Card heading (inside `cm-card-header`) |
| cm-card-count | Count pill after a card title |
| cm-count-badge | Attention count on a button or nav item |
| cm-field-label | Form field label; `cm-field-hint` for the "(optional)" part |
| cm-checkbox | Checkbox |
| cm-text-muted | Secondary body copy inside cards |
| cm-table | Data tables (catalogues, admin lists) |

## Button Semantics

Colour carries meaning; do not pick it for variety.

| Intent | Class |
|--------|-------|
| The page's or dialog's main action (save, submit, create, current page) | `cm-btn-primary` |
| Everything neutral (back, cancel, toggle, secondary nav) | `cm-btn-secondary` |
| Row/card edit action | `cm-btn-outline-primary` |
| Row/card destructive action, reset | `cm-btn-outline-error` |
| Irreversible destructive confirmation inside a dialog | `cm-btn-error` |
| Approving someone else's request | `cm-btn-success` |

`cm-btn-info` (blue) is **not** for ordinary actions — it used to be scattered across submit
buttons and made the same action look different on every page. Blue stays for informational
`Badge`/`Alert`. Row actions always carry a text label, never a bare icon: there is no hover
tooltip on the iPad this app targets.

## CSS Button Classes

Base `cm-btn` (44px, font-weight 600, rounded-lg) + variant. All have hover/active/disabled states.

| Class | Style |
|-------|-------|
| cm-btn-sm | 36px height |
| cm-btn-lg | 52px height |
| cm-btn-primary | primary-700 bg, white text |
| cm-btn-secondary | gray bg, dark text, border |
| cm-btn-success | green bg, white text |
| cm-btn-error | red bg, white text |
| cm-btn-warning | amber bg, white text |
| cm-btn-info | accent-600 bg, white text |
| cm-btn-outline-primary | transparent, primary border |
| cm-btn-outline-error | transparent, error border |

Usage: `<button class="cm-btn cm-btn-primary">Save</button>`

Prefer the `<Button>` Blazor component over raw `cm-btn` classes in new code.

## CSS Form Input

`cm-input` — block, full-width, accent-500 focus ring, disabled gray bg.

Usage: `<input class="cm-input" />`

Модификаторы:

| Класс | Зачем |
|---|---|
| `cm-input-icon-left` | отступ слева под вложенную иконку (лупа в поиске) |
| `cm-field-label-inline` | подпись слева от поля, а не над ним — снимает нижний отступ |
| `cm-field-note` | подсказка под полем: правило из книги со ссылкой на страницу |

**`design-system.css` подключается после тейлвиндовского `styles.css`**, поэтому свойства,
которые `.cm-input` задаёт сам, утилитами не переопределяются: `w-20`, `flex-1` и `pl-8`
на самом поле молча не работают против `width: 100%` и сокращённого `padding`. Ширину
задавайте обёрткой (`<div class="w-20"><input class="cm-input"/></div>`), а новые отступы —
модификатором в дизайн-системе, а не утилитой на странице.

## Shared Blazor Components

All in `Components/Shared/`, globally available.

### Badge
Color label. `<Badge Variant="success" Size="sm">Active</Badge>`
- `Variant` string = "primary" — primary|secondary|accent|success|warning|error|info
- `Size` string = "md" — sm|md|lg
- `Rounded` string = "lg" — md|lg|full
- `WithBorder` bool = true

### Button
Interactive button. `<Button Variant="error" Icon="fa-trash" OnClick="Delete">Remove</Button>`
- `Variant` string = "primary" — primary|secondary|success|error|warning|info|outline-primary|outline-error
- `Size` string = "md" — sm|md|lg
- `Icon` string? — FontAwesome class, e.g. "fa-plus"
- `IsDisabled` bool = false
- `FullWidth` bool = false
- `ButtonType` string = "button" — button|submit|reset
- `AdditionalClasses` string?
- `OnClick` EventCallback

### Alert
Status block. `<Alert Type="warning" Title="Warning">Message</Alert>`
- `Type` string = "info" — success|warning|error|info|primary|secondary|accent
- `Title` string?

### SaveButton
Save with spinner. `<SaveButton IsLoading="@_saving" OnClick="Save" />`
- `IsLoading` bool = false
- `OnClick` EventCallback

### Modal
Dialog container. Scrollable body, header/footer slots.
```
<Modal IsVisible="@_show" OnClose="Close" Title="Edit" MaxWidth="2xl">
    Body content
    <FooterContent><Button Variant="primary" OnClick="Save">Save</Button></FooterContent>
</Modal>
```
- `IsVisible` bool
- `OnClose` EventCallback
- `Title` string = ""
- `MaxWidth` string = "4xl" — sm|md|lg|xl|2xl|3xl|4xl|5xl|6xl|7xl
- `MaxHeight` string? = "70vh"
- `HeaderColorScheme` string = "primary"
- `CloseOnBackdropClick` bool = true
- `FooterContent` RenderFragment?

### ConfirmationModal
Dangerous action confirmation. Optional type-to-confirm.
```
<ConfirmationModal IsVisible="@_show" OnConfirm="Delete" OnCancel="Cancel"
                   IconType="error" RequireConfirmationText="true" ConfirmationTextRequired="DELETE" />
```
- `IsVisible` bool, `OnConfirm` EventCallback, `OnCancel` EventCallback
- `Title` string = "Подтверждение", `Message` string = "Вы уверены?"
- `WarningText` string = "Это действие нельзя отменить."
- `ConfirmText`/`CancelText`/`LoadingText` strings
- `IconType` string = "warning" — warning|error|info|question|success
- `RequireConfirmationText` bool = false, `ConfirmationTextRequired` string = "УДАЛИТЬ"
- `IsLoading` bool = false, `CloseOnBackdropClick` bool = true
- `DetailsContent` RenderFragment?

### Other Components

| Component | Parameters | Purpose |
|-----------|-----------|---------|
| NotificationAlert | Type, Message, OnClose | Dismissable notification |
| EmptyState | Title, Message, IconClass, ActionButton(RF) | No-data placeholder |
| LoadingIndicator | Message | Spinner |
| Pagination | CurrentPage, TotalPages, TotalItems, ItemsPerPage, OnPageChanged | Page nav |
| FilterPanel | Title, IsExpanded, IsExpandedChanged, ActionButtons(RF) | Collapsible filters |
| SortableTableHeader | Title, FieldName, CurrentSortField, SortAscending, OnSortChanged | Sortable column header |
| CustomInput | Label, Value, Type(text/number/checkbox), FullWidth, Disabled, OnValueChanged | Labeled form input |
| InitialSizeTextArea | InitialRows | Auto-expanding textarea (3–15 rows) |
