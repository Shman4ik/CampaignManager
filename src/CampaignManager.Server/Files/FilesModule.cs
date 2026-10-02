using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Identity;
using CampaignManager.Server.Files.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampaignManager.Server.Files;

/// <summary>Файлы: <c>cm.files</c> поверх MinIO. Знание модуля — <c>Files/CLAUDE.md</c>.</summary>
public static class FilesModule
{
    /// <summary>Запас на заголовки multipart сверх самого файла.</summary>
    private const long MultipartOverheadBytes = 1024 * 1024;

    public static WebApplicationBuilder AddFilesModule(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<FilesOptions>().Bind(builder.Configuration.GetSection(FilesOptions.Section));
        services.AddOptions<MinioOptions>().Bind(builder.Configuration.GetSection(MinioOptions.Section));

        // Клиент MinIO — один на приложение. Настройки проверяются при первом обращении к файлам,
        // а не при старте: без них сервер работает, просто без файлов.
        services.TryAddSingleton<IObjectStorage, MinioObjectStorage>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<FileService>();
        return builder;
    }

    public static IEndpointRouteBuilder MapFilesApi(this IEndpointRouteBuilder app)
    {
        var maxUpload = app.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FilesOptions>>().Value.MaxUploadBytes;

        // Читать и загружать — любой вошедший; права на сирот проверяет и FileService (AccessPolicy).
        app.MapPost(FilesRoutes.Upload, UploadAsync)
            .RequireAuthorization()
            .WithName("UploadFile")
            .WithTags("Files")
            // Kestrel по умолчанию режет тело на 30 МБ, а трек бывает до 50.
            .WithMetadata(new RequestSizeLimitAttribute(maxUpload + MultipartOverheadBytes))
            // Клиент — WebAssembly и мобильное приложение, а не форма сервера: токена антифорджери
            // у них нет. Межсайтовую отправку формы закрывает SameSite=Lax у куки входа: браузер не
            // приложит её к POST с чужого сайта, и запрос придёт анонимным — 401 (Identity/CLAUDE.md).
            .DisableAntiforgery();

        app.MapPost(FilesRoutes.External, AddExternalAsync)
            .RequireAuthorization()
            .WithName("AddExternalFile")
            .WithTags("Files");

        app.MapMethods(FilesRoutes.ContentPattern, [HttpMethods.Get, HttpMethods.Head], FileContentEndpoint.HandleAsync)
            .RequireAuthorization()
            .WithName("GetFileContent")
            .WithTags("Files");

        app.MapGet(FilesRoutes.Orphans, GetOrphansAsync)
            .RequireAuthorization(Policies.Admin)
            .WithName("GetOrphanFiles")
            .WithTags("Files");

        app.MapPost(FilesRoutes.DeleteOrphans, DeleteOrphansAsync)
            .RequireAuthorization(Policies.Admin)
            .WithName("DeleteOrphanFiles")
            .WithTags("Files");

        return app;
    }

    private static async Task<IResult> UploadAsync(
        [FromForm(Name = FilesRoutes.UploadField)] IFormFile file,
        FileService files,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await files.UploadAsync(file.FileName, file.Length, file.OpenReadStream, cancellationToken));
        }
        catch (FileRejectedException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> AddExternalAsync(
        AddExternalFileRequest request,
        FileService files,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await files.AddExternalAsync(request.Url, cancellationToken));
        }
        catch (FileRejectedException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GetOrphansAsync(FileService files, CancellationToken cancellationToken) =>
        TypedResults.Ok(await files.GetOrphansAsync(cancellationToken));

    private static async Task<IResult> DeleteOrphansAsync(
        DeleteOrphansRequest request,
        FileService files,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await files.DeleteOrphansAsync(request.Ids, cancellationToken));
}
