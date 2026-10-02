using System.Security.Claims;
using CampaignManager.Contracts.Identity;
using CampaignManager.Core.Identity;
using CampaignManager.UI.Identity;
using Microsoft.AspNetCore.Components.Authorization;

namespace CampaignManager.Web.Client.Identity;

/// <summary>
/// Кто вошёл — по <c>GET /api/v1/me</c>, один раз на загрузку приложения. Сессия — кука сервера
/// (HttpOnly), клиент её не видит; вход и выход — полной загрузкой страницы, после неё состояние
/// читается заново. Роль в принципале — только чтобы прятать кнопки: защищает сервер.
/// </summary>
public sealed class MeAuthenticationStateProvider(IIdentityApi identityApi, ILogger<MeAuthenticationStateProvider> logger)
    : AuthenticationStateProvider, IUserSession
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private Task<AuthenticationState>? _state;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state ??= LoadAsync();

    /// <summary>Перечитать <c>/me</c> (новое имя после кабинета) и оповестить меню и <c>AuthorizeView</c>.</summary>
    public Task RefreshAsync()
    {
        _state = LoadAsync();
        NotifyAuthenticationStateChanged(_state);
        return _state;
    }

    private async Task<AuthenticationState> LoadAsync()
    {
        try
        {
            if (await identityApi.GetMeAsync() is not { } me)
            {
                return Anonymous;
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, me.Id.ToString()),
                    new Claim(ClaimTypes.Email, me.Email),
                    new Claim(ClaimTypes.Name, me.DisplayName),
                    .. RoleClaims(me.Role),
                ],
                authenticationType: "CampaignManager", ClaimTypes.Name, ClaimTypes.Role);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (HttpRequestException ex)
        {
            // Сервер недоступен — показываем приложение как без входа; следующая загрузка спросит снова.
            logger.LogWarning(ex, "Не удалось узнать текущего пользователя");
            return Anonymous;
        }
    }

    // Администратор — тоже Хранитель: так политика Keeper и IsInRole("Keeper") видят его одинаково.
    private static IEnumerable<Claim> RoleClaims(UserRole role) => role switch
    {
        UserRole.Admin => [new Claim(ClaimTypes.Role, nameof(UserRole.Admin)), new Claim(ClaimTypes.Role, nameof(UserRole.Keeper))],
        _ => [new Claim(ClaimTypes.Role, role.ToString())],
    };
}
