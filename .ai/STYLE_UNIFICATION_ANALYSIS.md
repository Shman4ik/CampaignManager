# CampaignManager Razor Components - Comprehensive Style Unification Analysis

**Generated:** November 17, 2025
**Codebase:** CampaignManager.Web (Blazor Server)
**Total Razor Files Analyzed:** 74

---

## Executive Summary

The CampaignManager codebase uses a well-defined design system (CSS variables in `design-system.css`) but suffers from **significant inconsistencies in how these tokens are applied across components**. The main issues are:

1. **Inconsistent button styling** - Multiple approaches (hardcoded Tailwind, shared Button component, inline styles)
2. **Duplicated badge/label patterns** - Same layout repeated with slight variations across 15+ files
3. **Mixed spacing conventions** - Uses multiple spacing patterns (gap-2, space-y-1, etc.) without clear hierarchy
4. **Inconsistent shadow usage** - Hardcoded shadows vs. CSS variables
5. **Color palette drift** - Using hardcoded Tailwind colors (blue-600, green-500) instead of design system colors
6. **Modal and dialog variations** - Two modal components with different styling approaches

---

## Part 1: Complete Razor File Inventory

### A. Shared Components (/Components/Shared/)

**Base UI Components (11 files):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/Button.razor` - Primary button component
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/ColorButton.razor` - Alternative button component
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/Alert.razor` - Alert component with 7 variants
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/StatusBadge.razor` - Badge component with 7 variants
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/Modal.razor` - Modal dialog (gradient header)
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/ConfirmationModal.razor` - Confirmation dialog (icon-based header)
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/SaveButton.razor` - Specialized save button
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/NotificationAlert.razor` - Notification toast
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/LoadingIndicator.razor` - Spinner component
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/Pagination.razor` - Pagination controls
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/FilterPanel.razor` - Collapsible filter panel

**Form Components (3 files):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/CustomInput.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/InitialSizeTextArea.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/AutocompleteMinioImage.razor`

**Utility Components (3 files):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/EmptyState.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Shared/SortableTableHeader.razor`

### B. Layout Components (/Components/Layout/)

- `/home/user/CampaignManager/CampaignManager.Web/Components/Layout/MainLayout.razor` - Primary layout wrapper
- `/home/user/CampaignManager/CampaignManager.Web/Components/Navigation/TopBar.razor` - Top navigation bar
- `/home/user/CampaignManager/CampaignManager.Web/Components/App.razor` - Root Blazor component
- `/home/user/CampaignManager/CampaignManager.Web/Components/Routes.razor` - Route definitions

### C. Feature Components by Domain

**Campaigns (4 components):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Campaigns/Pages/CreateCampaignPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Campaigns/Components/UserCampaignsComponent.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Campaigns/Components/JoinCampaignComponent.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Campaigns/Components/KeeperCharactersComponent.razor`

**Characters (11 components):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Pages/CharacterPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/PersonalInfoComponent.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/AttributeRowsTable.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/DerivedAttributesTable.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/CharacterStateTable.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/SkillGroupCard.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/SkillGroupColumn.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/WeaponComponent.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/SpellComponent.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/EquipmentComponent.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/BiographyComponent.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/FinancesComponent.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Characters/Components/CharacterStatusChanger.razor`

**Weapons (5 components):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Weapons/Pages/WeaponsPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Weapons/Components/WeaponsListView.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Weapons/Components/WeaponTableRow.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Weapons/Components/WeaponsFilterControls.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Weapons/Components/WeaponFormFields.razor`

**Spells (5 components):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Spells/Pages/SpellsPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Spells/Components/SpellsListView.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Spells/Components/SpellTableRow.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Spells/Components/SpellsFilterControls.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Spells/Components/SpellFormFields.razor`

**Skills (3 components):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Skills/Pages/SkillsPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Skills/Pages/SkillEditPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Skills/Components/SkillCard.razor`

