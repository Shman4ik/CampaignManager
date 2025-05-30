namespace CampaignManager.Web.Components.Features.Characters.Model;

public class SkillGroup
{
    public required string Name { get; set; }
    public List<Skill> Skills { get; set; } = new();

    public string NewSkillName { get; set; } = "";
    public string NewSkillBaseValue { get; set; } = "";

    public void AddSkill(Skill skill)
    {
        Skills.Add(skill);
    }

    public void RemoveSkill(Skill skill)
    {
        Skills.Remove(skill);
    }
}