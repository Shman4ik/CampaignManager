using CampaignManager.Core.Identity;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Identity;
using CampaignManager.Server.Tests.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CampaignManager.Server.Tests.Identity;

/// <summary>Вход: поиск по sub, затем по почте; белый список; первые администраторы.</summary>
public sealed class UserDirectoryTests(SchemaDatabase database) : IClassFixture<SchemaDatabase>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task New_person_gets_a_player_row()
    {
        TestDatabase.SkipIfMissing();
        var email = Email();

        var user = await Directory().SignInAsync(new ExternalLogin("google-oauth2|1", email, "Харви"), Cancellation);

        Assert.NotNull(user);
        await using var db = database.CreateContext();
        var row = await db.Users.SingleAsync(u => u.Id == user.Id, Cancellation);
        Assert.Equal(("google-oauth2|1", "Харви", UserRole.Player), (row.Auth0Sub, row.DisplayName, row.Role));
        Assert.NotNull(row.LastLoginAt);
    }

    // Перенесённый из v1 пользователь: sub пуст, почта та же (в другом регистре) — строка его, не новая.
    [Fact]
    public async Task Migrated_user_is_linked_by_email()
    {
        TestDatabase.SkipIfMissing();
        var email = Email();
        var migrated = await AddUserAsync(email, UserRole.Keeper);

        var user = await Directory().SignInAsync(new ExternalLogin("auth0|42", email.ToUpperInvariant(), "Другое имя"), Cancellation);

        Assert.Equal(migrated.Id, user?.Id);
        await using var db = database.CreateContext();
        var row = await db.Users.SingleAsync(u => u.Id == migrated.Id, Cancellation);
        Assert.Equal(("auth0|42", UserRole.Keeper, "Хранитель"), (row.Auth0Sub, row.Role, row.DisplayName));
    }

    [Fact]
    public async Task Known_sub_wins_over_changed_email()
    {
        TestDatabase.SkipIfMissing();
        var subject = $"google-oauth2|{Guid.NewGuid():N}";
        var first = await Directory().SignInAsync(new ExternalLogin(subject, Email(), null), Cancellation);

        var again = await Directory().SignInAsync(new ExternalLogin(subject, Email(), null), Cancellation);

        Assert.Equal(first?.Id, again?.Id);
    }

    [Fact]
    public async Task Not_whitelisted_email_is_refused_and_not_created()
    {
        TestDatabase.SkipIfMissing();
        var email = Email();
        var directory = Directory(new AccessListOptions { AllowedDomains = ["friends.test"] });

        Assert.Null(await directory.SignInAsync(new ExternalLogin("auth0|x", email, null), Cancellation));
        await using var db = database.CreateContext();
        Assert.False(await db.Users.AnyAsync(u => u.Email == email, Cancellation));
    }

    [Theory]
    [InlineData("a@friends.test", true)]
    [InlineData("A@FRIENDS.TEST", true)]
    [InlineData("guest@example.test", true)]
    [InlineData("stranger@example.test", false)]
    public void Whitelist_matches_emails_and_domains_ignoring_case(string email, bool allowed)
    {
        var directory = new UserDirectory(null!, Options.Create(new AccessListOptions
        {
            AllowedEmails = ["Guest@Example.test"],
            AllowedDomains = ["friends.test"],
        }), TimeProvider.System, NullLogger<UserDirectory>.Instance);

        Assert.Equal(allowed, directory.IsAllowed(email));
    }

    [Fact]
    public async Task Admin_emails_promote_on_sign_in()
    {
        TestDatabase.SkipIfMissing();
        var email = Email();
        await AddUserAsync(email, UserRole.Player);

        var user = await Directory(new AccessListOptions { AdminEmails = [email] })
            .SignInAsync(new ExternalLogin("auth0|admin", email, null), Cancellation);

        Assert.Equal(UserRole.Admin, user?.Role);
    }

    private UserDirectory Directory(AccessListOptions? options = null) =>
        new(database.CreateContext(), Options.Create(options ?? new AccessListOptions()), TimeProvider.System,
            NullLogger<UserDirectory>.Instance);

    private async Task<User> AddUserAsync(string email, UserRole role)
    {
        await using var db = database.CreateContext();
        var user = new User { Email = email, DisplayName = "Хранитель", Role = role };
        db.Users.Add(user);
        await db.SaveChangesAsync(Cancellation);
        return user;
    }

    private static string Email() => $"{Guid.NewGuid():N}@example.test";
}