**Items (1 component):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Items/Pages/ItemsPage.razor`

**Bestiary/Creatures (3 components):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Bestiary/Pages/CreaturesPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Bestiary/Pages/CreatureEditPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Bestiary/Components/CreatureCard.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Bestiary/Components/CharacteristicItem.razor`

**NPCs (3 components):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/NPC/Pages/NpcListPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/NPC/Components/CharacterTemplateCard.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/NPC/Components/LinkToScenarioDialog.razor`

**Scenarios (6 components):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Scenarios/Pages/ScenariosPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Scenarios/Pages/ScenarioDetailPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Scenarios/Pages/ScenarioEditPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Scenarios/Components/AddNpcModal.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Scenarios/Components/AddCreatureModal.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Scenarios/Components/AddItemModal.razor`

**Combat (6 components):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Combat/Pages/CombatPage.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Combat/Components/SetupCombat.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Combat/Components/InitiativeTracker.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Combat/Components/CombatParticipantCard.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Combat/Components/CombatLog.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Combat/Components/ActionResultModal.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Features/Combat/Components/ActionSelector.razor`

**Pages (2 components):**
- `/home/user/CampaignManager/CampaignManager.Web/Components/Pages/Home.razor`
- `/home/user/CampaignManager/CampaignManager.Web/Components/Pages/DesignSystem.razor`

---

## Part 2: Design System Foundation

### Available Design Tokens (from design-system.css)

**Color Palette (7 semantic color systems):**
- Primary: Teal/cyan (500 = #1A9697)
- Secondary: Parchment/tan (500 = #C3A06C)
- Accent: Purple/cosmic (500 = #8F3BC7)
- Success: Green (500 = #2C9D49)
- Warning: Orange (500 = #D97706)
- Error: Red (500 = #C71D20)
- Info: Blue (500 = #2A6BC9)

Each color has 10 shades (50-950) available.

**Typography System:**
- Font families: Inter (primary), serif (headings), mono (code)
- Size scale: xs, sm, base, lg, xl, 2xl, 3xl, 4xl, 5xl
- Weight scale: thin (100) to black (900)
- Line heights and letter spacing variables

**Spacing System:**
- Defined variables: --cm-spacing-1 through --cm-spacing-8
- 0.25rem, 0.5rem, 0.75rem, 1rem, 1.5rem, 2rem

**Border Radius:**
- sm: 0.125rem
- md: 0.375rem
- lg: 0.5rem
- xl: 0.75rem
- full: 9999px

**Shadows:**
- sm, md, lg, error (specific)

---

## Part 3: Common Styling Patterns Identified

### Pattern 1: Badge/Label Component (MOST DUPLICATED)

**Frequency:** Found in 15+ components with variations
**Example from WeaponTableRow.razor (lines 9-11):**
```razor
<span class="inline-flex items-center px-3 py-1.5 rounded-lg text-sm font-semibold bg-primary-100 text-primary-800 border border-primary-200">
    @Weapon.Type.ToRussianString()
</span>
```

**Variations found:**
1. `px-3 py-1.5 rounded-lg` (WeaponTableRow, SpellTableRow, multiple files)
2. `px-2.5 py-1 rounded-md` (WeaponsListView, SpellsListView, ItemsPage)
3. `px-2 py-0.5` (ItemsPage, smaller variant)
4. `px-3 py-2` (CreatureEditPage, larger variant)

**Files with this pattern:**
- WeaponTableRow.razor (4 instances, lines 9, 19, 25, 123, 129)
- SpellTableRow.razor (2 instances, lines 20, 78)
- WeaponsListView.razor (3 instances)
- SpellsListView.razor (3 instances)
- ItemsPage.razor (2 instances)
- SkillCard.razor (2 instances, lines 30, 38)
- CreatureCard.razor (0 - uses different pattern)

**Recommendation:** Extract as `<Badge>` component with `Variant` and `Size` parameters.

### Pattern 2: Button Styling (INCONSISTENT APPROACHES)

**Approach 1 - Shared Button Component:**
```razor
<Button Variant="primary" Size="md" @onclick="HandleSave">Save</Button>
```
**Usage:** Not heavily used despite being available

**Approach 2 - ColorButton Component:**
```razor
<ColorButton Color="primary" Outline="false" @onclick="Edit">Edit</ColorButton>
```
**Usage:** Limited use

**Approach 3 - Hardcoded Tailwind (MOST COMMON):**
```razor
<!-- Example from UserCampaignsComponent.razor -->
<button @onclick="() => EditCharacter(userCharacter.Id)"
        class="w-full bg-blue-500 hover:bg-blue-600 text-white font-bold py-2 px-4 rounded transition-colors">
    Редактировать
