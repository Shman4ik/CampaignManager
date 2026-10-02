using System.Text.Json.Serialization;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Documents;

namespace CampaignManager.Core.Encounters;

/// <summary>Необязательные правила боя (стр. 122–123) — книга помечает их как необязательные, по умолчанию выключены.</summary>
public sealed record CombatSettings : DocumentPart
{
    /// <summary>Очерёдность броском ЛВК: порядок по уровню успеха, держится до конца боя (стр. 122).</summary>
    public bool InitiativeRolls { get; set; }

    /// <summary>Броски инициативы сделаны — очередь идёт по их уровням (<see cref="CombatantState.InitiativeLevel"/>).</summary>
    public bool InitiativeRolled { get; set; }

    /// <summary>«Киношный» нокаут манёвром (стр. 123).</summary>
    public bool CinematicKnockout { get; set; }

    /// <summary>Тратить Удачу, чтобы не потерять сознание: 1, 2, 4, 8… (стр. 123).</summary>
    public bool LuckToStayConscious { get; set; }
}

/// <summary>Вид атаки участника: от него — встречная проверка или сложность по дальности, манёвр по Комплекции.</summary>
public enum CombatAttackKind
{
    Melee,
    Ranged,

    /// <summary>Атака-манёвр твари (захват, затаптывание): исход — разница Комплекции, а не урон (стр. 278).</summary>
    Maneuver,

    /// <summary>Особая атака твари — читать описание.</summary>
    Special,
}

/// <summary>
/// Чем участник воюет — снимок из листа или статблока (<see cref="CombatProfiles"/>). Числа оружия читаются из текста
/// книги на листе (<see cref="WeaponStatsReader"/>) при каждом снимке: разобранных блоков в листе нет.
/// </summary>
public sealed record CombatProfile : DocumentPart
{
    public List<CombatAttack> Attacks { get; set; } = [];

    /// <summary>Атак за раунд — столько же защит до численного превосходства (стр. 106, 279).</summary>
    public int AttacksPerRound { get; set; } = 1;

    /// <summary>Ближний бой для контратаки — драка, если другого навыка нет.</summary>
    public int FightBack { get; set; }

    public int FirstAid { get; set; }

    public int Medicine { get; set; }

    public int MechanicalRepair { get; set; }

    public List<CombatSpell> Spells { get; set; } = [];
}

/// <summary>Атака участника: оружие листа или атака статблока.</summary>
public sealed record CombatAttack : DocumentPart
{
    /// <summary>Ключ внутри участника: строка оружия листа, «brawl», «a0» у твари. По нему патроны и заклинивание.</summary>
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public int Skill { get; set; }

    /// <summary>Формула урона книгой: «1D10», «2d6 + БкУ»; «0» — весь урон в бонусе.</summary>
    public string Damage { get; set; } = "";

    public CombatAttackKind Kind { get; set; }

    /// <summary>Как добавляется бонус к урону: ближний бой — полный, стрельба — нет, если оружие не сказало иное (стр. 106).</summary>
    public CreatureDamageBonusMode DamageBonus { get; set; }

    /// <summary>Проникающее: при чрезвычайном успехе — ещё бросок урона (стр. 101). Огнестрел — всегда.</summary>
    public bool Impaling { get; set; }

    public int? AmmoCapacity { get; set; }

    /// <summary>Осечка: бросок не меньше — оружие не выстрелило (стр. 113).</summary>
    public int? Malfunction { get; set; }

    public int? BaseRangeMeters { get; set; }

    /// <summary>Сколько выстрелов за раунд можно сделать (серия из пистолета).</summary>
    public int? ShotsPerRound { get; set; }

    /// <summary>Есть автоматический режим (очереди).</summary>
    public bool Automatic { get; set; }

    public string? Description { get; set; }

    [JsonIgnore]
    public bool IsRanged => Kind == CombatAttackKind.Ranged;
}

/// <summary>Заклинание участника — экземпляр листа (цена и время — текст книги, числа подсказывает <see cref="SpellStatsReader"/>).</summary>
public sealed record CombatSpell : DocumentPart
{
    public Guid? CatalogSpellId { get; set; }

    public string Name { get; set; } = "";

    public string Cost { get; set; } = "";

    public string CastingTime { get; set; } = "";
}

