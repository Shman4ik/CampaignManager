namespace CampaignManager.Web.Components.Features.Chase.Model;

public class ChaseLocation
{
    public int Number { get; set; }
    public string? Description { get; set; }

    // Препятствие (Barrier) — блокирует проход, пока не пройдена проверка или не разрушено
    public bool HasBarrier { get; set; }
    public string? BarrierName { get; set; }
    public string? BarrierSkillName { get; set; }
    public int BarrierSkillValue { get; set; }
    public int BarrierDifficulty { get; set; } = 1; // 1=Regular, 2=Hard, 3=Extreme
    public int BarrierHitPoints { get; set; } // Начальные ПЗ преграды (0 = нельзя разрушить)
    public int BarrierCurrentHitPoints { get; set; } // Текущие ПЗ
    public bool IsBarrierDestroyed { get; set; } // Когда ПЗ = 0, преграда → помеха

    // Опасность (Hazard) — не блокирует, но причиняет вред при провале. Участник ВСЕГДА проходит дальше
    public bool HasHazard { get; set; }
    public string? HazardName { get; set; }
    public string? HazardSkillName { get; set; }
    public int HazardSkillValue { get; set; }
    public int HazardDifficulty { get; set; } = 1;
    public string? HazardDamageFormula { get; set; } // Формула урона: "1D6", "1D3"
}
