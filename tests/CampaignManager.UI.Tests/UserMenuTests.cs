using Bunit;
using CampaignManager.UI.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

// Вход через Auth0 в браузере агента не пройти (пароли не вводятся, callback dev-приложения — 8080),
// поэтому вошедшее состояние подвала оболочки проверяется здесь.
public sealed class UserMenuTests : KitContext
{
    [Fact]
    public void Guest_sees_login_with_return_to_current_page()
    {
        AddAuthorization();
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo("dev/files");

        var cut = Render<UserMenu>(p => p.Add(m => m.Placement, UserMenuPlacement.Rail));

        var login = cut.Find("a");
        Assert.Contains("Войти", login.TextContent);
        Assert.Equal("login?returnUrl=%2Fdev%2Ffiles", login.GetAttribute("href"));
    }

    [Fact]
    public void Signed_in_keeper_sees_cabinet_and_logout_in_rail()
    {
        AddAuthorization().SetAuthorized("Харви Уолтерс").SetRoles("Keeper");

        var cut = Render<UserMenu>(p => p.Add(m => m.Placement, UserMenuPlacement.Rail));

        Assert.Equal("Х", cut.Find(".cm-avatar").TextContent);
        Assert.Contains("Кабинет", cut.Markup);
        Assert.Contains("Выйти", cut.Find("button").TextContent);
    }

    [Fact]
    public void Sheet_shows_name_and_role()
    {
        AddAuthorization().SetAuthorized("Нора Флинн").SetRoles("Admin", "Keeper");

        var cut = Render<UserMenu>(p => p.Add(m => m.Placement, UserMenuPlacement.Sheet));

        var user = cut.Find("[data-testid='current-user']");
        Assert.Contains("Нора Флинн", user.TextContent);
        Assert.Contains("администратор", user.TextContent);
    }
}
