using System.Net;
using CampaignManager.Contracts.Files;
using CampaignManager.Data;
using CampaignManager.Data.Files;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Files;

public sealed class FileReferencesTests
{
    // Anti-join из SCHEMA.md («Проверки после переноса»). Список строится по модели: если здесь
    // появилась новая ссылка — она уже в поиске сирот, тест просто надо дополнить.
    [Fact]
    public void Orphan_search_covers_every_reference_from_schema()
    {
        var options = new DbContextOptionsBuilder<CmDbContext>();
        CmDatabase.Configure(options, TestDatabase.Unreachable);
        using var context = new CmDbContext(options.Options);

        Assert.Equal(
        [
            ("cm.artifact_images", "file_id"),
            ("cm.books", "image_file_id"),
            ("cm.characters", "portrait_file_id"),
            ("cm.creature_images", "file_id"),
            ("cm.items", "image_file_id"),
            ("cm.music_tracks", "file_id"),
            ("cm.occupation_images", "file_id"),
            ("cm.scenario_handouts", "file_id"),
            ("cm.spell_images", "file_id"),
            ("cm.weapon_images", "file_id"),
        ], FileReferences.All(context.Model));
    }

    // Без входа файлы закрыты: 401 ещё до базы и хранилища (права — AccessPolicy, T1.4).
    [Theory]
    [InlineData("GET", "/api/v1/files/0199a2b1-0000-7000-8000-000000000001")]
    [InlineData("GET", FilesRoutes.Orphans)]
    [InlineData("POST", FilesRoutes.External)]
    public async Task Files_are_closed_without_sign_in(string method, string path)
    {
        await using var factory = new CmApp();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = new StringContent("""{"url":"https://example.org/a.jpg"}""", System.Text.Encoding.UTF8, "application/json");
        }

        using var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
