using System.Globalization;

namespace CampaignManager.Core.Encounters.Chase;

/// <summary>
/// Действие в ход погони — то, что предлагает <see cref="ChaseRules.AvailableActions"/>. Кнопки панели рисуются из этого
/// списка, а не цепочкой условий в разметке (в v1 видимость решали 15 <c>@if</c> в <c>ChaseActionPanel</c>).
/// </summary>
public enum ChaseActionKind
{
    Move,
    Hazard,
    Barrier,
    BreakBarrier,
    Melee,
    Ranged,
    Maneuver,
    Ram,
    Tyres,
    FloorIt,
    Navigate,
    Hide,
    Track,
    CreateObstacle,
    DriverControl,
}

/// <summary>Подписи погоны по-русски — одна таблица; тест требует подпись у каждого члена.</summary>
public static class ChaseText
{
    public static string Of(ChasePhase phase) => phase switch
    {
        ChasePhase.Setup => "Расстановка",
        ChasePhase.SpeedCheck => "Проверка скорости",
        ChasePhase.Active => "Погоня",
        ChasePhase.Ended => "Погоня окончена",
        _ => phase.ToString(),
    };

    public static string Of(ChaseRole role) => role switch
    {
        ChaseRole.Prey => "Убегающий",
        ChaseRole.Pursuer => "Преследователь",
        _ => role.ToString(),
    };

    public static string Of(MovementMode mode) => mode switch
    {
        MovementMode.OnFoot => "пешком",
        MovementMode.Swimming => "вплавь",
        MovementMode.Flying => "по воздуху",
        _ => mode.ToString(),
    };

    public static string Of(ChaseStatus status) => status switch
    {
        ChaseStatus.Running => "в погоне",
        ChaseStatus.Escaped => "сбежал",
        ChaseStatus.Caught => "пойман",
        ChaseStatus.TooSlow => "отстал",
        ChaseStatus.LostTrail => "потерял след",
        _ => status.ToString(),
    };

    public static string Of(ChaseActionKind action) => action switch
    {
        ChaseActionKind.Move => "Вперёд",
        ChaseActionKind.Hazard => "Пройти помеху",
        ChaseActionKind.Barrier => "Преодолеть преграду",
        ChaseActionKind.BreakBarrier => "Разрушить преграду",
        ChaseActionKind.Melee => "Ближний бой",
        ChaseActionKind.Ranged => "Стрельба",
        ChaseActionKind.Maneuver => "Манёвр",
        ChaseActionKind.Ram => "Таран",
        ChaseActionKind.Tyres => "По шинам",
        ChaseActionKind.FloorIt => "Педаль в пол",
        ChaseActionKind.Navigate => "Штурман",
        ChaseActionKind.Hide => "Спрятаться",
        ChaseActionKind.Track => "Искать след",
        ChaseActionKind.CreateObstacle => "Создать помеху",
        ChaseActionKind.DriverControl => "Удержать управление",
        _ => action.ToString(),
    };

    /// <summary>Значок Font Awesome действия.</summary>
    public static string Icon(ChaseActionKind action) => action switch
    {
        ChaseActionKind.Move => "fa-person-running",
        ChaseActionKind.Hazard => "fa-bolt",
        ChaseActionKind.Barrier => "fa-road-barrier",
        ChaseActionKind.BreakBarrier => "fa-burst",
        ChaseActionKind.Melee => "fa-hand-fist",
        ChaseActionKind.Ranged => "fa-gun",
        ChaseActionKind.Maneuver => "fa-people-pulling",
        ChaseActionKind.Ram => "fa-car-burst",
        ChaseActionKind.Tyres => "fa-crosshairs",
        ChaseActionKind.FloorIt => "fa-gauge-high",
        ChaseActionKind.Navigate => "fa-map",
        ChaseActionKind.Hide => "fa-user-secret",
        ChaseActionKind.Track => "fa-shoe-prints",
        ChaseActionKind.CreateObstacle => "fa-dumpster",
        ChaseActionKind.DriverControl => "fa-car-side",
        _ => "fa-circle",
    };

    /// <summary>«обычная», «трудная», «чрезвычайная» — сложность помехи и преграды.</summary>
    public static string Of(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Hard => "трудная",
        Difficulty.Extreme => "чрезвычайная",
        _ => "обычная",
    };

    /// <summary>Русское склонение по числу: 1 действие, 2 действия, 5 действий.</summary>
    public static string Plural(int count, string one, string few, string many)
    {
        var mod100 = Math.Abs(count) % 100;
        if (mod100 is >= 11 and <= 14)
            return many;

        return (Math.Abs(count) % 10) switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many,
        };
    }

    public static string Actions(int count) => $"{N(count)} {Plural(count, "действие", "действия", "действий")}";

    public static string Locations(int count) => $"{N(count)} {Plural(count, "локацию", "локации", "локаций")}";

    public static string PenaltyDice(int count) => $"{N(count)} {Plural(count, "штрафная кость", "штрафные кости", "штрафных костей")}";

    /// <summary>Комплекция транспорта: «5», «0,5» — как в книге.</summary>
    public static string Build(double build) =>
        build.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');

    public static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
