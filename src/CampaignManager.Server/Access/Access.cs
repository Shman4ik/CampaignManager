namespace CampaignManager.Server.Access;

public enum Operation
{
    Read,
    Edit,
    Delete,
}

/// <summary>
/// Права текущего пользователя на один объект — ответ <see cref="AccessPolicy"/>. Эти же флаги
/// уходят в DTO (<c>canEdit</c>/<c>canDelete</c>): UI по ним прячет кнопки, но защита — <see cref="Demand"/>
/// в методе записи прикладного сервиса.
/// </summary>
public readonly record struct Access(bool CanRead, bool CanEdit, bool CanDelete)
{
    public static Access None => default;
    public static Access ReadOnly => new(CanRead: true, CanEdit: false, CanDelete: false);
    public static Access Full => new(CanRead: true, CanEdit: true, CanDelete: true);

    public bool Allows(Operation operation) => operation switch
    {
        Operation.Read => CanRead,
        Operation.Edit => CanEdit,
        Operation.Delete => CanDelete,
        _ => false,
    };

    /// <summary>
    /// Бросает <see cref="AccessDeniedException"/>, если операции нет. Не видно объекта — 404, а не 403:
    /// «нет такого» и «не твоё» неразличимы, перебирать чужие листы и журналы нельзя. Видно, но
    /// править нельзя — 403.
    /// </summary>
    public Access Demand(Operation operation)
    {
        if (Allows(operation))
        {
            return this;
        }

        throw CanRead ? AccessDeniedException.Forbidden() : AccessDeniedException.NotFound();
    }
}

/// <summary>Отказ в доступе из прикладного сервиса. API превращает его в ProblemDetails (<see cref="AccessExceptionHandler"/>).</summary>
public sealed class AccessDeniedException : Exception
{
    private AccessDeniedException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    /// <summary>Объекта нет или он не виден этому пользователю — 404.</summary>
    public static AccessDeniedException NotFound() =>
        new(StatusCodes.Status404NotFound, "Не найдено.");

    /// <summary>Объект виден, но операция не разрешена — 403.</summary>
    public static AccessDeniedException Forbidden() =>
        new(StatusCodes.Status403Forbidden, "Недостаточно прав.");
}

public static class AccessExtensions
{
    /// <summary><c>await policy.ForCharacterAsync(id).Demand(Operation.Edit)</c>.</summary>
    public static async Task<Access> Demand(this Task<Access> access, Operation operation) =>
        (await access).Demand(operation);

    /// <summary>Для разрешений без объекта (<c>CanCreate…</c>, <c>CanAdminister…</c>): отказ — 403.</summary>
    public static async Task Demand(this Task<bool> allowed)
    {
        if (!await allowed)
        {
            throw AccessDeniedException.Forbidden();
        }
    }
}
