using System.Text.Json;
using System.Text.Json.Nodes;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Documents;
using CampaignManager.Migrate.V1;

namespace CampaignManager.Migrate.Catalogs;

/// <summary>
/// Четыре jsonb-колонки твари v1 (<c>CreatureCharacteristics</c>, <c>Attacks</c>, <c>Skills</c>,
/// <c>SpecialAbilities</c>) → документ <see cref="Statblock"/>. Наследие v1 не переносится: словарь
/// <c>CombatDescriptions</c> (исходный текст книги, источник правды давно не он), <c>ImageUrl</c>, ключи
/// <c>Appearance</c>/<c>Education</c>/<c>Luck</c>/<c>Constitutions</c> у одной твари.
/// </summary>
public static class StatblockConverter
{
    /// <summary>Ключи наследия в характеристиках — их отбрасываем, а не кладём в <c>Extra</c>.</summary>
    public static readonly IReadOnlySet<string> LegacyKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "Appearance", "Education", "Luck", "Constitutions",
    };

    /// <param name="creature">Строка <c>games.Creatures</c> или тварь сценария v1 — у них одни поля.</param>
    /// <param name="skillId">Имя навыка → навык справочника (null — навык твари без справочника).</param>
    public static Statblock Convert(JsonNode creature, Func<string, Guid?> skillId)
    {
        var c = creature.Obj("CreatureCharacteristics") ?? [];
        var statblock = new Statblock
        {
            Str = Stat(c, "Strength"),
            Con = Stat(c, "Constitution"),
            Siz = Stat(c, "Size"),
            Dex = Stat(c, "Dexterity"),
            Int = Stat(c, "Intelligence"),
            Pow = Stat(c, "Power"),
            HitPoints = c.Int("HealPoint") ?? 0,
            MagicPoints = c.Int("ManaPoint") ?? 0,
            DamageBonus = c.Text("AverageDamageBonus") ?? "0",
            Build = c.Int("AverageComplexity") ?? 0,
            Speed = new CreatureSpeed
            {
                Move = c.Int("Speed") ?? 0,
                Swim = c.Int("SwimSpeed"),
                Fly = c.Int("FlySpeed"),
                Note = c.Text("SpeedNote"),
            },
            AttacksPerRound = c.Int("AttacksPerRound") ?? 1,
            AttacksPerRoundNote = c.Text("AttacksPerRoundNote"),
            Armor = c.Int("Armor") ?? 0,
            ArmorNote = c.Text("ArmorNote"),
            Dodge = c.Int("DodgeSkill") ?? 0,
            SanityLoss = c.Text("SanityLoss") ?? "",
            Initiative = c.Int("Initiative") ?? 0,
        };

        foreach (var attack in creature.Arr("Attacks").OfType<JsonObject>())
        {
            statblock.Attacks.Add(new CreatureAttack
            {
                Name = attack.Text("Name") ?? "",
                SkillValue = attack.Int("SkillValue") ?? 0,
                Damage = attack.Text("DamageFormula") ?? "",
                Kind = attack.Enum<CreatureAttackKind>("Kind") ?? CreatureAttackKind.Melee,
                DamageBonusMode = attack.Enum<CreatureDamageBonusMode>("DamageBonus") ?? CreatureDamageBonusMode.None,
                Description = attack.Text("Description"),
            });
        }

        foreach (var skill in creature.Arr("Skills").OfType<JsonObject>())
        {
            var name = skill.Text("Name") ?? "";
            statblock.Skills.Add(new CreatureSkill
            {
                SkillId = skillId(name),
                Name = name,
                Value = skill.Int("Value") ?? 0,
                Note = skill.Text("Note"),
            });
        }

        foreach (var (name, text) in creature.Obj("SpecialAbilities") ?? [])
        {
            statblock.SpecialAbilities.Add(new SpecialAbility { Name = name.Trim(), Text = text?.ToString().Trim() ?? "" });
        }

        return statblock;
    }

    /// <summary>Канонический JSON статблока — для сравнения твари сценария с бестиарием.</summary>
    public static string Canonical(Statblock statblock) =>
        JsonSerializer.Serialize(statblock, CmJsonContext.Default.Statblock);

    private static StatValue Stat(JsonObject characteristics, string key) => new()
    {
        Value = characteristics.Obj(key).Int("Value") ?? 0,
        Dice = characteristics.Obj(key).Text("DiceRoll"),
    };
}
