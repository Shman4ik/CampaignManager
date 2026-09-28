using CampaignManager.Web.Components.Features.KeeperScreen.Model;

namespace CampaignManager.Web.Components.Features.KeeperScreen.Services;

/// <summary>
///     Открыта ли ширма и какой раздел в ней. Кнопка живёт в шапке страницы, а панель — в лэйауте:
///     это два разных интерактивных острова одного circuit, и общий у них только scoped-сервис
///     (так же устроены <c>MusicPlaybackService</c> и панель плеера).
///     <para>
///         Паузу circuit это состояние не переживает намеренно: после возобновления ширма просто
///         закрыта, а раздел — первый. Для закладки есть <c>/reference?block=…</c>.
///     </para>
/// </summary>
public sealed class KeeperScreenState
{
    public bool IsOpen { get; private set; }

    public KeeperScreenBlock ActiveBlock { get; private set; } = KeeperScreenBlock.Checks;

    /// <summary>Кампания групповой проверки: чтобы не выбирать её заново при каждом открытии.</summary>
    public Guid? GroupCheckCampaignId { get; set; }

    public event Action? Changed;

    public void Toggle()
    {
        IsOpen = !IsOpen;
        Changed?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        Changed?.Invoke();
    }

    public void Show(KeeperScreenBlock block)
    {
        ActiveBlock = block;
        IsOpen = true;
        Changed?.Invoke();
    }
}
