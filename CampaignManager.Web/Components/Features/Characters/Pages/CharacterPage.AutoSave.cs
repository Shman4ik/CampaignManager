using System.Security.Cryptography;
using System.Text.Json;
using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Characters.Pages;

/// <summary>
///     Автосохранение листа персонажа.
/// </summary>
public partial class CharacterPage : IAsyncDisposable
{
    /// <summary>Как часто лист сверяется с сохранённым и уходит в базу.</summary>
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromSeconds(3);

    /// <summary>Слепок делаем без отступов: он нужен только для сравнения «изменилось или нет».</summary>
    private static readonly JsonSerializerOptions SnapshotOptions = new() { WriteIndented = false };

    private CancellationTokenSource? _autoSaveCts;

    /// <summary>Хеш последнего состояния листа, про которое точно известно, что оно в базе.</summary>
    private string? _savedSnapshot;

    private AutoSaveState _autoSaveState = AutoSaveState.Idle;

    private enum AutoSaveState
    {
        /// <summary>С момента открытия ничего не сохраняли — показывать нечего.</summary>
        Idle,
        Saving,
        Saved,
        Failed
    }

    /// <summary>
    ///     Автосохранение работает только для уже существующего листа: у нового ещё нет строки,
    ///     а владельца (кампанию, сценарий) выбирают руками перед первым сохранением.
    /// </summary>
    private bool CanAutoSave => Character is not null && CharacterStorageDto is not null;

    private sealed record SaveIndicator(string Icon, string Text, string CssClass);

    /// <summary>Значок в шапке: Хранитель должен видеть, что правки уходят в базу сами.</summary>
    private SaveIndicator? AutoSaveIndicator => _autoSaveState switch
    {
        AutoSaveState.Saving => new SaveIndicator("fa-solid fa-rotate fa-spin", "Сохранение…", "text-gray-500"),
        AutoSaveState.Saved => new SaveIndicator("fa-solid fa-check", "Сохранено", "text-emerald-600"),
        AutoSaveState.Failed => new SaveIndicator("fa-solid fa-triangle-exclamation", "Не сохранено", "text-red-600"),
        _ => null
    };

    /// <summary>
    ///     Слепок листа. Считаем по JSON, потому что в базе он и лежит как JSON: любая правка
    ///     любого поля — в характеристиках, навыках, оружии, биографии — меняет слепок, и
    ///     страницу не приходится обвешивать колбэками «я изменился» на каждый компонент.
    /// </summary>
    private static string Snapshot(Character character) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(character, SnapshotOptions)));

    /// <summary>Запомнить, что текущее состояние листа уже в базе.</summary>
    private void MarkSaved()
    {
        if (Character is not null)
            _savedSnapshot = Snapshot(Character);
    }

    private void StartAutoSaveLoop()
    {
        _autoSaveCts = new CancellationTokenSource();
        _ = RunAutoSaveLoopAsync(_autoSaveCts.Token);
    }

    /// <summary>
    ///     Опрос, а не подписка на изменения: поля листа правятся через обычный <c>@bind</c>
    ///     внутри дочерних компонентов (оружие, снаряжение, биография), и родительская страница
    ///     об этих правках ничего не узнаёт — ни события, ни лишнего рендера у неё не случается.
    /// </summary>
    private async Task RunAutoSaveLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(AutoSaveInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(token))
                await InvokeAsync(AutoSaveAsync);
        }
        catch (OperationCanceledException)
        {
            // Страница закрыта — это штатное завершение цикла.
        }
        catch (ObjectDisposedException)
        {
            // Circuit исчез между тиком и InvokeAsync.
        }
    }

    private async Task AutoSaveAsync()
    {
        // _isBusy занят ручным сохранением: два UPDATE одной строки подряд не нужны.
        if (!CanAutoSave || _isBusy)
            return;

        var snapshot = Snapshot(Character!);
        if (snapshot == _savedSnapshot)
            return;

        _isBusy = true;
        var wasFailed = _autoSaveState is AutoSaveState.Failed;
        _autoSaveState = AutoSaveState.Saving;
        StateHasChanged();

        try
        {
            await CharacterService.UpdateCharacterAsync(Character!);
            _savedSnapshot = snapshot;
            _autoSaveState = AutoSaveState.Saved;
        }
        catch (Exception ex)
        {
            _autoSaveState = AutoSaveState.Failed;
            Logger.LogError(ex, "Автосохранение листа {CharacterId} не удалось", CharacterId);

            // Одно уведомление на серию неудач: тикаем раз в три секунды, и повторять его
            // каждый тик значило бы держать Хранителю на экране незакрываемую красную плашку.
            if (!wasFailed)
                ShowNotification($"Не удалось сохранить лист автоматически: {ex.Message}", "error");
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_autoSaveCts is null)
            return;

        await _autoSaveCts.CancelAsync();
        _autoSaveCts.Dispose();
        _autoSaveCts = null;
    }
}
