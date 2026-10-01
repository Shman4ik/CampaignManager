namespace CampaignManager.Core.Catalogs;

public enum SkillCategory
{
    ProblemSolving,
    InformationGathering,
    Special,
    Social,
    Healing,
    CombatGeneral,
    Knowledge,
    CombatFirearms,
    Actions,
}

/// <summary>Формула очков навыков профессии: <c>Edu2DexOrStr2</c> = ОБР×2 + (ЛВК или СИЛ)×2.</summary>
public enum SkillPointsFormula
{
    Edu4,
    Edu2Dex2,
    Edu2App2,
    Edu2Str2,
    Edu2Pow2,
    Edu2DexOrStr2,
    Edu2AppOrPow2,
    Edu2DexOrPow2,
    Edu2AppOrDexOrStr2,
}

/// <summary>Вид слота навыка профессии (<c>cm.occupation_slots.kind</c>).</summary>
public enum OccupationSlotKind
{
    /// <summary>Конкретный навык.</summary>
    Skill,

    /// <summary>Названная специализация навыка-родителя («Язык, иностранный (латынь)»).</summary>
    Specialization,

    /// <summary>Любая специализация навыка-родителя.</summary>
    AnySpecialization,

    /// <summary>Выбор из списка вариантов (<c>cm.occupation_slot_options</c>).</summary>
    Choice,

    /// <summary>Любой социальный навык.</summary>
    Social,

    /// <summary>Любой навык.</summary>
    Free,
}

public enum WeaponType
{
    Melee,
    Pistols,
    Rifles,
    Shotguns,
    AssaultRifles,
    SubmachineGuns,
    MachineGuns,
    ExplosivesAndHeavyWeapons,
    Other,
}

public enum BookType
{
    MythosBook,
    OccultBook,
}

public enum CreatureType
{
    Other,
    MythicMonsters,
    MythicGods,
    Monsters,
    Beast,
}
