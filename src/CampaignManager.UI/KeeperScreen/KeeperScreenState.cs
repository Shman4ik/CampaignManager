using CampaignManager.UI.Checks;

namespace CampaignManager.UI.KeeperScreen;

/// <summary>
/// Открыта ли ширма, какой раздел в ней и черновик групповой проверки. Синглтон WebAssembly — это одна
/// вкладка браузера: кнопка в шапке, выезжающая панель и страница <c>/reference</c> делят одно состояние.
/// <para>
/// Перезагрузку вкладки оно не переживает, и не должно: ширма — справочник, а постоянный адрес раздела —
/// <c>/reference?block=…</c>. Заметки v1 про паузу circuit сюда не переехали — circuit'а больше нет.
/// </para>
/// </summary>
public sealed class KeeperScreenState
{
    public bool IsOpen { get; private set; }

    public KeeperScreenBlock ActiveBlock { get; private set; } = KeeperScreenBlock.Checks;

    /// <summary>Групповая проверка: сыщики, навык и броски — чтобы не вписывать заново при каждом открытии.</summary>
    public GroupCheckDraft GroupCheck { get; } = new();

    public event Action? Changed;

    public void Toggle()
    {
        IsOpen = !IsOpen;
        Changed?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        Changed?.Invoke();
    }

    /// <summary>Открыть раздел (в панели — открыв её).</summary>
    public void Show(KeeperScreenBlock block)
    {
        ActiveBlock = block;
        IsOpen = true;
        Changed?.Invoke();
    }

    /// <summary>Сменить раздел, не трогая видимость панели (страница <c>/reference</c>).</summary>
    public void Select(KeeperScreenBlock block)
    {
        if (ActiveBlock == block)
            return;

        ActiveBlock = block;
        Changed?.Invoke();
    }
}
