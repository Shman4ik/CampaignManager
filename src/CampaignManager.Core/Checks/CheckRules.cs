using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Checks;

/// <summary>Итог одного броска проверки: что выпало, какой это уровень и пройдена ли сложность.</summary>
public sealed record CheckOutcome(int Roll, SuccessLevel Level, bool Passed);

/// <summary>Попытка: кости (брошенные или вписанные) и итог против цели.</summary>
public sealed record CheckAttempt(D100Roll Dice, CheckOutcome Outcome);

/// <summary>
/// Что диалог проверки просит записать в лист: потраченные пункты Удачи и отметку развития навыка. Сам
/// диалог лист не пишет — изменение применяет владелец документа через <see cref="CheckRules.Apply"/> и
/// сохраняет своим путём (в v1 так же: запись уносило автосохранение листа).
/// </summary>
public sealed record CheckSheetChange
{
    /// <summary>Сколько пунктов Удачи списать (стр. 97); 0 — не тратили.</summary>
    public int LuckCost { get; init; }

    /// <summary>
    /// Навык, которому поставить отметку развития (стр. 92); null — не отмечать. Вместе с тратой Удачи
    /// (<see cref="LuckCost"/> &gt; 0) игнорируется: успех, купленный Удачей, отметки не даёт (стр. 97).
    /// </summary>
    public CheckSubject? Mark { get; init; }

    /// <summary>
    /// Навык, которому <b>этот же</b> диалог уже поставил отметку за этот бросок, а теперь бросок поднимают
    /// Удачей: отметка за него снимается вместе с тратой. Отметку, стоявшую до броска, сюда не кладут.
    /// Без <see cref="LuckCost"/> не значит ничего. Собирает <see cref="CheckRules.LuckSpend"/>.
    /// </summary>
    public CheckSubject? UndoMarkOfThisRoll { get; init; }
}

/// <summary>
/// Проверка навыка или характеристики (гл. 5, стр. 80–97): сложность, бонусные и штрафные кости, повторная
/// проверка, трата Удачи и отметка для фазы развития.
/// <para>
/// Уровень успеха здесь <b>не считается</b> — только <see cref="Check.Evaluate"/>, единственная копия порогов;
/// цена Удачи — только <see cref="LuckRules"/>. Этот класс решает то, чего нет ни там, ни там: что разрешено
/// после броска.
/// </para>
/// </summary>
public static class CheckRules
{
    /// <summary>Больше двух бонусных или штрафных костей книга не даёт (стр. 89).</summary>
    public const int MaxExtraDice = 2;

    /// <summary>Три уровня сложности проверки навыка (стр. 80).</summary>
    public static IReadOnlyList<Difficulty> Difficulties { get; } = [Difficulty.Regular, Difficulty.Hard, Difficulty.Extreme];

    /// <summary>Итог бонусных и штрафных костей одним числом: +2…−2 (одна бонусная гасит одну штрафную).</summary>
    public static int ClampNetDice(int netDice) => Math.Clamp(netDice, -MaxExtraDice, MaxExtraDice);

    /// <summary>Бросок d100 с итогом костей (<paramref name="netDice"/> &gt; 0 — бонусные, &lt; 0 — штрафные).</summary>
    public static D100Roll Roll(IDiceRoller dice, int netDice)
    {
        var net = ClampNetDice(netDice);
        return D100.Roll(dice, Math.Max(0, net), Math.Max(0, -net));
    }

    public static CheckOutcome Evaluate(int roll, int value, Difficulty difficulty)
    {
        var level = Check.Evaluate(roll, value, difficulty);
        return new CheckOutcome(roll, level, Check.Passes(level, difficulty));
    }

