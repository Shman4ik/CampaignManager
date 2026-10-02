using System.Text.Json.Nodes;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Data.Catalogs;
using CampaignManager.Migrate.V1;

namespace CampaignManager.Migrate.Catalogs;

/// <summary>
/// Четыре поля профессии v1 (<c>OccupationSkills</c>, <c>SkillChoices</c>, <c>SocialSkillSlots</c>,
/// <c>FreeSkillSlots</c>) → слоты <c>cm.occupation_slots</c> (SCHEMA, «Соответствие таблиц»): названный навык —
/// <c>Skill</c>, навык-родитель — <c>AnySpecialization</c>, названная специализация без справочника
/// («Язык, иностранный (латынь)») — <c>Specialization</c>, выбор — <c>Choice</c> с вариантами, затем
/// социальные и свободные. Строки v1 раскладываются здесь один раз; Core видит уже FK (F-S07).
/// </summary>
public static class OccupationSlots
{
    public static List<OccupationSlot> Build(JsonNode occupation, SkillCatalog catalog, SkillResolver skills, Action<string> problem)
    {
        List<OccupationSlot> slots = [];

        foreach (var name in occupation.Strings("OccupationSkills"))
        {
            switch (skills.Resolve(name))
            {
                case SkillMatch.Catalog { Skill: var skill }:
                    slots.Add(new OccupationSlot
                    {
                        Kind = catalog.IsParent(skill.Id) ? OccupationSlotKind.AnySpecialization : OccupationSlotKind.Skill,
                        SkillId = skill.Id,
                    });
                    break;
                case SkillMatch.Specialization { Parent: var parent, Name: var specialization }:
                    slots.Add(new OccupationSlot
                    {
                        Kind = OccupationSlotKind.Specialization,
                        SkillId = parent.Id,
                        Specialization = specialization,
                    });
                    break;
                default:
                    problem($"навык «{name}» не найден в справочнике — слот не перенесён");
                    break;
            }
        }

        foreach (var choice in occupation.Arr("SkillChoices").OfType<JsonObject>())
        {
            var slot = new OccupationSlot { Kind = OccupationSlotKind.Choice, ChooseCount = Math.Max(1, choice.Int("Count") ?? 1) };
            foreach (var option in choice.Strings("Options"))
            {
                // Вариант-родитель означает любую его специализацию — Core разворачивает его сам
                if (skills.CatalogId(option) is { } id)
                {
                    if (slot.Options.All(o => o.SkillId != id))
                    {
                        slot.Options.Add(new OccupationSlotOption { SkillId = id });
                    }
                }
                else
                {
                    problem($"вариант выбора «{option}» не найден в справочнике — не перенесён");
                }
            }

            if (slot.Options.Count > 0)
            {
                slots.Add(slot);
            }
        }

        for (var i = 0; i < (occupation.Int("SocialSkillSlots") ?? 0); i++)
        {
            slots.Add(new OccupationSlot { Kind = OccupationSlotKind.Social });
        }

        for (var i = 0; i < (occupation.Int("FreeSkillSlots") ?? 0); i++)
        {
            slots.Add(new OccupationSlot { Kind = OccupationSlotKind.Free });
        }

        for (var i = 0; i < slots.Count; i++)
        {
            slots[i].Ord = i;
        }

        return slots;
    }

    /// <summary>Битовая маска <c>OccupationTag</c> v1 → имена тегов (порядок битов — как в v1).</summary>
    public static List<string> Tags(int mask)
    {
        string[] names =
        [
            "Academic", "Social", "Combat", "Physical", "Stealth", "Technical", "Medical", "Investigative",
            "Artistic", "Occult", "Outdoor", "Criminal", "Language", "Nautical", "Scholarly",
        ];
        return names.Where((_, bit) => (mask & (1 << bit)) != 0).ToList();
    }
}
