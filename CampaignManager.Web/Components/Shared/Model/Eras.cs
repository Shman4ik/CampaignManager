using System.Text.Json.Serialization;

namespace CampaignManager.Web.Components.Shared.Model;

[Flags]
public enum Eras
{
    [JsonStringEnumMemberName("1920x")]
    Classic = 1,

    [JsonStringEnumMemberName("Современность")]
    Modern = 2,
}