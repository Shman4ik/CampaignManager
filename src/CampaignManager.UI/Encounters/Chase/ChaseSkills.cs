using CampaignManager.Contracts.Characters;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.Core.KeeperScreen;

namespace CampaignManager.UI.Encounters.Chase;

/// <summary>Навык или характеристика в поле проверки погони: ключ (код справочника или <c>char:DEX</c>) и подпись.</summary>
public sealed record ChaseSkillOption(string Key, string Label);

/// <summary>
/// Значения навыков участников погони. В снимке участника навыков нет (ядро T2.6a), поэтому навык сыщика и НПС читается из
/// листа (<c>sheet.Value(catalog, code)</c> — та же база справочника, что на листе), характеристики — из снимка. У твари
/// навыков в снимке нет: Хранитель вписывает число или берёт ЛВК, её половину или пятую часть (стр. 142).
/// Листы читаются по одному разу на открытую погоню.
/// </summary>
public sealed class ChaseSkills(ICharactersApi characters)
{
    public const string Dex = "char:DEX";
    public const string Str = "char:STR";
    public const string Con = "char:CON";
    public const string Int = "char:INT";
    public const string Pow = "char:POW";
    public const string Luck = "luck";

    private readonly Dictionary<Guid, CharacterSheet?> _sheets = [];

    /// <summary>Навыки, которыми в погоне обычно проходят помехи и преграды, атакуют и прячутся (стр. 133–139).</summary>
    public static IReadOnlyList<string> Common { get; } =
    [
        Dex, Str, Con, "skill.climb", "skill.jump", "skill.swim", "skill.dodge", "skill.locksmith", "skill.stealth",
        "skill.spot-hidden", "skill.track", "skill.navigate", VehicleReference.Drive, VehicleReference.Ride, VehicleReference.Pilot,
        VehicleReference.HeavyMachinery, "skill.fighting.brawl", "skill.firearms.handgun", "skill.firearms.rifle-shotgun", "skill.throw",
        Int, Pow,
    ];

    public static IReadOnlyList<ChaseSkillOption> Options(SkillCatalog catalog) =>
        [.. Common.Select(key => new ChaseSkillOption(key, LabelOf(catalog, key)))];

    public static string LabelOf(SkillCatalog catalog, string key) => key switch
    {
        Dex => "ЛВК",
        Str => "СИЛ",
        Con => "ВЫН",
        Int => "ИНТ",
        Pow => "МОЩ",
        Luck => "Удача",
        _ => catalog.FindByCode(key)?.Name ?? key,
    };

    /// <summary>Прочитать листы участников (кто ещё не прочитан). Нет связи — значение просто не подставится.</summary>
    public async Task LoadAsync(EncounterState state)
    {
        foreach (var characterId in state.Participants.Select(p => p.SourceCharacterId).OfType<Guid>().Where(id => !_sheets.ContainsKey(id)).ToList())
        {
            try
            {
                _sheets[characterId] = (await characters.GetAsync(characterId)).Sheet;
            }
            catch (HttpRequestException)
            {
                _sheets[characterId] = null;
            }
        }
    }

    /// <summary>Значение навыка участника; null — не знаем (тварь без навыка, лист не прочитан): впишет Хранитель.</summary>
    public int? Value(EncounterParticipant participant, SkillCatalog catalog, string key)
    {
        switch (key)
        {
            case Dex: return participant.Stats.Dex;
            case Str: return participant.Stats.Str;
            case Con: return participant.Stats.Con;
            case Int: return participant.Stats.Int;
            case Pow: return participant.Stats.Pow;
            case Luck: return participant.Luck;
            case "skill.dodge" when participant.SourceCharacterId is null:
                return participant.Stats.Dodge > 0 ? participant.Stats.Dodge : null;
        }

        if (participant.SourceCharacterId is not { } id || _sheets.GetValueOrDefault(id) is not { } sheet)
            return null;

        var value = sheet.Value(catalog, key);
        return value > 0 || catalog.FindByCode(key) is not null ? value : null;
    }

    /// <summary>Навык управления транспортом водителя — из листа (Вождение, Пилотирование…); у твари — нет.</summary>
    public int? Driving(EncounterParticipant participant, SkillCatalog catalog, string skillCode)
    {
        var value = Value(participant, catalog, skillCode);
        if (value is null && ParentOf(skillCode) is { } parent)
            value = Value(participant, catalog, parent);
        return value;
    }

    private static string? ParentOf(string code) => Core.Catalogs.SkillCodes.ParentOf(code);

    /// <summary>Подстановка ЛВК у твари без навыка (стр. 142).</summary>
    public static int Substitute(EncounterParticipant participant, SkillAptitude aptitude) =>
        ChaseRules.SubstituteFromDex(participant.Stats.Dex, aptitude);
}
