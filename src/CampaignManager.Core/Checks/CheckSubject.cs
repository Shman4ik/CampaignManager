namespace CampaignManager.Core.Checks;

/// <summary>Что проверяется — от этого зависят повторная проверка, Удача и отметка развития.</summary>
public enum CheckSubjectKind
{
    /// <summary>Навык листа: его отмечают для фазы развития.</summary>
    Skill,

    /// <summary>Характеристика (СИЛ, ЛВК…): повторять можно, отмечать нечего.</summary>
    Characteristic,

    /// <summary>Удача: ни повтора, ни траты пунктов Удачи на неё (стр. 83, 97).</summary>
    Luck,

    /// <summary>Число, вписанное Хранителем руками, без листа за ним.</summary>
    Manual,
}

/// <summary>
/// Цель проверки: вид, подпись и значение, против которого бросают d100. Собирает её
/// <see cref="CheckSubjects"/> с листа или <see cref="Manual"/> — из вписанного числа.
/// <para>
/// Навык опознаётся по коду справочника (<see cref="SkillCode"/>, у специализации вне справочника — код
/// родителя в <see cref="ParentSkillCode"/>), а не по имени: в v1 «Автомат» не узнавался как огнестрел,
/// потому что имя не начиналось со «Стрельба» (F-C09).
/// </para>
/// </summary>
public sealed record CheckSubject(CheckSubjectKind Kind, string Name, int Value)
{
    /// <summary>Характеристика — у <see cref="CheckSubjectKind.Characteristic"/>.</summary>
    public Characteristic? Characteristic { get; init; }

    /// <summary>Навык справочника; null у своего навыка листа и у специализации вне справочника.</summary>
    public Guid? SkillId { get; init; }

    /// <summary>Код книжного навыка (<c>skill.spot-hidden</c>); null у самодельного.</summary>
    public string? SkillCode { get; init; }

    /// <summary>Код родителя специализации: <c>skill.firearms</c> у «Стрельба (пистолет)» и у своей «Стрельба (гарпун)».</summary>
    public string? ParentSkillCode { get; init; }

    /// <summary>
    /// Ключ для выпадающего списка и для поиска строки листа: <c>skill:{id}</c>, <c>own:{имя}</c>,
    /// <c>char:STR</c>, <c>luck</c>, <c>manual</c>.
    /// </summary>
    public string Key => Kind switch
    {
        CheckSubjectKind.Skill when SkillId is { } id => $"skill:{id}",
        CheckSubjectKind.Skill => $"own:{Name}",
        CheckSubjectKind.Characteristic => $"char:{Characteristic}",
        CheckSubjectKind.Luck => "luck",
        _ => "manual",
    };

    /// <summary>Значение, вписанное руками: «Проверка 50» без листа.</summary>
    public static CheckSubject Manual(int value, string? name = null) =>
        new(CheckSubjectKind.Manual, string.IsNullOrWhiteSpace(name) ? "Проверка" : name.Trim(), value);
}