/// <summary>
/// Что с участником сейчас в бою. Счётчики раунда (атаки, защиты, очередь) помечены раундом: в новом раунде они сами
/// становятся нулями — очереди не нужно знать про бой, а «сброс раунда» v1 (ResetRoundTracking) не нужен.
/// </summary>
public sealed record CombatantState : DocumentPart
{
    /// <summary>Раунд, к которому относятся <see cref="AttacksMade"/>, <see cref="DefensesMade"/>, <see cref="AutofireChecks"/>.</summary>
    public int Round { get; set; }

    public int AttacksMade { get; set; }

    public int DefensesMade { get; set; }

    public int AutofireChecks { get; set; }

    /// <summary>Прицелился: бонусная кость следующему выстрелу; теряется выстрелом, раной или движением (стр. 111).</summary>
    public bool Aiming { get; set; }

    /// <summary>Раунд, в котором участник укрылся от огня: стрелкам по нему — штрафная кость (стр. 111).</summary>
    public int? CoverRound { get; set; }

    /// <summary>Раунд, в котором он теряет атаку за укрытие.</summary>
    public int? AttackBlockedRound { get; set; }

    public bool Prone { get; set; }

    /// <summary>Кто держит его в захвате.</summary>
    public Guid? GrappledBy { get; set; }

    public bool Disarmed { get; set; }

    public bool Disadvantage { get; set; }

    /// <summary>Огнестрел наготове: +50 к ЛВК при очерёдности (стр. 110).</summary>
    public bool FirearmReady { get; set; }

    /// <summary>Патроны в магазине по ключу атаки; нет записи — магазин полон.</summary>
    public Dictionary<string, int> Ammo { get; set; } = [];

    /// <summary>Заклинившее оружие (ключ атаки) и сколько раундов починки осталось.</summary>
    public string? JammedAttack { get; set; }

    public int JamRoundsLeft { get; set; }

    /// <summary>Долгое сотворение заклинания (стр. 241).</summary>
    public SpellCasting? Casting { get; set; }

    /// <summary>Первая помощь по этой ране уже подействовала (снова — только после нового урона, гл. 4).</summary>
    public bool FirstAidReceived { get; set; }

    /// <summary>Первую помощь по этой ране уже пробовали: следующая попытка — повторная проверка.</summary>
    public bool FirstAidTried { get; set; }

    public bool MedicineReceived { get; set; }

    /// <summary>Раунд, в котором стал «при смерти»: первая проверка ВЫН — в конце следующего (стр. 118).</summary>
    public int? DyingSinceRound { get; set; }

    /// <summary>Раунд последней проверки ВЫН умирающего.</summary>
    public int? DyingCheckedRound { get; set; }

    /// <summary>Удачи потрачено, чтобы оставаться в сознании (цена удваивается, стр. 123).</summary>
    public int LuckSpentToStayConscious { get; set; }

    /// <summary>Уровень броска инициативы (необязательное правило, стр. 122).</summary>
    public SuccessLevel? InitiativeLevel { get; set; }

    public int? InitiativeRoll { get; set; }

    public CombatantState Copy() => this with
    {
        Ammo = new Dictionary<string, int>(Ammo),
        Casting = Casting is null ? null : Casting with { },
    };

    /// <summary>Атак в раунде <paramref name="round"/> (запись другого раунда — ноль).</summary>
    public int AttacksIn(int round) => Round == round ? AttacksMade : 0;

    public int DefensesIn(int round) => Round == round ? DefensesMade : 0;

    public int AutofireIn(int round) => Round == round ? AutofireChecks : 0;

    public bool TakingCoverIn(int round) => CoverRound == round;

    /// <summary>Начать счёт раунда заново, если запись старая.</summary>
    public void Touch(int round)
    {
        if (Round == round)
            return;

        Round = round;
        AttacksMade = 0;
        DefensesMade = 0;
        AutofireChecks = 0;
    }
}

/// <summary>Долгое сотворение: что творится, когда сработает, сорвано ли (стр. 177, 241).</summary>
public sealed record SpellCasting : DocumentPart
{
    public string SpellName { get; set; } = "";

    public Guid? TargetId { get; set; }

    public int StartedRound { get; set; }

    public int CompletesInRound { get; set; }

    public int MagicPoints { get; set; }

    public int Sanity { get; set; }

    public int Power { get; set; }

    public int HitPoints { get; set; }

    public bool TargetResists { get; set; }

    /// <summary>Заклинателя ранили посреди сотворения: эффекта нет, ПМ и рассудок платятся (стр. 177).</summary>
    public bool Disrupted { get; set; }
}
