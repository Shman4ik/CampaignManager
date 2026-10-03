using Bunit;
using CampaignManager.UI.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

// Тестовый вход: кнопки только в Development; сам адрес /dev/login вне Development сервер не маппит.
public sealed class LoginPageTests : KitContext
{
    [Fact]
    public void Development_offers_dev_login_for_every_role_with_return_url()
    {
        AddAuthorization();
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("login?returnUrl=%2Fdev%2Ffiles");

        var cut = Render<LoginPage>();
        var buttons = cut.FindAll("[data-testid='dev-login'] button");

        Assert.Equal(["Войти как игрок", "Войти как Хранитель", "Войти как администратор"],
            buttons.Select(b => b.TextContent.Trim()));

        buttons[1].Click();
        Assert.EndsWith("dev/login?as=keeper&returnUrl=%2Fdev%2Ffiles", navigation.Uri);
    }

    [Fact]
    public void Outside_development_there_is_no_dev_login()
    {
        Services.AddSingleton(new UiEnvironment(IsDevelopment: false));
        AddAuthorization();

        var cut = Render<LoginPage>();

        Assert.Empty(cut.FindAll("[data-testid='dev-login']"));
        Assert.Contains("Войти через Google", cut.Markup);
    }

    // Гость входит с главной одним нажатием: способы входа на ней же, без экрана выбора /login.
    [Fact]
    public void Guest_home_goes_straight_to_google_sign_in()
    {
        AddAuthorization();
        Services.AddSingleton<CampaignManager.Contracts.Campaigns.ICampaignsApi>(new TableFakes.Campaigns());
        var navigation = Services.GetRequiredService<NavigationManager>();

        var cut = Render<CampaignManager.UI.Campaigns.HomePage>();
        Assert.Contains("Войти по почте и паролю", cut.Markup);

        cut.Find("[data-testid='login-google'] button").Click();
        Assert.EndsWith("account/login?method=google&returnUrl=%2F", navigation.Uri);
    }
}
