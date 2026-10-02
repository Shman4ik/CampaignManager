using CampaignManager.Core;

namespace CampaignManager.UI.Checks;

/// <summary>
/// Состояние групповой проверки, которое должно пережить закрытие панели: навык, сложность, броски и
/// сыщики, вписанные руками. Хозяин — тот, кто показывает панель (ширма держит один на вкладку браузера);
/// без него панель заводит свой.
/// </summary>
public sealed class GroupCheckDraft
{
    /// <summary>Навык справочника (у состава кампании) — по нему берутся значения с листов.</summary>
    public Guid? SkillId { get; set; }

    /// <summary>Подпись навыка у вписанных руками: «Внимание».</summary>
    public string SkillName { get; set; } = "Внимание";

    public Difficulty Difficulty { get; set; }

    /// <summary>Выпало у каждого — по id строки; нет записи — ещё не бросали.</summary>
    public Dictionary<Guid, int> Rolls { get; } = [];

    /// <summary>Сыщики без листа: имя и значение навыка вписывает Хранитель.</summary>
    public List<ManualInvestigator> Manual { get; } = [];
}

/// <summary>Сыщик групповой проверки, вписанный руками.</summary>
public sealed class ManualInvestigator
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    public int Value { get; set; }
}
