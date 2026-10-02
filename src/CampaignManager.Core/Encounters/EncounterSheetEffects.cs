using CampaignManager.Core.Characters;

namespace CampaignManager.Core.Encounters;

/// <summary>
/// Эффекты сцены, применённые к листу участника: ПЗ, ПМ, рассудок (с привыканием), МОЩ. Правила — листа: раны —
/// <see cref="WoundRules"/>, рассудок — <see cref="SanityRules.ApplyLoss"/> и <see cref="HabituationRules"/>; здесь только
/// разводка. В v1 итог боя в лист не попадал вовсе: <c>ApplySanityOutcome</c>/<c>RecordHabituation</c> правили
/// оторванную копию (AUDIT, «Сцены»).
/// <para>
/// Эффект — <b>приращение</b> («−5 ПЗ»), а не итоговое значение: если лист между чтением и записью поправили на другом
/// устройстве (409), его перечитывают и применяют эффект ещё раз — поверх чужой правки, без потерь с обеих сторон.
/// </para>
/// </summary>
public static class EncounterSheetEffects
{
    /// <summary>Пишется ли эффект в лист (остальные — только состояние сцены: очередь, выбывание).</summary>
    public static bool TouchesSheet(EncounterEffectKind kind) => kind is
        EncounterEffectKind.Damage or EncounterEffectKind.Heal or EncounterEffectKind.MagicPoints
        or EncounterEffectKind.SanityLoss or EncounterEffectKind.Power;

    /// <summary>Применяет эффекты к листу по порядку. Возвращает строки итога для журнала («ПЗ 12 → 7, серьёзная рана»).</summary>
    public static List<string> Apply(CharacterSheet sheet, SkillCatalog catalog, IEnumerable<EncounterEffect> effects)
    {
        List<string> lines = [];
        foreach (var effect in effects)
        {
            if (Apply(sheet, catalog, effect) is { } line)
                lines.Add(line);
        }

        return lines;
    }

    private static string? Apply(CharacterSheet sheet, SkillCatalog catalog, EncounterEffect effect)
    {
        var current = sheet.Current;
        switch (effect.Kind)
        {
            case EncounterEffectKind.Damage:
            {
                var before = current.HitPoints;
                var major = WoundRules.ApplyDamage(sheet, Math.Max(0, effect.Amount));
                return $"ПЗ {before} → {current.HitPoints}{WoundNote(major, sheet.Condition)}";
            }
            case EncounterEffectKind.Heal:
            {
                var before = current.HitPoints;
                current.HitPoints = Math.Min(DerivedAttributeRules.MaxHitPoints(sheet), current.HitPoints + Math.Max(0, effect.Amount));
                WoundRules.UpdateConsciousness(sheet);
                return $"ПЗ {before} → {current.HitPoints}";
            }
            case EncounterEffectKind.MagicPoints:
            {
                var before = current.MagicPoints;
                current.MagicPoints = Math.Clamp(current.MagicPoints + effect.Amount, 0, DerivedAttributeRules.MaxMagicPoints(sheet));
                return $"ПМ {before} → {current.MagicPoints}";
            }
            case EncounterEffectKind.SanityLoss:
                return LoseSanity(sheet, catalog, effect);
            case EncounterEffectKind.Power:
            {
                var before = sheet.Characteristics.Pow;
                sheet.Characteristics.Pow = Math.Max(0, before + effect.Amount);
                // Максимум ПМ — от МОЩ (стр. 31): трата МОЩ прижимает и текущие ПМ.
                current.MagicPoints = Math.Min(current.MagicPoints, DerivedAttributeRules.MaxMagicPoints(sheet));
                return $"МОЩ {before} → {sheet.Characteristics.Pow}";
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Потеря рассудка одной причиной. От твари — через привыкание к её виду (стр. 167): запись по имени вида заводится,
    /// предел — из её записи потери, за предел вид рассудка уже не отнимает. Пороги (≥5 — проверка ИНТ, ⅕ за день) лист
    /// покажет сам (<see cref="SanityRules.Status"/>); здесь — только подсказка в журнал.
    /// </summary>
    private static string LoseSanity(CharacterSheet sheet, SkillCatalog catalog, EncounterEffect effect)
    {
        var before = sheet.Current.Sanity;
        var amount = Math.Max(0, effect.Amount);
        int lost;
        var habituationNote = "";
        if (!string.IsNullOrWhiteSpace(effect.CreatureName))
        {
            var habituation = HabituationRules.Add(sheet, effect.CreatureName, effect.CreatureId, effect.SanityLossFormula);
            lost = HabituationRules.Lose(sheet, catalog, habituation, amount);
            if (lost < amount)
                habituationNote = $", привыкание к виду «{habituation.CreatureName}»: потеряно {lost} из {amount}";
        }
        else
        {
            lost = SanityRules.ApplyLoss(sheet, catalog, amount);
        }

        var status = SanityRules.Status(sheet, catalog);
        var prompts = new List<string>();
        if (lost > 0 && status.NeedsIntCheck)
            prompts.Add("проверка ИНТ на временное безумие");
        if (lost > 0 && status.IndefiniteLoss && !sheet.Condition.IndefiniteInsanity)
            prompts.Add("⅕ рассудка за день — бессрочное безумие");
        if (status.PermanentlyInsane)
            prompts.Add("рассудок на нуле");

        var promptText = prompts.Count > 0 ? $" ({string.Join("; ", prompts)})" : "";
        return $"Рассудок {before} → {sheet.Current.Sanity}{habituationNote}{promptText}";
    }

    internal static string WoundNote(bool majorWound, SheetCondition condition) =>
        WoundNote(majorWound, condition.Unconscious, condition.Dying);

    internal static string WoundNote(bool majorWound, bool unconscious, bool dying)
    {
        var notes = new List<string>();
        if (majorWound)
            notes.Add("серьёзная рана");
        if (dying)
            notes.Add("при смерти");
        else if (unconscious)
            notes.Add("без сознания");
        return notes.Count > 0 ? ", " + string.Join(", ", notes) : "";
    }
}