    /// <summary>
    /// Почему провал нельзя повторить (стр. 82–84, 87, 102); null — можно. Повторяют только навыки и
    /// характеристики, один раз, не в бою, не Ближний бой и Стрельбу и не после краха.
    /// </summary>
    public static string? PushBlockReason(CheckSubject subject, CheckOutcome first, bool inCombat)
    {
        if (first.Passed)
            return "Проверка пройдена — повторять нечего.";
        if (inCombat)
            return "В бою повторных проверок не бывает: новая попытка — это действие в следующем раунде.";
        if (subject.Kind is CheckSubjectKind.Luck)
            return "Проверку Удачи повторить нельзя.";
        if (IsCombatSkill(subject))
            return "Ближний бой и Стрельбу повторно не проверяют — следующая атака и есть вторая попытка.";
        if (first.Level is SuccessLevel.Fumble)
            return "Крах наступает сразу, повторной проверкой его не отменить.";
        return null;
    }

    /// <summary>
    /// Почему за этот бросок нельзя заплатить Удачей (стр. 97); null — можно. Крах считается с учётом
    /// сложности (<see cref="Check.Evaluate"/>): 97 на трудной проверке навыка 60 — уже крах, хотя
    /// <see cref="LuckRules.CanSpendOn"/> видит только обычную сложность.
    /// <para>
    /// Удачей и пройденную проверку поднимают до уровня выше (трудный успех во встречной проверке, стр. 97);
    /// критический выше некуда — у него <see cref="LuckOptions"/> пуст.
    /// </para>
    /// </summary>
    public static string? LuckBlockReason(CheckSubject subject, CheckOutcome outcome, bool isPushed, int? malfunction = null)
    {
        if (subject.Kind is CheckSubjectKind.Luck)
            return "На проверку Удачи пункты Удачи не тратят.";
        if (isPushed)
            return "На повторную проверку Удачу не тратят: либо повтор, либо Удача.";
        if (outcome.Level is SuccessLevel.Fumble)
            return "Крах вступает в силу в любом случае — выкупить его нельзя.";
        if (IsMalfunction(outcome.Roll, malfunction))
            return "Осечка вступает в силу в любом случае — выкупить её нельзя.";
        return null;
    }

    /// <summary>
    /// Осечка (стр. 117): на проверке оружия выпало не меньше его порога — оно не выстрелило, какой бы ни был уровень успеха.
    /// Удачей её не выкупают (стр. 97), отметки за неё нет. <paramref name="threshold"/> null — не оружие или порога нет.
    /// </summary>
    public static bool IsMalfunction(int roll, int? threshold) => threshold is { } t && roll >= t;

    /// <summary>
    /// Что записать в лист, когда за этот бросок платят Удачей. <paramref name="markedThisRoll"/> — диалог уже
    /// отметил навык за этот же бросок (успех отметили, потом решили поднять уровень): купленный успех отметки
    /// не даёт (стр. 97), и эта отметка снимается. Отметку, стоявшую на листе до броска, трата не трогает.
    /// </summary>
    public static CheckSheetChange LuckSpend(CheckSubject subject, int cost, bool markedThisRoll) => new()
    {
        LuckCost = cost,
        UndoMarkOfThisRoll = markedThisRoll && subject.Kind is CheckSubjectKind.Skill ? subject : null,
    };

    /// <summary>
    /// Во что обойдётся дотянуть провал до нужной сложности и выше. Цены считает <see cref="LuckRules"/>;
    /// уровни ниже сложности отброшены — за них платить бессмысленно.
    /// </summary>
    public static IReadOnlyList<LuckRules.SpendOption> LuckOptions(
        CheckOutcome outcome, int value, Difficulty difficulty, int currentLuck) =>
        LuckRules.Options(outcome.Roll, value, currentLuck)
            .Where(option => option.Level >= Check.Required(difficulty))
            .ToList();

