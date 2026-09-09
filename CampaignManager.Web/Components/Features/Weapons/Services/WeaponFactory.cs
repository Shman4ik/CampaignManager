using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Utilities.Services;

namespace CampaignManager.Web.Components.Features.Weapons.Services;

/// <summary>
/// Создание оружия для листа персонажа и заполнение разобранных полей.
/// </summary>
public static class WeaponFactory
{
    /// <summary>
    /// Полная копия каталожной записи для листа персонажа.
    /// <para>
    /// Каталожный экземпляр отдавать в лист нельзя: <c>WeaponService</c> держит список
    /// в <c>IMemoryCache</c>, общем для всех пользователей, — правка патронов на листе
    /// мутировала бы справочник у всех сразу. Копия получает собственный
    /// <c>Id</c> и ссылку <see cref="Weapon.CatalogWeaponId" />
    /// на исходную запись.
    /// </para>
    /// </summary>
    public static Weapon CopyForCharacter(Weapon catalogWeapon)
    {
        ArgumentNullException.ThrowIfNull(catalogWeapon);

        var copy = new Weapon
        {
            Type = catalogWeapon.Type,
            Name = catalogWeapon.Name,
            Skill = catalogWeapon.Skill,
            SkillId = catalogWeapon.SkillId,
            Is1920 = catalogWeapon.Is1920,
            IsModern = catalogWeapon.IsModern,
            IsRare = catalogWeapon.IsRare,
            Damage = catalogWeapon.Damage,
            Range = catalogWeapon.Range,
            Attacks = catalogWeapon.Attacks,
            Cost = catalogWeapon.Cost,
            Notes = catalogWeapon.Notes,
            Ammo = catalogWeapon.Ammo,
            Malfunction = catalogWeapon.Malfunction,
            MalfunctionThreshold = catalogWeapon.MalfunctionThreshold,
            IsImpaling = catalogWeapon.IsImpaling,
            CatalogWeaponId = catalogWeapon.CatalogWeaponId ?? catalogWeapon.Id
        };

        copy.Init();

        // Разобранные блоки пересобираются из строк, а не переносятся ссылкой:
        // копия и справочник не должны делить один объект.
        FillParsedStats(copy);

        return copy;
    }

    /// <summary>
    /// Заполняет разобранные поля оружия из его строковых колонок.
    /// Вызывается при создании копии и при сохранении оружия в каталоге.
    /// </summary>
    public static void FillParsedStats(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);

        weapon.DamageInfo = DamageFormulaParser.Parse(weapon.Damage);
        weapon.RangeInfo = WeaponStatsParser.ParseRange(weapon.Range);
        weapon.AttacksInfo = WeaponStatsParser.ParseAttacks(weapon.Attacks);
        weapon.AmmoInfo = WeaponStatsParser.ParseAmmo(weapon.Ammo);
        weapon.CostInfo = WeaponStatsParser.ParseCost(weapon.Cost);
        weapon.MalfunctionThreshold = WeaponStatsParser.ParseMalfunction(weapon.Malfunction);
    }
}
