namespace CampaignManager.Web.Components.Features.Chase.Components.Actions;

/// <summary>
///     Поля ввода панели действий погони. Один экземпляр на <c>ChaseActionPanel</c>, общий для всех
///     панелей действий: навык и его значение переходят из действия в действие (их подставляет
///     помеха или преграда впереди), а сложность, способ разгона, параметры помехи и тяжесть
///     аварии помнятся до следующего раза. Поэтому состояние живёт здесь, а не в самих панелях —
///     панель действия пересоздаётся при каждом выборе и потеряла бы его.
/// </summary>
public sealed class ChaseActionForm
{
    public string SkillName { get; set; } = "";
    public int SkillValue { get; set; }
    public int Roll { get; set; }
    public int BonusDice { get; set; }
    public int DamageRoll { get; set; }
    public int LostActionsRoll { get; set; }
    public string TargetId { get; set; } = "";
    public bool StoppedToShoot { get; set; } = true;

    // Часть 4–5
    public int SecondSkillValue { get; set; }
    public int Difficulty { get; set; } = 1;
    public int BoostLocations { get; set; } = 2;
    public string ObstacleDescription { get; set; } = "";
    public int ObstacleLocation { get; set; } = 1;
    public bool ObstacleIsBarrier { get; set; }
    public int ObstacleHitPoints { get; set; }
    public int ObstacleCost { get; set; } = 1;
    public bool DriverUnconscious { get; set; }
    public string CrashTier { get; set; } = "Средняя авария";
    public int BuildLossRoll { get; set; }

    // Ответ цели в ближнем бою (стр. 136)
    public bool UseDefence { get; set; }
    public string DefenceSkillName { get; set; } = "";
    public int DefenceValue { get; set; }
    public int DefenceRoll { get; set; }
    public bool DefenceIsFightBack { get; set; }

    /// <summary>
    ///     Выбрано действие: броски, цель и ответ цели обнуляются, навык и остальные настройки
    ///     остаются как были.
    /// </summary>
    public void ResetForAction(int currentLocation)
    {
        Roll = 0;
        BonusDice = 0;
        DamageRoll = 0;
        LostActionsRoll = 0;
        TargetId = "";
        BuildLossRoll = 0;
        UseDefence = false;
        DefenceValue = 0;
        DefenceRoll = 0;
        DefenceSkillName = "";
        ObstacleLocation = currentLocation;
    }

    /// <summary>Действие закрыто: навык очищается, чтобы следующий раз подставился заново.</summary>
    public void ClearSkill()
    {
        SkillName = "";
        SkillValue = 0;
    }

    /// <summary>Значение, которое ушло бы в сервис как «не введено».</summary>
    public static int? Entered(int value) => value > 0 ? value : null;
}
