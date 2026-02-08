namespace CampaignManager.Web.Components.Features.Chase.Model;

public class ChaseLocation
{
    public int Number { get; set; }

    // Препятствие (Barrier) — блокирует проход
    public bool HasBarrier { get; set; }
    public string? BarrierName { get; set; }
    public string? BarrierSkillName { get; set; }
    public int BarrierSkillValue { get; set; }
    public int BarrierDifficulty { get; set; } = 1; // 1=Regular, 2=Hard, 3=Extreme

    // Опасность (Hazard) — не блокирует, но причиняет вред при провале
    public bool HasHazard { get; set; }
    public string? HazardName { get; set; }
    public string? HazardSkillName { get; set; }
    public int HazardSkillValue { get; set; }
    public int HazardDifficulty { get; set; } = 1;
    public string? HazardConsequence { get; set; } // Формула урона: "1D6", "1D3"
}