    /// <summary>
    /// Почему успех не даёт отметки для фазы развития (стр. 92, 97); null — даёт. Отмечают только навыки, и
    /// не Мифы Ктулху со Средствами (<see cref="DevelopmentPhaseRules.CanBeChecked(string?)"/>).
    /// </summary>
    public static string? MarkBlockReason(CheckSubject subject, int netDice, bool luckSpent, bool malfunction = false)
    {
        if (subject.Kind is CheckSubjectKind.Characteristic or CheckSubjectKind.Luck)
            return "Отметку для развития ставят только навыкам, не характеристикам и не Удаче.";
        if (subject.Kind is CheckSubjectKind.Skill && !DevelopmentPhaseRules.CanBeChecked(subject.SkillCode))
            return "Мифы Ктулху и Средства никогда не отмечают.";
        if (netDice > 0)
            return "Проверка шла с бонусной костью — навык не отмечают.";
        if (luckSpent)
            return "Успех куплен Удачей — отметки за него нет.";
        if (malfunction)
            return "Осечка — оружие не выстрелило, отметки нет.";
        if (subject.Kind is CheckSubjectKind.Manual)
            return "Значение вписано вручную: если это был навык сыщика, отметку ставят на его листе.";
        return null;
    }

    /// <summary>
    /// Ближний бой и Стрельба (любая специализация) — проверки, которые книга запрещает повторять (стр. 102).
    /// По коду справочника, а не по имени: в v1 «Автомат» проходил как не-огнестрел (F-C09).
    /// </summary>
    public static bool IsCombatSkill(CheckSubject subject) =>
        subject.Kind is CheckSubjectKind.Skill
        && (IsCombatCode(subject.SkillCode) || IsCombatCode(subject.ParentSkillCode));

    /// <summary>
    /// Записывает в лист то, что решили в диалоге: списывает Удачу и ставит отметку развития. Строку навыка
    /// справочника, которого на листе нет, заводит с базой. Возвращает, изменился ли лист.
    /// <para>
    /// Правило «купленный успех отметки не даёт» (стр. 97) — в <see cref="LuckRules.Spend"/>: ей передаётся
    /// только строка, отмеченная за этот же бросок (<see cref="CheckSheetChange.UndoMarkOfThisRoll"/>). Отметка,
    /// стоявшая до броска, — от другого честного успеха, и снимать её не за что. <see cref="CheckSheetChange.Mark"/>
    /// вместе с тратой Удачи не ставится.
    /// </para>
    /// </summary>
    public static bool Apply(CharacterSheet sheet, SkillCatalog catalog, CheckSheetChange change)
    {
        if (change.LuckCost > 0)
        {
            var markOfThisRoll = change.UndoMarkOfThisRoll is { Kind: CheckSubjectKind.Skill } undo
                ? CheckSubjects.FindSkill(sheet, catalog, undo)
                : null;
            return LuckRules.Spend(sheet, change.LuckCost, markOfThisRoll);
        }

        var changed = false;
        if (change.Mark is { Kind: CheckSubjectKind.Skill } subject && DevelopmentPhaseRules.CanBeChecked(subject.SkillCode))
        {
            var row = CheckSubjects.FindSkill(sheet, catalog, subject) ?? AddCatalogRow(sheet, catalog, subject);
            if (row is { Checked: false })
            {
                row.Checked = true;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>Что означает число костей в переключателе.</summary>
    public static string ExtraDiceLabel(int netDice) => ClampNetDice(netDice) switch
    {
        2 => "две бонусные кости",
        1 => "одна бонусная кость",
        -1 => "одна штрафная кость",
        -2 => "две штрафные кости",
        _ => "без дополнительных костей",
    };

    /// <summary>Сложность в подписи кнопки: «Обычная», «Трудная», «Чрезвычайная» (стр. 80).</summary>
    public static string DifficultyLabel(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Hard => "Трудная",
        Difficulty.Extreme => "Чрезвычайная",
        _ => "Обычная",
    };

    private static bool IsCombatCode(string? code) => code is SkillCodes.Fighting or SkillCodes.Firearms
        || (code is not null && SkillCodes.ParentOf(code) is SkillCodes.Fighting or SkillCodes.Firearms);

    private static SheetSkill? AddCatalogRow(CharacterSheet sheet, SkillCatalog catalog, CheckSubject subject)
    {
        if (catalog.Find(subject.SkillId) is not { } definition)
            return null;

        var row = new SheetSkill
        {
            SkillId = definition.Id,
            Value = SkillCatalog.BaseValueOf(definition, sheet.Characteristics),
        };
        sheet.Skills.Add(row);
        return row;
    }
}
