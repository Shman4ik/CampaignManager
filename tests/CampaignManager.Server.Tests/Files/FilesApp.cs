using CampaignManager.ApiClient.Files;
using CampaignManager.Core.Identity;
using CampaignManager.Data;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Files.Storage;
using CampaignManager.Server.Tests.Schema;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CampaignManager.Server.Tests.Files;

/// <summary>
/// Сервер с хранилищем в памяти, своей базой (<see cref="SchemaDatabase"/>) и переставляемыми
/// часами: возраст файла решает, сирота он или нет. Клиент по умолчанию — администратор
/// (<see cref="Admin"/>): сироты только ему; <see cref="Player"/> — для проверок прав.
/// </summary>
public sealed class FilesApp : IAsyncLifetime
{
    /// <summary>Маленький предел, чтобы проверить отказ без 50 МБ в памяти.</summary>
    public const int MaxUploadBytes = 64 * 1024;

    private WebApplicationFactory<Program>? _factory;

    public SchemaDatabase Database { get; } = new();

    public InMemoryObjectStorage Storage { get; } = new();

    public MovableTime Time { get; } = new();

    public User Admin { get; } = new() { Email = $"admin-{Guid.NewGuid():N}@example.test", DisplayName = "Админ", Role = UserRole.Admin };

    public User Player { get; } = new() { Email = $"player-{Guid.NewGuid():N}@example.test", DisplayName = "Игрок" };

    public async ValueTask InitializeAsync()
    {
        await Database.InitializeAsync();
        if (TestDatabase.ConnectionString is null)
        {
            return;
        }

        await using (var db = Database.CreateContext())
        {
            db.Users.AddRange(Admin, Player);
            await db.SaveChangesAsync();
        }

        // CmApp: пустышки Auth0, эфемерные ключи и сессия заголовками (TestAuth).
        _factory = new CmApp().WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"ConnectionStrings:{CmDatabase.ConnectionStringName}", Database.ConnectionString);
            builder.UseSetting("Files:MaxUploadBytes", MaxUploadBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IObjectStorage>();
                services.AddSingleton<IObjectStorage>(Storage);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Time);
            });
        });
    }

    /// <summary>
    /// Клиент без перехода по редиректам (внешний файл отвечает 302, его и проверяем) от имени
    /// администратора; <paramref name="anonymous"/> — без входа, <paramref name="user"/> — от имени другого.
    /// </summary>
    public HttpClient CreateClient(User? user = null, bool anonymous = false)
    {
        var client = (_factory ?? throw new InvalidOperationException(
                "Сервер не поднят: тест должен начинаться с TestDatabase.SkipIfMissing()."))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return anonymous ? client : client.As((user ?? Admin).Id);
    }

    public FilesApiClient Api() => new(CreateClient());

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await Database.DisposeAsync();
    }
}

/// <summary>Часы, которые тест может отвести назад.</summary>
public sealed class MovableTime : TimeProvider
{
    public TimeSpan Offset { get; set; }

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Offset;
}
