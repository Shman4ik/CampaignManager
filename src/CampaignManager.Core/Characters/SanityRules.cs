using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Рассудок, безумие и Мифы (глава 8). Списание рассудка, отметки безумия и флаг «положен приступ»
/// живут только здесь: книги Мифов, бой и панель листа идут через <see cref="ApplyLoss"/> и
/// <see cref="AddMythos"/>, а не правят поля сами.
/// </summary>
public static class SanityRules
{
    public const int AbsoluteMaxSanity = 99;
    private const int MaxMythos = 99;

    /// <summary>Потеря от одной причины, после которой проверяют ИНТ на временное безумие (стр. 153).</summary>
    public const int TemporaryInsanityThreshold = 5;

    /// <summary>Максимум Рассудка = 99 − Мифы Ктулху (стр. 63, 153).</summary>
    public static int ComputeMaxSanity(CharacterSheet sheet, SkillCatalog catalog) =>
        Math.Max(0, AbsoluteMaxSanity - MythosValue(sheet, catalog));

    public static int MythosValue(CharacterSheet sheet, SkillCatalog catalog) =>
        sheet.Value(catalog, SkillCodes.Mythos);

    /// <summary>Максимум с поправкой из книги, если она есть.</summary>
    private static int MaxSanity(CharacterSheet sheet, SkillCatalog catalog) =>
        sheet.Overrides.MaxSanity ?? ComputeMaxSanity(sheet, catalog);

    /// <summary>
    /// Прибавляет к Мифам (не выше 99); текущий Рассудок прижимается к новому максимуму. Возвращает
    /// фактическую прибавку. Строки Мифов на листе нет — она заводится: в 2.0 навык берётся из
    /// справочника, а не из того, что случайно оказалось на листе. Ноль — Мифов нет и в справочнике.
    /// </summary>
    public static int AddMythos(CharacterSheet sheet, SkillCatalog catalog, int gain)
    {
        if (gain <= 0)
            return 0;

        var mythos = sheet.EnsureEntry(catalog, SkillCodes.Mythos);
        if (mythos is null)
            return 0;

        var before = mythos.Value;
        mythos.Value = Math.Min(MaxMythos, before + gain);
        sheet.Current.Sanity = Math.Min(sheet.Current.Sanity, MaxSanity(sheet, catalog));

        return mythos.Value - before;
    }

    /// <summary>
    /// Порог бессрочного безумия: «не менее 1/5 текущих пунктов рассудка» за игровой день (стр. 154).
    /// Пятая часть бывает дробной, потеря — целая, поэтому порог округляется <b>вверх</b>: при Рассудке 52
    /// нужно 11, а не 10. «Текущий» — Рассудок на начало дня (<see cref="SanityAtDayStart"/>): иначе
    /// знаменатель уменьшался бы от той самой потери, с которой его сравнивают.
    /// </summary>
    public static int IndefiniteInsanityThreshold(int sanityAtDayStart) =>
        sanityAtDayStart <= 0 ? 0 : (sanityAtDayStart + 4) / 5;

    /// <summary>
    /// Рассудок на начало дня: нынешний плюс потерянное за день. Прибавка посреди дня счётчик не
    /// уменьшает — порог от этого только строже, а не мягче.
    /// </summary>
    public static int SanityAtDayStart(int currentSanity, int lostToday) =>
        Math.Max(0, currentSanity) + Math.Max(0, lostToday);

    /// <summary>За день потеряно не меньше пятой части Рассудка, бывшего на его начало.</summary>
    public static bool IsIndefiniteInsanityLoss(int currentSanity, int lostToday) =>
        lostToday > 0 && lostToday >= IndefiniteInsanityThreshold(SanityAtDayStart(currentSanity, lostToday));

    /// <summary>Рассудок на нуле — неизлечимо безумен (стр. 154). Своего флага нет.</summary>
    public static bool IsPermanentlyInsane(CharacterSheet sheet) => sheet.Current.Sanity <= 0;

    /// <summary>Временно или бессрочно безумен — после приступа это «затаённое безумие» (стр. 156).</summary>
    public static bool IsInsane(CharacterSheet sheet) =>
        sheet.Condition.TemporaryInsanity || sheet.Condition.IndefiniteInsanity;

    /// <summary>
    /// Списывает рассудок как одну причину потери: оба окна порогов (последняя причина и день) и, если
    /// сыщик уже безумен, флаг нового приступа — в затаённом безумии его вызывает даже один пункт.
    /// </summary>
    /// <returns>Сколько списано на самом деле — ниже нуля Рассудок не падает.</returns>
    public static int ApplyLoss(CharacterSheet sheet, SkillCatalog catalog, int amount)
    {
        var current = Math.Clamp(sheet.Current.Sanity, 0, MaxSanity(sheet, catalog));
        var actualLoss = Math.Min(current, Math.Max(0, amount));

        sheet.Current.Sanity = current - actualLoss;
        sheet.Condition.SanityLostToday += actualLoss;
        sheet.Condition.LastSanityLoss = actualLoss;

        if (actualLoss > 0 && IsInsane(sheet))
            sheet.Condition.BoutDue = true;

        return actualLoss;
    }

