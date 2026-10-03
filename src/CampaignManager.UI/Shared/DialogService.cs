namespace CampaignManager.UI.Shared;

/// <summary>
/// Подтверждение действия: <c>if (await Dialogs.ConfirmAsync(…)) { … }</c>. Окно рисует
/// <see cref="DialogHost"/> в оболочке тем же <see cref="Modal"/>, что и любые диалоги. В v1 на это
/// уходило ~25 строк на каждый из 15 сценариев удаления (флаги <c>_showDelete*</c> — 41 раз в
/// 9 файлах), а свой <c>ConfirmationModal</c> по Enter синхронно ждал задачу на рендерере.
/// </summary>
public sealed class DialogService
{
    private TaskCompletionSource<bool>? _pending;

    /// <summary>Открытый запрос; <c>null</c> — окна нет.</summary>
    public ConfirmRequest? Current { get; private set; }

    /// <summary>Открылось или закрылось окно — для <see cref="DialogHost"/>.</summary>
    public event Action? Changed;

    /// <summary>
    /// Показывает окно и ждёт ответа: <c>true</c> — подтвердили, <c>false</c> — отмена, крестик,
    /// Esc или подложка. Новый запрос поверх открытого отвечает открытому «нет».
    /// </summary>
    public Task<bool> ConfirmAsync(ConfirmRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _pending?.TrySetResult(false);

        _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Current = request;
        Changed?.Invoke();
        return _pending.Task;
    }

    /// <summary>Короткая запись для удаления: «Удалить существо?» / «Глубоководный будет удалён.»</summary>
    public Task<bool> ConfirmDeleteAsync(string title, string message) =>
        ConfirmAsync(new ConfirmRequest(title, message));

    /// <summary>
    /// Удаление с телом «объект + последствие + можно ли вернуть»: заголовок «Удалить оружие?», ниже жирным
    /// имя без обрамляющих кавычек, потом последствие и «Можно вернуть.» / «Нельзя отменить.». Кнопка
    /// называет действие: «Удалить оружие», а не «Подтвердить». <paramref name="noun"/> — существительное
    /// в винительном падеже: «оружие», «сценарий», «трек». <paramref name="reversible"/> без значения по
    /// умолчанию: «нельзя отменить» пишут, только если это правда.
    /// </summary>
    public Task<bool> ConfirmDeleteAsync(string noun, string subject, string consequence, bool reversible) =>
        ConfirmAsync(new ConfirmRequest($"Удалить {noun}?", consequence)
        {
            Subject = subject,
            ConfirmText = $"Удалить {noun}",
            Details = reversible ? "Можно вернуть." : "Нельзя отменить.",
        });

    /// <summary>Ответ из окна.</summary>
    public void Complete(bool confirmed)
    {
        var pending = _pending;
        _pending = null;
        Current = null;
        Changed?.Invoke();
        pending?.TrySetResult(confirmed);
    }
}

/// <summary>Что спросить. По умолчанию — необратимое удаление: красная кнопка «Удалить».</summary>
public sealed record ConfirmRequest(string Title, string Message)
{
    public string ConfirmText { get; init; } = "Удалить";

    public string CancelText { get; init; } = "Отмена";

    /// <summary>Error — необратимое (удалить, исключить); Primary — обычное подтверждение.</summary>
    public ButtonVariant ConfirmVariant { get; init; } = ButtonVariant.Error;

    /// <summary>Пояснение мельче под вопросом: «Это действие нельзя отменить.» / «Можно вернуть.»</summary>
    public string? Details { get; init; } = "Это действие нельзя отменить.";

    private string? _subject;

    /// <summary>
    /// Объект действия: выводится жирной строкой над <see cref="Message"/>, без обрамляющих кавычек
    /// («Кольт «Миротворец»» остаётся как есть, а «Кольт» превращается в Кольт). Форматирование имени
    /// здесь минимальное; общая точка для подтверждений, тостов и ошибок API — <c>Names</c> (UX-0b).
    /// </summary>
    public string? Subject
    {
        get => _subject;
        init => _subject = StripWrappingQuotes(value);
    }

    private static string? StripWrappingQuotes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        while (text.Length >= 2 && IsWrapping(text[0], text[^1]) && !text[1..^1].Contains(text[0]))
        {
            text = text[1..^1].Trim();
        }

        return text;
    }

    private static bool IsWrapping(char first, char last) =>
        (first, last) is ('«', '»') or ('"', '"') or ('“', '”') or ('\'', '\'') or ('‘', '’');
}