</button>
```

**Approach 4 - SaveButton Component:**
```razor
<SaveButton IsLoading="@IsLoading" @onclick="Save">Save</SaveButton>
```
**Usage:** Single dedicated button

**Issues:**
- 136+ instances of hardcoded Tailwind button styles
- Uses hardcoded `blue-600`, `green-500`, `red-600` instead of design system colors
- Inconsistent padding: `py-2 px-4`, `py-1 px-3`, `py-2.5 px-4`
- Some use `rounded`, some `rounded-lg`, some `rounded-md`
- Disabled states vary

**Example inconsistency from ItemsPage.razor (line 27):**
```razor
class="text-blue-600 hover:text-blue-800 py-1 px-2 rounded-md border border-blue-300"
```
vs. WeaponTableRow.razor (line 53):
```razor
class="min-h-[44px] px-4 py-2 text-primary-600 hover:text-primary-800 bg-primary-50 hover:bg-primary-100 rounded-lg text-sm font-semibold flex items-center justify-center gap-2 transition-all duration-200 border border-primary-200"
```

### Pattern 3: Card/Container Layout

**Pattern Found:**
```razor
<div class="bg-white shadow-md rounded-lg overflow-hidden hover:shadow-lg transition-shadow duration-300">
    <div class="p-6">
        <!-- Content -->
    </div>
