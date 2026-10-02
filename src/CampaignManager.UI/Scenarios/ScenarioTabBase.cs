using CampaignManager.Contracts.Scenarios;
using CampaignManager.UI.Platform;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;

namespace CampaignManager.UI.Scenarios;

/// <summary>
/// Общее у вкладок рабочего места: сценарий, «перечитай» для страницы, удаление строки с подтверждением, перестановка
/// строк. Запись — своим адресом API на строку; после неё страница перечитывает сценарий целиком (один запрос), так что
/// вкладка ничего не держит в копии и правка соседней вкладки до неё доходит.
/// </summary>
public abstract class ScenarioTabBase : ComponentBase
{
    [Inject] protected IScenariosApi Api { get; set; } = null!;

    [Inject] protected DialogService Dialogs { get; set; } = null!;

    [Inject] protected ToastService Toasts { get; set; } = null!;

    [Parameter, EditorRequired] public ScenarioDto Scenario { get; set; } = null!;

    /// <summary>Строка записана — страница перечитывает сценарий.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    /// <summary>Режим «Порядок»: вместо «Изменить»/«Удалить» — «Выше»/«Ниже».</summary>
    protected bool Reordering { get; set; }

    /// <summary>Идёт запись из строки (удаление, перестановка): кнопки строк выключены.</summary>
    protected bool Busy { get; private set; }

    protected bool CanEdit => Scenario.CanEdit;

    /// <summary>
    /// Удалить строку после подтверждения; отказ — тостом. <paramref name="confirmText"/> «Убрать» — связь, а не запись
    /// (НПС, тварь, предмет остаются в библиотеке и справочниках): тогда без «действие нельзя отменить».
    /// </summary>
    protected async Task DeleteAsync(string title, string message, Func<Task> delete, string done, string confirmText = "Удалить")
    {
        var request = confirmText == "Удалить"
            ? new ConfirmRequest(title, message)
            : new ConfirmRequest(title, message) { ConfirmText = confirmText, Details = null };
        if (!await Dialogs.ConfirmAsync(request))
            return;

        await RunAsync(delete, done);
    }

    /// <summary>
    /// Сдвинуть строку на <paramref name="delta"/> среди <paramref name="siblings"/> (соседи по порядку) и записать
    /// новый порядок всей группы.
    /// </summary>
    protected Task MoveAsync(ScenarioPart part, IReadOnlyList<Guid> siblings, Guid id, int delta)
    {
        var order = siblings.ToList();
        var from = order.IndexOf(id);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= order.Count)
            return Task.CompletedTask;

        (order[from], order[to]) = (order[to], order[from]);
        return RunAsync(() => Api.ReorderAsync(Scenario.Id, new ReorderRequest(part, order)), null);
    }

    protected async Task RunAsync(Func<Task> action, string? done)
    {
        if (Busy)
            return;

        Busy = true;
        try
        {
            await action();
            if (done is not null)
                Toasts.Success(done);
            await OnChanged.InvokeAsync();
        }
        catch (HttpRequestException ex)
        {
            Toasts.Error(ApiErrors.Describe(ex));
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Модалка записала строку: закрыть её делает вкладка, перечитать — страница.</summary>
    protected Task SavedAsync(string done)
    {
        Toasts.Success(done);
        return OnChanged.InvokeAsync();
    }
}
