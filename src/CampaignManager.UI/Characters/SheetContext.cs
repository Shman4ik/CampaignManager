using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Core;
using CampaignManager.Core.Characters;

namespace CampaignManager.UI.Characters;

/// <summary>
/// Открытый лист для блоков страницы (каскадный параметр): документ, справочник навыков, права и обратная связь
/// со страницей. Блоки правят <see cref="Sheet"/> на месте и зовут <see cref="Changed"/> — страница перерисуется
/// целиком (правка рассудка видна и в «Состоянии», и в панели рассудка), а в базу правку унесёт автосохранение:
/// оно сверяет слепок документа, своего пути записи у блоков нет.
/// </summary>
public sealed class SheetContext(
    CharacterDto character,
    SkillCatalog catalog,
    IReadOnlyList<SkillDto> skills,
    SheetCatalogs catalogs,
    Action changed,
    Action<string?> check)
{
    public CharacterDto Character { get; } = character;

    public CharacterSheet Sheet => Character.Sheet;

    public SkillCatalog Catalog { get; } = catalog;

    /// <summary>Справочник навыков целиком — для справки по навыку (<c>SkillDetails</c>).</summary>
    public IReadOnlyList<SkillDto> Skills { get; } = skills;

    /// <summary>Остальные справочники — грузятся по требованию (оружие, заклинания, книги, бестиарий, предметы).</summary>
    public SheetCatalogs Catalogs { get; } = catalogs;

    /// <summary>Правка не разрешена (преген у игрока): поля только для чтения, проверки — без записи в лист.</summary>
    public bool ReadOnly => !Character.CanEdit;

    public Era Era => Character.Era;

    /// <summary>Блок изменил лист — перерисовать страницу.</summary>
    public void Changed() => changed();

    /// <summary>
    /// Проверка ИНТ открыта из тревоги Рассудка («Потеряно ≥5 по одной причине»): когда окно закроют после броска, страница
    /// сама отметит исход (успех — безумие, провал — разум отгородился). Обычная проверка ИНТ из плитки характеристики
    /// ничего не отмечает.
    /// </summary>
    public bool IntCheckPending { get; set; }

    /// <summary>Открыть диалог проверки с целью (<c>skill:{id}</c>, <c>char:STR</c>, <c>luck</c>; null — выбрать в окне).</summary>
    public void Check(string? key)
    {
        CheckMalfunction = null;
        check(key);
    }

    /// <summary>
    /// Проверка навыка оружия (касание оружия в «Игре»): бросок не ниже порога — осечка, её не выкупить Удачей и отметки за неё
    /// нет (стр. 97, 117). Без порога — обычная проверка навыка.
    /// </summary>
    public void CheckWeapon(string key, int? malfunction)
    {
        CheckMalfunction = malfunction;
        check(key);
    }

    /// <summary>Порог осечки оружия, из которого открыта последняя проверка (<see cref="CheckWeapon"/>); null — не оружие.</summary>
    public int? CheckMalfunction { get; private set; }
}
