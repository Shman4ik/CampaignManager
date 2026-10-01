using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Chase.Services;

namespace CampaignManager.Rules.Tests.Chase;

/// <summary>Заготовки сцены погони: сервис без DI, участники только с нужными числами.</summary>
internal static class ChaseScene
{
    public static ChaseParticipant Runner(string name, ChaseRole role, int mov = 8, int dex = 50,
        int con = 50, int hp = 10, int build = 0) => new()
    {
        Name = name,
        Role = role,
        MovementRate = mov,
        Dexterity = dex,
        ConstitutionValue = con,
        MaxHitPoints = hp,
        CurrentHitPoints = hp,
        BuildValue = build
    };

    /// <summary>Посадить участника в транспорт с заданной Комплекцией (как <c>SetVehicleFromTemplate</c>).</summary>
    public static ChaseParticipant InVehicle(this ChaseParticipant p, double build, int speed = 14)
    {
        p.IsInVehicle = true;
        p.VehicleName = "Стандартный автомобиль";
        p.VehicleSpeed = speed;
        p.VehicleBuild = build;
        p.VehicleCurrentBuild = build;
        return p;
    }

    /// <summary>Трасса из <paramref name="locations" /> локаций; участники встают по правилу стр. 130.</summary>
    public static ChaseService Track(int locations, params ChaseParticipant[] participants)
    {
        var chase = new ChaseService();
        chase.SetupTrack(locations);
        foreach (var p in participants)
            chase.AddParticipant(p);
        return chase;
    }

    /// <summary>Трасса, на которой у <paramref name="location" /> стоит помеха.</summary>
    public static void PutHazard(this ChaseService chase, int location, int difficulty = 1,
        string? damageFormula = null, string name = "Лужа")
    {
        var l = chase.GetLocation(location)!;
        l.HasHazard = true;
        l.HazardName = name;
        l.HazardDifficulty = difficulty;
        l.HazardDamageFormula = damageFormula;
    }

    /// <summary>Преграда с ПЗ (0 — неразрушимая).</summary>
    public static void PutBarrier(this ChaseService chase, int location, int hitPoints = 0, int difficulty = 1,
        string name = "Забор")
    {
        var l = chase.GetLocation(location)!;
        l.HasBarrier = true;
        l.BarrierName = name;
        l.BarrierDifficulty = difficulty;
        l.BarrierHitPoints = hitPoints;
        l.BarrierCurrentHitPoints = hitPoints;
    }
}
