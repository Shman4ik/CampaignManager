using CampaignManager.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CampaignManager.Server.Tests;

/// <summary>
/// Сервер в памяти. Окружение — Testing, чтобы не подхватить appsettings.Development.json
/// с базой разработчика. База — из <c>CM_TEST_DB</c> (D7); без неё строка указывает в никуда,
/// и тесты, которым нужна база, пропускаются через <see cref="TestDatabase.SkipIfMissing"/>.
/// Auth0 — пустышки: без них сервер не стартует, а к Auth0 тесты не ходят (см. <see cref="TestAuth"/>).
/// </summary>
public class CmApp : WebApplicationFactory<Program>
{
    public const string Auth0Domain = "auth0.example.test";
    public const string Auth0ClientId = "test-client-id";

    /// <summary>Строка подключения сервера; наследник подставляет свою одноразовую базу.</summary>
    protected virtual string ConnectionString => TestDatabase.ConnectionString ?? TestDatabase.Unreachable;

    /// <summary>Окружение хоста; Development — только у тестов тестового входа (<c>DevLoginTests</c>).</summary>
    protected virtual string EnvironmentName => "Testing";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting($"ConnectionStrings:{CmDatabase.ConnectionStringName}", ConnectionString);
        builder.UseSetting("Authentication:Auth0:Domain", Auth0Domain);
        builder.UseSetting("Authentication:Auth0:ClientId", Auth0ClientId);
        builder.UseSetting("Authentication:Auth0:ClientSecret", "test-client-secret");
        builder.ConfigureTestServices(services =>
        {
            // Ключи Data Protection сервер держит в cm.data_protection_keys; общая база CM_TEST_DB
            // миграциями не поднята, а куки корреляции входа без ключей не зашифровать.
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            TestAuth.Add(services);
        });
    }
}
