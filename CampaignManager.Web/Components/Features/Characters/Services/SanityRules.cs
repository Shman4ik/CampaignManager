using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Правила Рассудка из главы 8 ("Зов Ктулху" 7e).
/// </summary>
public static class SanityRules
{
    public const int AbsoluteMaxSanity = 99;
    private const int MaxMythos = 99;
    private const string MythosSkillName = "Мифы Ктулху";

    /// <summary>
    ///     Максимум Рассудка = 99 − значение навыка "Мифы Ктулху" (стр. 63 и 152).
    /// </summary>
    public static int ComputeMaxSanity(Character character)
    {
        var mythos = GetMythosValue(character);
        return Math.Max(0, AbsoluteMaxSanity - mythos);
    }

    public static int GetMythosValue(Character character) => FindMythosSkill(character)?.Value.Regular ?? 0;

    /// <summary>
    ///     Есть ли на листе навык "Мифы Ктулху": без него прирост Мифов просто некуда записать.
    /// </summary>
    public static bool HasMythosSkill(Character character) => FindMythosSkill(character) is not null;

    /// <summary>
    ///     Прибавляет к навыку "Мифы Ктулху" (не выше 99) и пересчитывает максимум Рассудка:
    ///     99 − Мифы (стр. 152). Текущий Рассудок прижимается к новому максимуму. Возвращает
    ///     фактическую прибавку; ноль — если навыка на листе нет.
    /// </summary>
    public static int AddMythos(Character character, int gain)
    {
        var mythos = FindMythosSkill(character);
        if (mythos is null || gain <= 0)
            return 0;

        var before = mythos.Value.Regular;
        mythos.Value.Regular = Math.Min(MaxMythos, before + gain);
        mythos.Value.UpdateDerived();

        // Новый максимум может оказаться ниже текущего Рассудка — прижимаем.
        var max = ComputeMaxSanity(character);
        character.DerivedAttributes.Sanity.MaxValue = max;
        character.DerivedAttributes.Sanity.Value = Math.Min(character.DerivedAttributes.Sanity.Value, max);

        return mythos.Value.Regular - before;
    }

    /// <summary>
    ///     Порог для проверки на бессрочное безумие — 1/5 от текущего Рассудка, потерянные
    ///     за один игровой день (стр. 153).
    /// </summary>
    public static int IndefiniteInsanityThreshold(int currentSanity) => currentSanity / 5;

    /// <summary>
    ///     Триггер на проверку ИНТ → временное безумие: 5 и более пунктов, потерянных
    ///     по одной и той же причине (стр. 152).
    /// </summary>
    public const int TemporaryInsanityThreshold = 5;

    /// <summary>
    ///     Рассудок упал до нуля — сыщик неизлечимо безумен и выбывает из игры (стр. 153).
    ///     Отдельного флага не держим: это ровно "Рассудок = 0".
    /// </summary>
    public static bool IsPermanentlyInsane(Character character) =>
        character.DerivedAttributes.Sanity.Value <= 0;

    /// <summary>
    ///     Сыщик временно или бессрочно безумен. После приступа это «затаённое безумие»: игрок
    ///     снова управляет сыщиком, но рассудок уязвим — любая потеря вызывает новый приступ (стр. 156).
    /// </summary>
    public static bool IsInsane(Character character) =>
        character.State.HasTemporaryInsanity || character.State.HasIndefiniteInsanity;

    /// <summary>
    ///     Списывает рассудок как одну причину потери. Считает оба окна порогов (последняя причина
    ///     и игровой день) и, если сыщик уже безумен, помечает, что положен новый приступ:
    ///     в затаённом безумии его вызывает даже один пункт (стр. 156).
    /// </summary>
    /// <returns>Сколько на самом деле списано — ниже нуля Рассудок не падает.</returns>
    public static int ApplyLoss(Character character, int amount)
    {
        var max = ComputeMaxSanity(character);
        var current = Math.Clamp(character.DerivedAttributes.Sanity.Value, 0, max);
        var actualLoss = Math.Min(current, Math.Max(0, amount));

        character.DerivedAttributes.Sanity.Value = current - actualLoss;
        character.State.SanityLossEpisode += actualLoss;
        character.State.LastSanityLoss = actualLoss;

        if (actualLoss > 0 && IsInsane(character))
            character.State.InsanityBoutDue = true;

        return actualLoss;
    }

    /// <summary>
    ///     Отметка временного безумия (успешная проверка ИНТ после потери ≥5 от одной причины,
    ///     стр. 153). Любое безумие начинается с приступа (стр. 154), поэтому отметка сразу
    ///     делает его положенным. Повторная отметка начинает отсчёт 1d10 часов заново.
    /// </summary>
    public static void SetTemporaryInsanity(Character character, bool insane)
    {
        character.State.HasTemporaryInsanity = insane;
        character.State.TemporaryInsanityStartedAt = insane ? DateTime.UtcNow : null;
        UpdateBoutDueOnMark(character, insane);
    }

    /// <summary>Отметка бессрочного безумия (≥1/5 текущего Рассудка за игровой день, стр. 153).</summary>
    public static void SetIndefiniteInsanity(Character character, bool insane)
    {
        character.State.HasIndefiniteInsanity = insane;
        character.State.IndefiniteInsanityStartedAt = insane ? DateTime.UtcNow : null;
        UpdateBoutDueOnMark(character, insane);
    }

    /// <summary>Записывает разыгранный приступ: он больше не положен, а на листе виден итог.</summary>
    public static void RecordBout(Character character, InsanityBoutMode mode, int roll, int? duration)
    {
        character.State.LastInsanityBout = new InsanityBout
        {
            Mode = mode,
            Roll = roll,
            Duration = duration
        };
        character.State.InsanityBoutDue = false;
    }

    /// <summary>Хранитель решил обойтись без приступа (разыграл на словах) — напоминание снимается.</summary>
    public static void DismissBout(Character character) => character.State.InsanityBoutDue = false;

    private static void UpdateBoutDueOnMark(Character character, bool insane)
    {
        if (insane)
            character.State.InsanityBoutDue = true;
        else if (!IsInsane(character))
            character.State.InsanityBoutDue = false; // безумие снято целиком — приступать не к чему
    }

    /// <summary>
    ///     Записывает случай безумия, связанного с Мифами: первый даёт +5 к навыку "Мифы Ктулху",
    ///     каждый следующий +1 (стр. 160–161). Максимум Рассудка при этом падает.
    /// </summary>
    public static int RecordMythosInsanity(Character character)
    {
        var gain = character.State.MythosInsanityCount == 0 ? 5 : 1;
        character.State.MythosInsanityCount++;

        AddMythos(character, gain);
        return gain;
    }

    private static Skill? FindMythosSkill(Character character) =>
        character.Skills.SkillGroups
            .SelectMany(g => g.Skills)
            .FirstOrDefault(s => string.Equals(s.Name, MythosSkillName, StringComparison.Ordinal));
}
