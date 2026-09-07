using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Web.Components.Features.Chase.Model;

public class ChaseParticipant
{
    /// <summary>
    ///     Идентификатор участника погони, а не листа персонажа: двух одинаковых
    ///     преследователей из одного листа надо различать.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ChaseRole Role { get; set; }
    public bool IsPlayer { get; set; }

    // Транспорт (таблица V, стр. 143)
    public bool IsInVehicle { get; set; }
    public string? VehicleName { get; set; }
    public int VehicleSpeed { get; set; }

    /// <summary>Комплекция транспорта: и число костей 1d10 урона, и его «прочность» (стр. 136).</summary>
    public double VehicleBuild { get; set; }
    public double VehicleCurrentBuild { get; set; }

    /// <summary>Броня транспорта — защищает водителя и пассажиров (стр. 139).</summary>
    public int VehicleArmor { get; set; }

    /// <summary>Навык управления: Вождение автомобиля, Пилотирование, Верховая езда, тяжёлые машины.</summary>
    public string? VehicleSkillName { get; set; }

    /// <summary>Накопленный урон транспорта: каждые полные 10 пунктов снимают 1 Комплекции (стр. 136).</summary>
    public int VehicleDamageCarry { get; set; }

    /// <summary>Число лопнувших шин (стр. 139) — каждая уже снята с Комплекции.</summary>
    public int BurstTyres { get; set; }

    // Пассажиры (стр. 139): не проходят проверку скорости и не имеют действий перемещения
    public bool IsPassenger { get; set; }
    public Guid? CarrierId { get; set; }

    /// <summary>Штурман снял штрафную кость со следующего разгона (стр. 139).</summary>
    public bool HasNavigatorAssist { get; set; }

    // Способ передвижения (стр. 141)
    public MovementMode Mode { get; set; } = MovementMode.OnFoot;

    /// <summary>Отдельная СКО для плавания или полёта. 0 — своей нет, значит половина обычной СКО.</summary>
    public int NativeModeSpeed { get; set; }

    /// <summary>
    /// Маршрут при разделении погони (стр. 142). Пусто — все бегут вместе;
    /// разные метки означают отдельные, независимо отслеживаемые погони.
    /// </summary>
    public string? RouteLabel { get; set; }

    /// <summary>
    /// Преследователь медленнее самого медленного убегающего — в погоне не учитывается (стр. 140).
    /// </summary>
    public bool IsOutOfChase { get; set; }

    // Характеристики
    public int MovementRate { get; set; }
    public int Dexterity { get; set; }
    public int ConstitutionValue { get; set; }
    public int MaxHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }
    public int DrivingSkill { get; set; }
    public int BuildValue { get; set; } // Комплекция (для разрушения преград)
    public int LuckValue { get; set; } // Удача

    // Проверка скорости
    public int MovModifier { get; set; } // +1/0/-1 от проверки скорости
    public bool SpeedCheckCompleted { get; set; }

    /// <summary>
    /// Базовая СКО с учётом способа передвижения (стр. 141): транспорт — своя СКО;
    /// плавание или полёт — отдельная СКО, если она есть, иначе половина обычной.
    /// </summary>
    public int BaseMov => IsInVehicle
        ? VehicleSpeed
        : Mode switch
        {
            MovementMode.OnFoot => MovementRate,
            _ when NativeModeSpeed > 0 => NativeModeSpeed,
            _ => MovementRate / 2
        };

    public int AdjustedMov => Math.Max(0, BaseMov + MovModifier);

    /// <summary>Комплекция для боевых манёвров и тарана: у транспорта своя (стр. 136).</summary>
    public double EffectiveBuild => IsInVehicle ? VehicleCurrentBuild : BuildValue;

    /// <summary>Транспорт разрушен, когда Комплекция опустилась до нуля.</summary>
    public bool IsVehicleWrecked => IsInVehicle && VehicleCurrentBuild <= 0;

    // Экономика действий перемещения
    public int TotalMovementActions { get; set; }
    public int MovementActionsRemaining { get; set; }
    public int MovementActionDebt { get; set; } // Перенос потерянных действий на следующий раунд

    // Позиция и состояние
    public int CurrentLocation { get; set; }
    public bool HasActedThisRound { get; set; }
    public bool IsEliminated { get; set; }
    public bool HasEscaped { get; set; }
    public bool IsCaught { get; set; }

    // Ссылки на источник
    public Character? CharacterSource { get; set; }
    public Creature? CreatureSource { get; set; }

    /// <summary>Лист персонажа, с которого снят участник.</summary>
    public Guid? SourceCharacterId { get; set; }

    /// <summary>Существо, с которого снят участник.</summary>
    public Guid? SourceCreatureId { get; set; }

    public bool IsActive => !IsEliminated && !HasEscaped && !IsCaught && !IsOutOfChase;

    /// <summary>Участник ходит в порядке ЛВК, но действий перемещения у пассажира нет (стр. 139).</summary>
    public bool HasMovementActions => !IsPassenger;

    public ChaseParticipant() { }

    public ChaseParticipant(Character character, bool isPlayer = true, string? nameSuffix = null)
    {
        Name = string.IsNullOrEmpty(nameSuffix)
            ? character.PersonalInfo.Name
            : $"{character.PersonalInfo.Name} {nameSuffix}";
        IsPlayer = isPlayer;
        SourceCharacterId = character.Id;
        CharacterSource = character;

        MovementRate = character.PersonalInfo.MoveSpeed;
        Dexterity = character.Characteristics.Dexterity.Regular;
        ConstitutionValue = character.Characteristics.Constitution.Regular;
        MaxHitPoints = character.DerivedAttributes.HitPoints.MaxValue;
        CurrentHitPoints = character.DerivedAttributes.HitPoints.Value;
        DrivingSkill = CombatService.FindSkillValue(character, "Вождение");
        LuckValue = character.DerivedAttributes.Luck.Value;

        if (int.TryParse(character.PersonalInfo.Build, out var build))
            BuildValue = build;
    }

    public ChaseParticipant(Creature creature, string? nameSuffix = null)
    {
        Name = string.IsNullOrEmpty(nameSuffix) ? creature.Name : $"{creature.Name} {nameSuffix}";
        IsPlayer = false;
        SourceCreatureId = creature.Id;
        CreatureSource = creature;

        MovementRate = creature.CreatureCharacteristics.Speed;
        Dexterity = creature.CreatureCharacteristics.Dexterity.Value;
        ConstitutionValue = creature.CreatureCharacteristics.Constitution.Value;
        MaxHitPoints = creature.CreatureCharacteristics.HealPoint;
        CurrentHitPoints = creature.CreatureCharacteristics.HealPoint;
        BuildValue = creature.CreatureCharacteristics.AverageComplexity;
        LuckValue = creature.CreatureCharacteristics.Luck;
    }
}