    /// <summary>
    /// Прибавляет рассудок, не выходя за максимум (99 − Мифы). Возвращает фактическую прибавку. Одна
    /// функция на награду Хранителя, самолечение и 90% навыка — в v1 копия жила ещё в панели листа.
    /// </summary>
    public static int Grant(CharacterSheet sheet, SkillCatalog catalog, int amount)
    {
        var max = MaxSanity(sheet, catalog);
        var newValue = Math.Min(max, sheet.Current.Sanity + amount);
        var actual = newValue - sheet.Current.Sanity;
        sheet.Current.Sanity = newValue;
        return actual;
    }

    /// <summary>
    /// Отметка временного безумия (успешная проверка ИНТ после потери ≥5 от одной причины, стр. 153).
    /// Любое безумие начинается с приступа (стр. 154) — отметка делает его положенным; повторная
    /// отметка начинает отсчёт заново.
    /// </summary>
    public static void SetTemporaryInsanity(CharacterSheet sheet, bool insane, DateTimeOffset? now = null)
    {
        sheet.Condition.TemporaryInsanity = insane;
        sheet.Condition.TemporaryInsanityStartedAt = insane ? now ?? DateTimeOffset.UtcNow : null;
        UpdateBoutDueOnMark(sheet, insane);
    }

    /// <summary>Отметка бессрочного безумия (≥1/5 Рассудка за день, стр. 154).</summary>
    public static void SetIndefiniteInsanity(CharacterSheet sheet, bool insane, DateTimeOffset? now = null)
    {
        sheet.Condition.IndefiniteInsanity = insane;
        sheet.Condition.IndefiniteInsanityStartedAt = insane ? now ?? DateTimeOffset.UtcNow : null;
        UpdateBoutDueOnMark(sheet, insane);
    }

    /// <summary>Разыгранный приступ: больше не положен, на листе виден итог.</summary>
    public static void RecordBout(CharacterSheet sheet, InsanityBoutMode mode, int roll, int? duration,
        DateTimeOffset? now = null)
    {
        sheet.Condition.LastBout = new InsanityBout
        {
            Mode = mode,
            Roll = roll,
            Duration = duration,
            RolledAt = now ?? DateTimeOffset.UtcNow,
        };
        sheet.Condition.BoutDue = false;
    }

    /// <summary>Хранитель обошёлся без приступа — напоминание снимается.</summary>
    public static void DismissBout(CharacterSheet sheet) => sheet.Condition.BoutDue = false;

    /// <summary>
    /// Случай безумия, связанного с Мифами: первый даёт +5 к Мифам, каждый следующий +1 (стр. 160–161).
    /// Возвращает фактическую прибавку; случай засчитывается, только если прибавку было куда записать
    /// (в v1 счётчик рос и без навыка, и следующий случай давал уже +1 — rules-findings F-S05).
    /// </summary>
    public static int RecordMythosInsanity(CharacterSheet sheet, SkillCatalog catalog)
    {
        var gain = sheet.Condition.MythosInsanityCount == 0 ? 5 : 1;
        if (catalog.FindByCode(SkillCodes.Mythos) is null)
            return 0;

        sheet.Condition.MythosInsanityCount++;
        return AddMythos(sheet, catalog, gain);
    }

    /// <summary>
    /// Откат случайного нажатия: снимает ровно ту прибавку, что дал последний случай (−5 за первый, −1 за
    /// следующие); Мифы не уходят ниже нуля. Возвращает, сколько снято.
    /// </summary>
    public static int UndoMythosInsanity(CharacterSheet sheet, SkillCatalog catalog)
    {
        if (sheet.Condition.MythosInsanityCount <= 0)
            return 0;

        var loss = sheet.Condition.MythosInsanityCount == 1 ? 5 : 1;
        sheet.Condition.MythosInsanityCount--;

        if (sheet.Entry(catalog, SkillCodes.Mythos) is not { } mythos)
            return 0;

        var before = mythos.Value;
        mythos.Value = Math.Max(0, before - loss);
        return before - mythos.Value;
    }

    /// <summary>«Новый день»: оба окна потерь обнуляются.</summary>
    public static void StartNewDay(CharacterSheet sheet)
    {
        sheet.Condition.SanityLostToday = 0;
        sheet.Condition.LastSanityLoss = 0;
    }

    /// <summary>Сколько Мифов даст следующий случай — подпись кнопки.</summary>
    public static int NextMythosGain(CharacterSheet sheet) => sheet.Condition.MythosInsanityCount == 0 ? 5 : 1;

    private static void UpdateBoutDueOnMark(CharacterSheet sheet, bool insane)
    {
        if (insane)
            sheet.Condition.BoutDue = true;
        else if (!IsInsane(sheet))
            sheet.Condition.BoutDue = false; // безумие снято целиком — приступать не к чему
    }
}
