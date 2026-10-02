namespace CampaignManager.UI.Identity;

/// <summary>
/// Перечитать, кто вошёл (<c>/api/v1/me</c>), без перезагрузки страницы: после смены имени в кабинете меню
/// должно показать новое. В v1 ради этого кабинет перезагружал страницу целиком (имя жило в claim куки);
/// в WebAssembly это перезапуск всего приложения. Реализация — <c>MeAuthenticationStateProvider</c> в
/// <c>Web.Client</c>.
/// </summary>
public interface IUserSession
{
    Task RefreshAsync();
}
