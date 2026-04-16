namespace CampaignManager.Web.Components.Features.Characters.Model;

[Flags]
public enum OccupationTag
{
    None         = 0,
    Academic     = 1 << 0,
    Social       = 1 << 1,
    Combat       = 1 << 2,
    Physical     = 1 << 3,
    Stealth      = 1 << 4,
    Technical    = 1 << 5,
    Medical      = 1 << 6,
    Investigative = 1 << 7,
    Artistic     = 1 << 8,
    Occult       = 1 << 9,
    Outdoor      = 1 << 10,
    Criminal     = 1 << 11,
    Language     = 1 << 12,
    Nautical     = 1 << 13,
    Scholarly    = 1 << 14
}
