using CampaignManager.Web.Model;
using CampaignManager.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using System.Security.Claims;

namespace CampaignManager.Web.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder accountGroup = routes.MapGroup("/api/account");

        accountGroup.MapGet("/login", HandleLogin);
        accountGroup.MapGet("/logout", HandleLogout);
        accountGroup.MapGet("/process-login", HandleProcessLogin);
        accountGroup.MapGet("/auth-status", HandleAuthStatus); // Эндпоинт для отладки
    }

    private static async Task<IResult> HandleLogin(string? returnUrl, HttpContext httpContext)
    {
        // Очищаем существующие куки
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Устанавливаем путь перенаправления после аутентификации
        AuthenticationProperties properties = new() 
        { 
            RedirectUri = returnUrl ?? "/",
            IsPersistent = true 
        };

        // Вызываем аутентификацию Google
        return Results.Challenge(properties, new[] { GoogleDefaults.AuthenticationScheme });
    }

    private static async Task<IResult> HandleLogout(string? returnUrl, HttpContext httpContext)
    {
        AuthenticationProperties properties = new()
        {
            RedirectUri = returnUrl ?? "/"
        };
        
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme, properties);
        return Results.Redirect(returnUrl ?? "/");
    }

    private static async Task<IResult> HandleProcessLogin(
        HttpContext httpContext,
        IUserService userService)
    {
        try
        {
            // Получаем результат аутентификации от внешнего провайдера
            var authenticateResult = await httpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            Console.WriteLine($"Process-login: Authentication result successful: {authenticateResult?.Succeeded}");

            if (!authenticateResult.Succeeded)
            {
                Console.WriteLine("Process-login: External authentication failed");
                return Results.Redirect("/");
            }

            // Получаем Google клеймы из результата аутентификации
            var externalPrincipal = authenticateResult.Principal;
            string? email = externalPrincipal.FindFirst(ClaimTypes.Email)?.Value;
            string? name = externalPrincipal.FindFirst(ClaimTypes.Name)?.Value;

            Console.WriteLine($"Process-login: External auth successful, email={email}, name={name}");

            if (string.IsNullOrEmpty(email))
            {
                Console.WriteLine("Process-login: Email is empty in external claims");
                return Results.Redirect("/");
            }

            // Используем сервис для создания или обновления пользователя
            ApplicationUser user = await userService.EnsureUserExistsAsync(email, name);
            
            // Создаем identity с нужными клеймами
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, name ?? email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };
            
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            
            // Выполняем вход пользователя с постоянным куки
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTime.UtcNow.AddDays(14) // 14 дней из настроек в Program.cs
                });

            Console.WriteLine($"Process-login: User signed in, redirecting to /");
            
            // Перенаправляем на начальную страницу
            return Results.Redirect("/");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during login processing: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return Results.Redirect("/Error");
        }
    }
    
    // Эндпоинт для отладки состояния аутентификации
    private static IResult HandleAuthStatus(HttpContext httpContext)
    {
        bool isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;
        string? userName = httpContext.User.Identity?.Name;
        string? email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
        
        var claims = httpContext.User.Claims.Select(c => new { Type = c.Type, Value = c.Value }).ToList();
        
        return Results.Json(new
        {
            IsAuthenticated = isAuthenticated,
            UserName = userName,
            Email = email,
            Claims = claims
        });
    }
}