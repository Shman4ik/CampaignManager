namespace CampaignManager.Contracts.Campaigns;

/// <summary>Кампании, участники, журнал встреч и главная.</summary>
public static class CampaignsRoutes
{
    /// <summary><c>GET</c> — главная одним запросом: <see cref="HomeDto"/>.</summary>
    public const string Home = ApiRoutes.Prefix + "/home";

    /// <summary><c>GET</c> — кампании, где я участник (администратору — все); <c>POST</c> <see cref="CampaignInput"/> — завести.</summary>
    public const string Campaigns = ApiRoutes.Prefix + "/campaigns";

    /// <summary><c>GET</c> — кампания с участниками; <c>PUT</c> <see cref="CampaignInput"/>; <c>DELETE</c>.</summary>
    public const string CampaignPattern = Campaigns + "/{campaignId:guid}";

    /// <summary><c>POST</c> <see cref="JoinCampaignRequest"/> — вступить.</summary>
    public const string JoinPattern = CampaignPattern + "/join";

    /// <summary><c>PUT</c> <see cref="UpdateMemberRequest"/> — псевдоним; <c>DELETE</c> — выйти самому или исключить игрока.</summary>
    public const string MemberPattern = CampaignPattern + "/members/{userId:guid}";

    /// <summary><c>GET</c> — журнал; <c>POST</c> <see cref="CampaignSessionInput"/> — записать встречу.</summary>
    public const string JournalPattern = CampaignPattern + "/journal";

    /// <summary><c>PUT</c> <see cref="CampaignSessionInput"/>; <c>DELETE</c>.</summary>
    public const string SessionPattern = JournalPattern + "/{sessionId:guid}";

    public static string Campaign(Guid campaignId) => $"{Campaigns}/{campaignId}";

    public static string Join(Guid campaignId) => $"{Campaign(campaignId)}/join";

    public static string Member(Guid campaignId, Guid userId) => $"{Campaign(campaignId)}/members/{userId}";

    public static string Journal(Guid campaignId) => $"{Campaign(campaignId)}/journal";

    public static string Session(Guid campaignId, Guid sessionId) => $"{Journal(campaignId)}/{sessionId}";
}

/// <summary>Пределы полей — одни для формы, сервиса и сообщений об ошибке.</summary>
public static class CampaignLimits
{
    public const int NameLength = 100;
    public const int DisplayNameLength = 100;
    public const int SessionTitleLength = 200;
    public const int SessionTextLength = 20000;
    public const int MinSessionNumber = 1;
    public const int MaxSessionNumber = 9999;
}