</div>
```

**Used in:**
- SkillCard.razor (line 5)
- CreatureCard.razor (line 5)
- UserCampaignsComponent.razor (line 32, variations)

**Inconsistencies:**
- Shadow: `shadow-md` vs `shadow-lg` vs `cm-shadow-sm`
- Padding: `p-6` vs `p-4` vs `p-8`
- Border radius: `rounded-lg` vs `rounded-xl` vs `rounded`
- Transitions: Most have `transition-shadow`, some have `transition-all`

### Pattern 4: Form Input Styling

**Common pattern:**
```html
<input class="px-3 py-2 border border-gray-300 rounded text-sm focus:outline-none focus:ring-1 focus:ring-primary-500" />
```

**Found in:**
- ItemsPage.razor (line 50)
- Various form components

**Issues:**
- No unified component for text inputs
- Focus ring inconsistent: some use `ring-1`, some use `ring-2`
- Border radius: mix of `rounded`, `rounded-md`, `rounded-lg`

### Pattern 5: Spacing Inconsistency

**Gap usage distribution (from grep analysis):**
- `gap-2`: 53 occurrences (most common)
- `space-y-1`: 24 occurrences
- `space-y-2`: 23 occurrences
- `gap-3`: 14 occurrences
- `space-y-3`: 10 occurrences
- `gap-4`, `space-y-4`: 8 each
- Others: `gap-6`, `gap-1`, `gap-1.5`, `space-y-6`, `space-y-1.5`

**Issue:** No clear hierarchy or convention. Should standardize to:
- Small: gap-2 or space-y-2
- Medium: gap-3 or space-y-3
- Large: gap-4 or space-y-4

### Pattern 6: Table Row Styling

**WeaponTableRow.razor (lines 5-6):**
```razor
<tr class="@(IsExpanded ? "bg-primary-50" : "hover:bg-gray-50") transition-all duration-200 cursor-pointer active:bg-gray-100">
```

**SpellTableRow.razor (lines 4-5):**
```razor
<tr class="@(IsExpanded ? "bg-primary-50" : "hover:bg-gray-50") transition-all duration-200 cursor-pointer active:bg-gray-100">
```

**Consistency:** Good pattern, but used only in 2 table row components.

### Pattern 7: Modal Styling (TWO VARIANTS)

**Modal.razor:**
- Uses gradient header: `bg-gradient-to-r from-{color}-500 to-{color}-600`
- Max width: `max-w-{size}` (dynamic class string)
- Fixed height: `max-h-[70vh]`

**ConfirmationModal.razor:**
- Uses icon-based header with background circle
- Better accessibility (ARIA labels)
- Confirmation text input validation
- More modern styling

**Issue:** Two different modal implementations create cognitive load. ConfirmationModal is superior.

---

## Part 4: Specific Inconsistencies with Examples

### Inconsistency 1: Button Color Naming

**Design System uses:** primary, secondary, accent, success, warning, error, info
**Components use:** primary, red, blue, green, orange, purple

Example from Modal.razor (line 48):
```csharp
"primary" => "bg-gradient-to-r from-primary-500 to-primary-600",
```

vs. UserCampaignsComponent.razor (line 41):
```razor
class="w-full bg-green-500 hover:bg-green-600 text-white font-bold py-2 px-4 rounded transition-colors"
```

**Recommendation:** Use design system color names consistently.

### Inconsistency 2: Border Radius Values

**Found variations:**
- `rounded` (Tailwind default = 0.25rem)
- `rounded-sm` (0.125rem)
- `rounded-md` (0.375rem)
- `rounded-lg` (0.5rem)
- `rounded-xl` (0.75rem)
- `rounded-2xl` (1rem)
- `rounded-full` (9999px)

**Pattern analysis:**
- Most cards use `rounded-lg` (good consistency)
- Buttons vary: `rounded`, `rounded-md`, `rounded-lg`
- Badges: `rounded-md`, `rounded-lg`, `rounded-full`

**Recommendation:** Adopt single standard:
- Buttons: `rounded-lg`
- Form inputs: `rounded-md`
- Badges: `rounded-lg`
- Cards: `rounded-xl`

### Inconsistency 3: Padding Inconsistency in Buttons

**Examples:**
```
py-2 px-4          (ItemsPage.razor, SkillCard.razor)
py-2 px-3          (Various)
py-1.5 px-3        (WeaponTableRow, SpellTableRow)
py-1 px-2          (ItemsPage.razor filters)
py-2.5 px-4        (ConfirmationModal.razor)
```

**Recommendation:** Create reusable button size classes:
- Small: px-3 py-1.5 text-xs
- Medium: px-4 py-2 text-sm
- Large: px-6 py-2.5 text-base

### Inconsistency 4: Shadow Depth Usage

**Hardcoded shadows:**
- `shadow-sm` (1 match)
- `shadow-md` (most common in cards)
- `shadow-lg` (used in hover states)
- `shadow-2xl` (ConfirmationModal)
- `cm-shadow-sm`, `cm-shadow-lg` (design system)
- `shadow-inner` (detail containers)
- `shadow-xl` (rare)

**Issue:** Both design system variables AND hardcoded Tailwind used.

### Inconsistency 5: Icon Button Sizing

Different approaches across components:

From FilterPanel.razor (line 8):
```razor
class="min-w-[44px] min-h-[44px] flex items-center justify-center"
```

From Pagination.razor (line 12):
```razor
class="min-w-[44px] min-h-[44px] px-3 py-2 text-sm sm:text-base rounded-lg"
```

From ConfirmationModal.razor (line 32):
```razor
class="w-10 h-10 flex items-center justify-center"
```

**Issue:** Three different approaches to create icon buttons (44px standard, 40px variant, no standard).

### Inconsistency 6: Color Saturation in Focus States

Some components:
```razor
focus:ring-2 focus:ring-gray-300        (ConfirmationModal)
focus:ring-1 focus:ring-primary-500     (ItemsPage)
focus:ring-2 focus:ring-offset-2 focus:ring-blue-500  (CreatureEditPage)
```

**Missing:** Unified focus ring specification.

---

## Part 5: Repeated Patterns for Extraction

### High Priority - Highly Duplicated Patterns

**1. Badge Component**
- Current: Inline Tailwind classes in 15+ locations
- Recommendation: Create unified `<Badge>` component
- Parameters: `Variant` (primary, secondary, accent, success, warning, error, info), `Size` (sm, md, lg), `Outline`

**2. Edit/Delete Button Pair**
Pattern found in WeaponTableRow, SpellTableRow, SkillCard, CreatureCard:
```razor
<button class="...text-primary-600 bg-primary-50 hover:bg-primary-100...">
    <i class="fas fa-edit"></i>
    <span>Изменить</span>
