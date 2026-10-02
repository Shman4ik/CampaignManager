using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Characters;

/// <summary>Оружие справочника — строки книги, которые снимаются в оружие листа (<c>cm.weapons</c>).</summary>
public sealed record WeaponData(Guid Id, string Name)
{
    public Guid? SkillId { get; init; }
    public string Damage { get; init; } = "";
    public string Range { get; init; } = "";
    public string Attacks { get; init; } = "";
    public string Ammo { get; init; } = "";
    public string Malfunction { get; init; } = "";
    public bool Impaling { get; init; }
}

/// <summary>
/// Копии записей справочника в лист. Лист держит <b>свой экземпляр</b>: правка патронов или стоимости
/// заклинания на листе не должна менять справочник у всех. В v1 выбранное на листе заклинание было
/// экземпляром общего кэша, и форма листа правила справочник (AUDIT, «Персонажи и НПС → Ошибки», 1); для
/// оружия ту же ошибку когда-то закрыл <c>WeaponFactory.CopyForCharacter</c>.
/// </summary>
public static class SheetCopies
{
    /// <summary>
    /// Оружие на лист: текст книги как есть. Числа для боя считаются при каждом чтении
    /// (<see cref="WeaponStatsReader"/>), поэтому правка урона на листе сразу видна бою (там же, ошибка 2).
    /// </summary>
    public static SheetWeapon Weapon(WeaponData weapon) => new()
    {
        CatalogWeaponId = weapon.Id,
        Name = weapon.Name,
        SkillId = weapon.SkillId,
        Damage = weapon.Damage,
        Range = weapon.Range,
        Attacks = weapon.Attacks,
        Ammo = weapon.Ammo,
        Malfunction = weapon.Malfunction,
        Impaling = weapon.Impaling,
    };

    /// <summary>Заклинание на лист: копия чисел и текста, другие названия — новым списком.</summary>
    public static SheetSpell Spell(SpellData spell) => new()
    {
        CatalogSpellId = spell.Id,
        Name = spell.Name,
        AlternativeNames = [.. spell.AlternativeNames],
        Cost = spell.Cost ?? "",
        CastingTime = spell.CastingTime ?? "",
        Description = spell.Description,
    };
}
