namespace CampaignManager.Contracts.Identity;

/// <summary>
/// Права токена агента (Auth0 client credentials, M2M) — scope'ы API в тенанте. Токен агента ходит только на адреса,
/// помеченные одним из них; всё остальное — 403 (<c>Server/Identity/CLAUDE.md</c>, «Токены агентов»).
/// </summary>
public static class MachineScopes
{
    /// <summary>Сценарии: библиотека, рабочее место, части, импорт, замена импортом, экспорт, удаление. Без прохождений.</summary>
    public const string Scenarios = "scenarios:write";

    /// <summary>Файлы: загрузка картинки и её содержимое — для раздаток и портретов.</summary>
    public const string Files = "files:write";
}
