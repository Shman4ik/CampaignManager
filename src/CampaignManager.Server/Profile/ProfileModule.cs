using System.Text.Json;
using CampaignManager.Contracts.Profile;

namespace CampaignManager.Server.Profile;

/// <summary>Личный кабинет: имя, заявка на Хранителя, настройки. Знание модуля — <c>Profile/CLAUDE.md</c>.</summary>
public static class ProfileModule
{
    private const string Tag = "Profile";

    public static WebApplicationBuilder AddProfileModule(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ProfileService>();
        return builder;
    }

    /// <summary>Всё — только вошедшим и только про себя: id в адресах нет, сервис берёт его из сессии.</summary>
    public static IEndpointRouteBuilder MapProfileApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(ProfileRoutes.Profile, (ProfileService profile, CancellationToken ct) => profile.GetAsync(ct))
            .RequireAuthorization().WithName("GetProfile").WithTags(Tag);
        app.MapPut(ProfileRoutes.DisplayName, (UpdateDisplayNameRequest request, ProfileService profile, CancellationToken ct) =>
                profile.UpdateDisplayNameAsync(request, ct))
            .RequireAuthorization().WithName("UpdateDisplayName").WithTags(Tag);
        app.MapPost(ProfileRoutes.KeeperApplication, (SubmitKeeperApplicationRequest request, ProfileService profile, CancellationToken ct) =>
                profile.SubmitKeeperApplicationAsync(request, ct))
            .RequireAuthorization().WithName("SubmitKeeperApplication").WithTags(Tag);

        app.MapGet(ProfileRoutes.Preferences, (ProfileService profile, CancellationToken ct) => profile.GetPreferencesAsync(ct))
            .RequireAuthorization().WithName("GetPreferences").WithTags(Tag);
        app.MapPut(ProfileRoutes.PreferencePattern, async (string key, JsonElement value, ProfileService profile, CancellationToken ct) =>
            {
                await profile.SetPreferenceAsync(key, value, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization().WithName("SetPreference").WithTags(Tag);
        app.MapDelete(ProfileRoutes.PreferencePattern, async (string key, ProfileService profile, CancellationToken ct) =>
            {
                await profile.RemovePreferenceAsync(key, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization().WithName("RemovePreference").WithTags(Tag);

        return app;
    }
}
