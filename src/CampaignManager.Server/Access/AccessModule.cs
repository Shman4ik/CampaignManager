using CampaignManager.Contracts.Identity;
using CampaignManager.Core.Identity;
using Microsoft.AspNetCore.Authorization;

namespace CampaignManager.Server.Access;

/// <summary>
/// Права: <see cref="CurrentUser"/>, <see cref="AccessPolicy"/>, политики для <c>[Authorize]</c> и ответ
/// 403/404 на <see cref="AccessDeniedException"/>.
/// </summary>
public static class AccessModule
{
    public static WebApplicationBuilder AddAccessModule(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUser>();
        services.AddScoped<AccessPolicy>();
        services.AddExceptionHandler<AccessExceptionHandler>();

        // Роль — из cm.users через CurrentUser, а не из claims куки: в куке её нет.
        services.AddScoped<IAuthorizationHandler, UserRequirementHandler>();
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new UserRequirement(null))
                .Build())
            .AddPolicy(Policies.Keeper, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new UserRequirement(UserRole.Keeper)))
            .AddPolicy(Policies.Admin, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new UserRequirement(UserRole.Admin)));
        return builder;
    }
}

/// <summary>
/// Сессия принадлежит человеку из <c>cm.users</c> с ролью не ниже <see cref="Role"/>
/// (<c>null</c> — любая роль). Хранитель — это и администратор.
/// </summary>
public sealed record UserRequirement(UserRole? Role) : IAuthorizationRequirement;

public sealed class UserRequirementHandler(CurrentUser currentUser) : AuthorizationHandler<UserRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, UserRequirement requirement)
    {
        var user = await currentUser.GetAsync();
        var allowed = requirement.Role switch
        {
            null => user is not null,
            UserRole.Admin => user is { IsAdmin: true },
            UserRole.Keeper => user is { IsKeeper: true },
            _ => user is not null,
        };

        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}
