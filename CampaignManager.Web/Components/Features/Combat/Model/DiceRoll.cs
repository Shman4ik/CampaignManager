namespace CampaignManager.Web.Components.Features.Combat.Model;

public class DiceRoll
{
    public int Result { get; set; }
    public int TargetValue { get; set; }
    public SuccessLevel SuccessLevel { get; set; }
    public bool IsCritical { get; set; }
    public bool IsFumble { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class DamageRoll
{
    public int TotalDamage { get; set; }
    public List<int> DiceResults { get; set; } = new();
    public int DamageBonus { get; set; }
    public string DamageFormula { get; set; } = string.Empty;
    public bool IsMaxDamage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}