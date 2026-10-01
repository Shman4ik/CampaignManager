using CampaignManager.Web.Components.Features.Combat.Services;
using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>Как применяется бонус к урону и какое оружие проникающее.</summary>
public sealed class DamageBonusTests
{
    [Theory]
    [Trait("page", "106")]
    [InlineData(true, null, DamageBonusType.Full)]
    [InlineData(false, null, DamageBonusType.None)]
    // None в данных не считается переопределением — иначе незаполненный DamageInfo съедал бы БкУ
    [InlineData(true, DamageBonusType.None, DamageBonusType.Full)]
    [InlineData(false, DamageBonusType.None, DamageBonusType.None)]
    [InlineData(true, DamageBonusType.Half, DamageBonusType.Half)]
    [InlineData(false, DamageBonusType.Half, DamageBonusType.Half)]
    [InlineData(false, DamageBonusType.Full, DamageBonusType.Full)]
    public void ResolveDamageBonusType_DataOverridesRule(bool isMelee, DamageBonusType? fromData, DamageBonusType expected)
    {
        var expr = fromData is { } bonus ? new DamageExpression { DamageBonus = bonus } : null;

        Assert.Equal(expected, CombatService.ResolveDamageBonusType(isMelee, expr));
    }

    [Theory]
    [Trait("page", "?")]
    [InlineData("Нож", true)]
    [InlineData("Охотничий нож", true)]
    [InlineData("Кинжал", true)]
    [InlineData("Меч", true)]
    [InlineData("Рапира", true)]
    [InlineData("Копьё", true)] // «копь» — с «ё» тоже
    [InlineData("Пика", true)]
    [InlineData("Штык", true)]
    [InlineData("Шпага", true)]
    [InlineData("Сабля", true)]
    [InlineData("Dagger", true)]
    [InlineData("Spear", true)]
    [InlineData("Дубинка", false)]
    [InlineData("Кастет", false)]
    [InlineData("Топор", false)]
    // Подстрока «пик» ловит и то, что пикой не является
    [InlineData("Пикап", true)]
    public void IsImpalingWeapon_MeleeByName(string name, bool expected)
    {
        Assert.Equal(expected, CombatService.IsImpalingWeapon(new Weapon { Name = name, Type = WeaponType.Melee }));
    }

    [Theory]
    [Trait("page", "?")]
    [InlineData(WeaponType.Pistols)]
    [InlineData(WeaponType.Shotguns)]
    [InlineData(WeaponType.ExplosivesAndHeavyWeapons)]
    [InlineData(WeaponType.Other)]
    public void IsImpalingWeapon_AnyNonMeleeType_True(WeaponType type)
    {
        Assert.True(CombatService.IsImpalingWeapon(new Weapon { Name = "Дубинка", Type = type }));
    }

    [Fact]
    [Trait("page", "?")]
    public void IsImpalingWeapon_ExplicitFlag_Wins()
    {
        Assert.True(CombatService.IsImpalingWeapon(new Weapon { Name = "Дубинка", Type = WeaponType.Melee, IsImpaling = true }));
    }
}
