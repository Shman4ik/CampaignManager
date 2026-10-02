using System.Text.Json.Serialization;
using CampaignManager.Core.Documents;

namespace CampaignManager.Core.Encounters.Chase;

/// <summary>Этап погони (гл. 7): трасса и участники → проверка скорости (часть 1) → раунды (части 2–4) → конец.</summary>
public enum ChasePhase
{
    Setup,
    SpeedCheck,
    Active,
    Ended,
}

/// <summary>Кто за кем: убегающий или преследователь. Не то же, что сторона сцены — сыщики тоже бывают преследователями.</summary>
public enum ChaseRole
{
    Prey,
    Pursuer,
}

/// <summary>Способ передвижения (стр. 141): без своей СКО для способа — половина обычной.</summary>
public enum MovementMode
{
    OnFoot,
    Swimming,
    Flying,
}

/// <summary>
/// Почему участник больше не в погоне. Действует, только пока участник выбыл из очереди (<see cref="EncounterParticipant.IsOut"/>):
/// «Вернуть» в строке участника возвращает его в погоню без отдельной отметки.
/// </summary>
public enum ChaseStatus
{
    Running,
    Escaped,
    Caught,

    /// <summary>Преследователь медленнее самого медленного убегающего (стр. 140, 145).</summary>
    TooSlow,

    /// <summary>Преследователь потерял след (стр. 139).</summary>
    LostTrail,
}

/// <summary>
/// Погоня — своё свойство документа сцены (<see cref="EncounterState.Chase"/>): трасса, бегущие и необязательные правила
/// части 5. Участники, очередь, журнал и запись урона в лист — общие с боем (ядро T2.6a); здесь только то, чего у боя нет.
/// Меняют её <see cref="ChaseRules"/> (расстановка, проверка скорости, раунды) и эффекты погони через
/// <see cref="EncounterEngine.Apply(EncounterState, EncounterResolution, DateTimeOffset)"/>.
/// </summary>
public sealed record ChaseState : DocumentPart
{
    public ChasePhase Phase { get; set; }

    /// <summary>Отрыв самого медленного убегающего от самого быстрого преследователя: 2 локации, в особых случаях 1 (стр. 131).</summary>
    public int StartGap { get; set; } = 2;

    /// <summary>Локации по порядку, номера с 1. Помеха и преграда лежат в той локации, <b>в которую</b> входят.</summary>
    public List<ChaseLocation> Locations { get; set; } = [];

    public List<ChaseRunner> Runners { get; set; } = [];

    /// <summary>
    /// СКО самого медленного участника на начало погони (стр. 132): от неё — действия перемещения. Пересчитывается при
    /// старте, присоединении и смене СКО, но не при выбывании (стр. 140).
    /// </summary>
    public int SlowestMove { get; set; }

    /// <summary>Часть 5: случайные помехи и преграды (стр. 137).</summary>
    public bool RandomHazards { get; set; }

    /// <summary>Часть 5: внезапные помехи (стр. 137).</summary>
    public bool SuddenHazards { get; set; }

    /// <summary>Часть 5: «Педаль в пол» (стр. 137).</summary>
    public bool FloorIt { get; set; }

    /// <summary>Кто назначил последнюю внезапную помеху: true — игроки, false — Хранитель, null — ещё никто (стр. 137).</summary>
    public bool? SuddenHazardByPlayers { get; set; }

    public ChaseRunner? Runner(Guid participantId) => Runners.FirstOrDefault(r => r.ParticipantId == participantId);

    public ChaseLocation? Location(int number) => Locations.FirstOrDefault(l => l.Number == number);

    [JsonIgnore]
    public int LastLocation => Locations.Count;
}

/// <summary>Локация трассы: название и что мешает в неё войти.</summary>
public sealed record ChaseLocation : DocumentPart
{
    public int Number { get; set; }

    public string? Name { get; set; }

    /// <summary>Помеха: проходят всегда, провал — урон и 1d3 потерянных действий (стр. 133).</summary>
    public ChaseHazard? Hazard { get; set; }

    /// <summary>Преграда: не пускает, пока не пройдена проверка или не разрушена (стр. 134).</summary>
    public ChaseBarrier? Barrier { get; set; }
}

public sealed record ChaseHazard : DocumentPart
{
    public string Name { get; set; } = "Помеха";

