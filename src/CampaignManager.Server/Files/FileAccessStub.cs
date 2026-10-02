namespace CampaignManager.Server.Files;

/// <summary>
/// TODO(T1.4): заглушка прав на файлы до <c>Server/Access</c>. Заменить на <c>AccessPolicy</c>:
/// читать — любой вошедший (и игрок: портреты, раздатки); загружать — вошедший, а
/// <c>uploaded_by_id</c> — из <c>CurrentUser</c>; сироты — только админ, ролевой политикой на группе
/// <c>/api/v1/admin</c>. Пока входа в 2.0 нет, поэтому заглушка пускает только в Development и
/// Testing, а во всех остальных окружениях отвечает 403: забытая заглушка закрывает, а не открывает.
/// </summary>
public sealed class FileAccessStub(IHostEnvironment environment)
{
    public bool CanRead => IsLocal;

    public bool CanUpload => IsLocal;

    public bool CanManageOrphans => IsLocal;

    private bool IsLocal => environment.IsDevelopment() || environment.IsEnvironment("Testing");
}