</button>
<button class="...text-error-600 bg-error-50 hover:bg-error-100...">
    <i class="fas fa-trash-alt"></i>
    <span>Удалить</span>
</button>
```

Recommendation: Create `<ActionButtons>` component with `OnEdit` and `OnDelete` callbacks.

**3. Card Header with Title and Actions**
Pattern in multiple components:
```razor
<div class="flex justify-between items-start mb-2">
    <h2 class="text-xl font-semibold">@Title</h2>
    <div class="flex space-x-1">
        <!-- Edit/Delete buttons -->
    </div>
</div>
```

Recommendation: Create `<CardHeader>` component.

**4. Table Detail Row Expandable Pattern**
Used in WeaponTableRow and SpellTableRow:
```razor
@if (IsExpanded)
{
    <tr class="bg-gradient-to-r from-gray-50 to-white border-t-2 border-primary-200">
        <td colspan="..." class="px-4 sm:px-6 py-6">
            <div class="bg-white rounded-xl p-6 shadow-inner border-2 border-gray-100">
```

Recommendation: Create `<ExpandableTableRow>` component.

**5. Empty State Component**
Currently one component, but usage varies. Should standardize.

### Medium Priority - Common Patterns

**6. Form Field with Label**
Pattern in various form inputs - needs unified styling.

**7. Info Display Grid**
Pattern from WeaponTableRow expanded view (lines 72-135):
```razor
<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
    <div class="space-y-1">
        <h5 class="text-sm font-semibold text-gray-500 uppercase tracking-wide">Label</h5>
        <p class="text-base font-medium text-gray-900">Value</p>
    </div>
</div>
```

Recommendation: Create `<InfoGrid>` component with `<InfoField>` child component.

**8. Sticky Filter Panel**
Used in multiple pages - extract as component with configurable content.

---

## Part 6: Component Reusability Analysis

### Currently Reusable Components (Good)
- Button.razor - Well-parameterized (6+ parameters)
- StatusBadge.razor - Good variant system
- Alert.razor - Good variant system
- Modal.razor - Good but MaxWidth class string generation is risky
- ConfirmationModal.razor - Excellent, feature-rich
- Pagination.razor - Specialized, well-designed
- FilterPanel.razor - Good, reusable

### Underutilized Components
- ColorButton.razor - Has good API but rarely used (prefer inline Tailwind)
- SaveButton.razor - Too specialized, only Russian text hardcoded
- CustomInput.razor - May have focus state issues not addressed

### Missing Components (Should Create)
1. **Badge** - Replace 15+ instances of inline badge markup
2. **ActionButtons** - Standardize edit/delete button pairs
3. **CardHeader** - Standardize card title + actions
4. **ExpandableTableRow** - Wrap table expansion pattern
5. **FormField** - Wrap input + label pattern
6. **InfoGrid / InfoField** - Display structured information
7. **StickyFilterPanel** - Extend FilterPanel for consistency
8. **Text** - Wrap typography (heading, body, caption)

---

## Part 7: Layout Pattern Analysis

### Page Structure Pattern (Consistent)

Most pages follow:
```razor
<div class="max-w-7xl mx-auto px-4">
    <!-- Sticky filter/header -->
    <div class="sticky top-0 z-10 bg-white rounded-lg shadow-md p-4 mb-4">
        <!-- Filter controls and action buttons -->
    </div>
    
    <!-- Content -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <!-- Cards or table rows -->
    </div>
</div>
```

**Observation:** Good consistency in page-level structure. Issue is within component styling.

### Responsive Design

**Breakpoints used:**
- `sm:` (640px)
- `md:` (768px)
- `lg:` (1024px)
- `xl:` (1280px)

**Pattern consistency:** Good - most components use responsive classes appropriately.

---

## Part 8: Design System Token Usage

### Current Usage of CSS Variables

**Well-used:**
- Color palette: `primary-{shade}`, `secondary-{shade}`, etc. (good)
- Typography: `cm-h1` through `cm-h6` (good)
- Shadows: Some use of `cm-shadow-sm`, `cm-shadow-lg`

**Under-utilized:**
- Spacing variables (--cm-spacing-1 through 8) - almost never used
- Border radius variables (--cm-radius-*) - never used, only Tailwind classes
- Focus ring system - inconsistent

**Recommendation:** Adopt CSS variables for:
```css
.btn-primary { /* uses --cm-spacing-4, --cm-radius-lg, etc */ }
.card { /* uses --cm-shadow-md, --cm-radius-xl */ }
```

---

## Part 9: Accessibility Observations

**Good practices found:**
- ConfirmationModal.razor - Excellent ARIA labels and keyboard support
- Button.razor - Uses semantic `<button>` with proper disabled handling
- Icons - Using Font Awesome with fallback text

**Issues:**
- Many icon-only buttons lack `aria-label` or `title`
- Focus rings inconsistent - some Tailwind, some none
- Modal backdrops use role="dialog" inconsistently

---

## Summary Recommendations for Style Unification

### Priority 1 - Immediate (High Impact)

1. **Replace all hardcoded button colors with Button/ColorButton component**
   - Search for `bg-green-`, `bg-blue-`, `bg-red-` patterns
   - Migrate to design system colors
   - Impact: Cleanup 136+ button instances

2. **Extract Badge component**
   - Create unified badge with all variants
   - Replace 15+ instances of inline badge markup
   - Impact: Eliminate most duplicated styling code

3. **Standardize modal usage**
   - Deprecate Modal.razor in favor of ConfirmationModal.razor
   - Update all modal consumers
   - Impact: Better UX, improved accessibility

### Priority 2 - High Value (Medium Effort)

4. **Unify button sizing**
   - Create standard sizes: sm, md, lg
   - Apply consistently across all buttons
   - Document in design system

5. **Extract CardHeader component**
   - Replace pattern in 8+ components
   - Ensure consistent styling

6. **Standardize border radius**
   - Create CSS variables for each radius size
   - Replace inline Tailwind classes
   - Enforce via component props

### Priority 3 - Quality Improvements

7. **Create Text/Typography components**
   - Wrap heading levels
   - Ensure consistent sizing and spacing

8. **Unify form input styling**
   - Create FormInput component
   - Standardize focus states

9. **Create comprehensive component library page**
   - Document all shared components
   - Show usage examples
   - Enforce best practices

### Process Recommendations

- Use ESLint rule to warn on hardcoded Tailwind color values in wrong contexts
- Create Razor component template for new features
- Add style review to code review checklist
- Maintain component.md documentation

---

## Appendix: File Statistics

| Category | Count | Avg Lines |
|----------|-------|-----------|
| Shared Components | 14 | 45 |
| Layout Components | 4 | 35 |
| Feature Pages | 8 | 150+ |
| Feature Components | 48 | 80 |
| **Total** | **74** | - |

**CSS Files:**
- design-system.css: ~430 lines (comprehensive)
- markdown-styles.css: Utility styles
- No component-scoped styles found (all Tailwind)

**Total lines analyzed:** ~10,000+ lines of Razor markup