    public Difficulty Difficulty { get; set; }

    /// <summary>Урон при провале — формула (таблица III для людей); пусто — Хранитель решает на месте.</summary>
    public string? Damage { get; set; }

    /// <summary>Каким навыком обычно проходят — подсказка (код справочника или «char:DEX»).</summary>
    public string? Skill { get; set; }
}

public sealed record ChaseBarrier : DocumentPart
{
    public string Name { get; set; } = "Преграда";

    public Difficulty Difficulty { get; set; }

    /// <summary>ПЗ преграды (стр. 136); 0 — сломать нельзя, только преодолеть.</summary>
    public int HitPoints { get; set; }

    public int HitPointsLeft { get; set; }

    public string? Skill { get; set; }

    [JsonIgnore]
    public bool CanBreak => HitPoints > 0;
}

/// <summary>
/// Участник в погоне: роль, локация, СКО и действия перемещения, транспорт. Ссылается на участника сцены по id — числа
/// (ЛВК, ВЫН, СКО, ПЗ) берутся из его снимка.
/// </summary>
public sealed record ChaseRunner : DocumentPart
{
    public Guid ParticipantId { get; set; }

    public ChaseRole Role { get; set; }

    /// <summary>Номер локации (с 1).</summary>
    public int Location { get; set; } = 1;

    public MovementMode Mode { get; set; }

    /// <summary>Своя СКО для плавания или полёта, вписанная Хранителем; null — из статблока, иначе половина обычной.</summary>
    public int? ModeSpeed { get; set; }

    /// <summary>Поправка проверки скорости на всю погоню: +1, 0, −1 (стр. 130).</summary>
    public int SpeedModifier { get; set; }

    public bool SpeedChecked { get; set; }

    /// <summary>Действий перемещения в этом раунде.</summary>
    public int Actions { get; set; }

    public int ActionsLeft { get; set; }

    /// <summary>Потерянные действия сверх оставшихся — спишутся в следующем раунде (стр. 133).</summary>
    public int Debt { get; set; }

    /// <summary>Атак в этом раунде: персонаж атакует в погоне столько же раз, сколько в бою (стр. 136).</summary>
    public int Attacks { get; set; }

    public ChaseVehicle? Vehicle { get; set; }

    /// <summary>Пассажир этого водителя (id участника): без проверки скорости и действий перемещения (стр. 139).</summary>
    public Guid? CarrierId { get; set; }

    /// <summary>Штурман помог: следующий разгон — на одну штрафную кость меньше (стр. 139).</summary>
    public bool NavigatorAssist { get; set; }

    /// <summary>Объявленный разгон «Педаль в пол», ещё не пройденный до конца.</summary>
    public ChaseBoost? Boost { get; set; }

    public ChaseStatus Status { get; set; }

    [JsonIgnore]
    public bool IsPassenger => CarrierId is not null;

    /// <summary>Ведёт транспорт (пассажир не водит, даже если у него записана машина).</summary>
    [JsonIgnore]
    public bool IsDriver => Vehicle is not null && !IsPassenger;
}

/// <summary>Транспорт водителя (таблица V): Комплекция падает от урона, у пассажиров — тот же транспорт водителя.</summary>
public sealed record ChaseVehicle : DocumentPart
{
    public string Name { get; set; } = "";

    public int Speed { get; set; }

    /// <summary>Начальная Комплекция: кости d10 тарана и порог «вдребезги» (стр. 136, 143).</summary>
    public double Build { get; set; }

    public double BuildLeft { get; set; }

    /// <summary>Броня для водителя и пассажиров (стр. 139).</summary>
    public int Armor { get; set; }

    /// <summary>Навык управления — код справочника (Вождение, Пилотирование, Верховая езда…).</summary>
    public string SkillCode { get; set; } = "";

    /// <summary>Навык водителя: из листа или вписан; null — нет, тогда половина ЛВК (стр. 142).</summary>
    public int? Skill { get; set; }
}

/// <summary>Разгон: сколько локаций ещё можно проехать без нового действия и сколько штрафных костей у помех на пути.</summary>
public sealed record ChaseBoost : DocumentPart
{
    public int LocationsLeft { get; set; }

    public int PenaltyDice { get; set; }
}
