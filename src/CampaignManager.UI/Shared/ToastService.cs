namespace CampaignManager.UI.Shared;

/// <summary>
/// Короткое сообщение поверх страницы: «Сохранено», «Не удалось удалить». Рисует его
/// <see cref="ToastHost"/> тем же <see cref="Alert"/> — отдельного вида уведомлений нет. Заменяет
/// 42 поля <c>_error</c>/<c>_success</c>/<c>_notification</c> v1. Ошибка висит, пока её не закроют;
/// остальное уходит само через <see cref="AutoDismissAfter"/>. Сообщение, которое должно остаться
/// рядом с формой (ошибки проверки, правило книги), — обычный Alert на странице, не Toast.
/// </summary>
public sealed class ToastService(TimeProvider time)
{
    /// <summary>Больше на экране не помещается: старые уходят первыми.</summary>
    public const int MaxVisible = 4;

    public static readonly TimeSpan AutoDismissAfter = TimeSpan.FromSeconds(5);

    private readonly List<ToastMessage> _messages = [];
    private long _nextId;

    public IReadOnlyList<ToastMessage> Messages => _messages;

    public event Action? Changed;

    public void Success(string message, string? title = null) => Show(Tone.Success, message, title);

    public void Info(string message, string? title = null) => Show(Tone.Info, message, title);

    public void Warning(string message, string? title = null) => Show(Tone.Warning, message, title);

    public void Error(string message, string? title = null) => Show(Tone.Error, message, title);

    public void Show(Tone tone, string message, string? title = null)
    {
        var toast = new ToastMessage(++_nextId, tone, message, title);
        _messages.Add(toast);
        if (_messages.Count > MaxVisible)
        {
            _messages.RemoveAt(0);
        }

        Changed?.Invoke();

        if (tone != Tone.Error)
        {
            _ = DismissLaterAsync(toast.Id);
        }
    }

    public void Dismiss(long id)
    {
        if (_messages.RemoveAll(m => m.Id == id) > 0)
        {
            Changed?.Invoke();
        }
    }

    private async Task DismissLaterAsync(long id)
    {
        await Task.Delay(AutoDismissAfter, time);
        Dismiss(id);
    }
}

public sealed record ToastMessage(long Id, Tone Tone, string Message, string? Title);
