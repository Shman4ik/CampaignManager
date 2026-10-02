namespace CampaignManager.Core.Encounters.Chase;

/// <summary>
/// Урон транспорту (стр. 136, 143) — одна копия правил; таблицы V и VI — <c>KeeperScreen.VehicleReference</c>.
/// </summary>
public static class VehicleRules
{
    /// <summary>
    /// Сколько Комплекции снимает урон в пунктах: каждые полные 10 — минус 1, остаток меньше 10 <b>не учитывается</b>
    /// (стр. 136 и 143). v1 копил остаток до следующего удара (а находка F-P07 требовала копить и при ударе меньше 10) —
    /// книга этого не делает.
    /// </summary>
    public static int BuildLoss(int damage) => Math.Max(0, damage) / 10;

    /// <summary>Кости d10 тарана и разрушения преграды: по одной за пункт текущей Комплекции транспорта, не меньше одной (стр. 135–136).</summary>
    public static int DamageDice(double build) => Math.Max(1, (int)Math.Round(build));

    /// <summary>
    /// Отдача тарана (стр. 136): половина нанесённого урона (вниз), но потерять можно не больше Комплекции, что была у цели
    /// до удара (у мотоцикла с Комплекцией 1 машина теряет не больше 1).
    /// </summary>
    public static int RecoilBuildLoss(int damageDealt, double targetBuild) =>
        Math.Min(BuildLoss(damageDealt / 2), (int)Math.Ceiling(Math.Max(0, targetBuild)));

    /// <summary>
    /// Поломка (стр. 143): Комплекция упала до половины начальной (с округлением вниз) — одна штрафная кость к проверкам
    /// Вождения (или другого навыка управления). Разбитый (0) не едет вовсе.
    /// </summary>
    public static bool IsBrokenDown(ChaseVehicle vehicle) =>
        vehicle.BuildLeft > 0 && vehicle.BuildLeft <= Math.Floor(vehicle.Build / 2);

    public static bool IsWrecked(ChaseVehicle vehicle) => vehicle.BuildLeft <= 0;

    /// <summary>Штрафные кости к проверке управления этим транспортом.</summary>
    public static int PenaltyDice(ChaseVehicle? vehicle) => vehicle is not null && IsBrokenDown(vehicle) ? 1 : 0;

    /// <summary>
    /// Что стало с транспортом после потери <paramref name="loss"/> Комплекции одним ударом (стр. 143): потеря не меньше полной
    /// Комплекции — вдребезги (сидящие внутри, скорее всего, гибнут — решает Хранитель, можно дать проверку Удачи); дошло до
    /// нуля постепенно — вышел из строя и встал; до половины — поломка.
    /// </summary>
    public static string? Note(ChaseVehicle vehicle, double buildBefore, int loss)
    {
        var after = Math.Max(0, buildBefore - loss);
        if (loss > 0 && loss >= vehicle.Build)
            return "разбит вдребезги: сидящие внутри, скорее всего, гибнут (стр. 143)";
        if (after <= 0)
            return "вышел из строя и встал";
        if (after <= Math.Floor(vehicle.Build / 2) && buildBefore > Math.Floor(vehicle.Build / 2))
            return "поломка: штрафная кость к управлению";
        return null;
    }
}
