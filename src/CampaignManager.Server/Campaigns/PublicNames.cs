namespace CampaignManager.Server.Campaigns;

/// <summary>
/// Как человека показывать другим: псевдоним в кампании, иначе отображаемое имя. Почта не показывается
/// никогда — а вход без <c>name</c> у провайдера заводит имя, равное почте (так было и в v1), поэтому
/// имя с «@» отдаётся как <c>null</c> и UI просто не рисует строку.
/// </summary>
internal static class PublicNames
{
    public static string? Of(string? alias, string? displayName)
    {
        var name = string.IsNullOrWhiteSpace(alias) ? displayName?.Trim() : alias.Trim();
        return string.IsNullOrEmpty(name) || name.Contains('@', StringComparison.Ordinal) ? null : name;
    }

    /// <summary>
    /// Псевдоним из формы: пусто или совпадает с именем профиля — псевдонима нет (<c>null</c>), строка
    /// участника тогда идёт за именем из профиля.
    /// </summary>
    public static string? Alias(string? requested, string profileName)
    {
        var alias = requested?.Trim();
        return string.IsNullOrEmpty(alias) || string.Equals(alias, profileName.Trim(), StringComparison.Ordinal) ? null : alias;
    }
}
