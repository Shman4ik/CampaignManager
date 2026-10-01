using CampaignManager.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CampaignManager.Server.Tests;

/// <summary>
/// Сервер в памяти. Окружение — Testing, чтобы не подхватить appsettings.Development.json
/// с базой разработчика. База — из <c>CM_TEST_DB</c> (D7); без неё строка указывает в никуда,
/// и тесты, которым нужна база, пропускаются через <see cref="TestDatabase.SkipIfMissing"/>.
/// </summary>
public sealed class CmApp : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{CmDatabase.ConnectionStringName}",
            TestDatabase.ConnectionString ?? TestDatabase.Unreachable);
    }
}
