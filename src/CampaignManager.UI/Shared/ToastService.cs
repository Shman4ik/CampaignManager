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

    public void Success(string message, string? title = null) => Show(Tone.Success, message, title);

    public void Info(string message, string? title = null) => Show(Tone.Info, message, title);

    public void Warning(string message, string? title = null) => Show(Tone.Warning, message, title);

    public void Error(string message, string? title = null) => Show(Tone.Error, message, title);

    /// <summary>
    /// Тост с действием «Отменить», 8 секунд. Для правки данных листа в строке (убрали оружие, заклинание,
    /// фобию): строку убирают сразу, а вернуть можно отсюда. <paramref name="undo"/> вызывается по нажатию;
    /// если он падает, показывается ошибка.
    /// </summary>
    public void Undo(string message, Func<Task> undo, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(undo);
        Show(Tone.Info, message, title, new ToastAction("Отменить", undo));
    }

    /// <summary>«Убрано: Кольт .45 — Отменить»: <see cref="Undo"/> с готовым текстом.</summary>
    public void Removed(string subject, Func<Task> undo) => Undo($"Убрано: {subject}", undo);

    public void Show(Tone tone, string message, string? title = null, ToastAction? action = null)
    {
        var toast = new ToastMessage(++_nextId, tone, message, title, action);
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

public sealed record ToastMessage(long Id, Tone Tone, string Message, string? Title, ToastAction? Action = null);

/// <summary>Кнопка в тосте: подпись и что сделать по нажатию.</summary>
public sealed record ToastAction(string Label, Func<Task> Callback);
