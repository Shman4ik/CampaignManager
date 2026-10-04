namespace CampaignManager.UI.Shared;

/// <summary>
/// Короткое сообщение поверх страницы: «Сохранено», «Не удалось удалить». Рисует его
/// <see cref="ToastHost"/> тем же <see cref="Alert"/> — отдельного вида уведомлений нет. Заменяет
/// 42 поля <c>_error</c>/<c>_success</c>/<c>_notification</c> v1. Сообщение уходит само: через <see cref="AutoDismissAfter"/> (4 с), ошибка и тост с действием — через
/// <see cref="LongDismissAfter"/> (8 с: ошибку надо успеть прочитать, «Отменить» — успеть нажать). Сообщение, которое должно остаться
/// рядом с формой (ошибки проверки, правило книги), — обычный Alert на странице, не Toast.
/// </summary>
public sealed class ToastService(TimeProvider time)
{
    /// <summary>Больше на экране не помещается: старые уходят первыми.</summary>
    public const int MaxVisible = 4;

    public static readonly TimeSpan AutoDismissAfter = TimeSpan.FromSeconds(4);

    /// <summary>Ошибки и тосты с действием («Отменить»).</summary>
    public static readonly TimeSpan LongDismissAfter = TimeSpan.FromSeconds(8);

    private readonly List<ToastMessage> _messages = [];
    private long _nextId;

    public IReadOnlyList<ToastMessage> Messages => _messages;

    public event Action? Changed;

    /// <remarks>
    /// <paramref name="subject"/> — имя объекта отдельным параметром (правило 7): <c>Success("Кампания создана:", subject: name)</c>
    /// рисуется «Кампания создана: <b>Норман и сыновья</b>» — жирным и без обрамляющих кавычек (<see cref="Names.Clean"/>).
    /// <see cref="ToastMessage.Message"/> остаётся целым текстом («Кампания создана: Норман и сыновья»); имя в нём стоит последним.
    /// Строкой с кавычками по-прежнему можно, но имя в именительном падеже и жирным — единственный вид по правилу.
    /// </remarks>
    public void Success(string message, string? title = null, string? subject = null) => Show(Tone.Success, message, title, subject: subject);

    public void Info(string message, string? title = null, string? subject = null) => Show(Tone.Info, message, title, subject: subject);

    public void Warning(string message, string? title = null, string? subject = null) => Show(Tone.Warning, message, title, subject: subject);

    public void Error(string message, string? title = null, string? subject = null) => Show(Tone.Error, message, title, subject: subject);

    /// <summary>
    /// Тост с действием «Отменить», 8 секунд. Для правки данных листа в строке (убрали оружие, заклинание,
    /// фобию): строку убирают сразу, а вернуть можно отсюда. <paramref name="undo"/> вызывается по нажатию;
    /// если он падает, показывается ошибка.
    /// </summary>
    public void Undo(string message, Func<Task> undo, string? title = null, string? subject = null)
    {
        ArgumentNullException.ThrowIfNull(undo);
        Show(Tone.Info, message, title, new ToastAction("Отменить", undo), subject);
    }

    /// <summary>«Убрано: <b>Кольт .45</b> — Отменить»: <see cref="Undo"/> с готовым текстом.</summary>
    public void Removed(string subject, Func<Task> undo) => Undo("Убрано:", undo, subject: subject);

    public void Show(Tone tone, string message, string? title = null, ToastAction? action = null, string? subject = null)
    {
        var name = Names.Clean(subject);
        var toast = name.Length == 0
            ? new ToastMessage(++_nextId, tone, message, title, action)
            : new ToastMessage(++_nextId, tone, $"{message} {name}", title, action, name);
        _messages.Add(toast);
        if (_messages.Count > MaxVisible)
        {
            _messages.RemoveAt(0);
        }

        Changed?.Invoke();

        _ = DismissLaterAsync(toast.Id, tone == Tone.Error || action is not null ? LongDismissAfter : AutoDismissAfter);
    }

    /// <summary>Нажали действие тоста: тост закрывается, действие выполняется.</summary>
    public async Task RunActionAsync(long id)
    {
        var toast = _messages.Find(m => m.Id == id);
        if (toast?.Action is not { } action)
        {
            return;
        }

        Dismiss(id);
        try
        {
            await action.Callback();
        }
        catch (Exception)
        {
            Error("Не удалось отменить.");
        }
    }

    /// <summary>
    /// Убрать тосты с действием — при уходе на другую страницу (<see cref="ToastHost"/>): «Отменить» правит объект страницы,
    /// которой уже нет, и вернуть убранное некуда — после перехода кнопка молча ничего не сохраняла (оружие пропадало).
    /// </summary>
    public void DropActions()
    {
        if (_messages.RemoveAll(m => m.Action is not null) > 0)
        {
            Changed?.Invoke();
        }
    }

    public void Dismiss(long id)
    {
        if (_messages.RemoveAll(m => m.Id == id) > 0)
        {
            Changed?.Invoke();
        }
    }

    private async Task DismissLaterAsync(long id, TimeSpan after)
    {
        await Task.Delay(after, time);
        Dismiss(id);
    }
}

/// <param name="Message">Целый текст сообщения; если есть <paramref name="Subject"/>, он стоит в конце и выводится жирным.</param>
/// <param name="Subject">Имя объекта без кавычек (<see cref="Names.Clean"/>) — хвост <paramref name="Message"/>.</param>
public sealed record ToastMessage(long Id, Tone Tone, string Message, string? Title, ToastAction? Action = null, string? Subject = null)
{
    /// <summary>Текст до имени объекта: «Убрано:».</summary>
    public string Lead => Subject is { Length: > 0 } && Message.EndsWith(Subject, StringComparison.Ordinal)
        ? Message[..^Subject.Length]
        : Message;
}

/// <summary>Кнопка в тосте: подпись и что сделать по нажатию.</summary>
public sealed record ToastAction(string Label, Func<Task> Callback);
