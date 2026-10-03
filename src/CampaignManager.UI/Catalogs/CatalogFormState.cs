namespace CampaignManager.UI.Catalogs;

/// <summary>
/// Ошибки проверки формы справочника по полям (правило 9): страница держит один экземпляр, отдаёт его
/// <c>CatalogPage</c> (<c>State</c>) и читает в форме — <c>Error="@State.For("name")"</c> у <c>Field</c> и
/// <c>aria-invalid="@State.Invalid("name")"</c> у контрола (по этому признаку страница прокручивается к первому
/// полю с ошибкой). Заполняет его <c>CatalogPage</c> при нажатии «Сохранить» и пересчитывает при каждой правке,
/// пока попытка была.
/// </summary>
public sealed class CatalogFormState
{
    private Dictionary<string, string> _errors = [];

    /// <summary>Записи из разных эпох: только тогда форма спрашивает эпоху (решение владельца 2026-10-02).</summary>
    public bool ErasVary { get; internal set; }

    /// <summary>Сколько раз нажали «Сохранить» с ошибками — по приросту страница прокручивается к первой.</summary>
    public int Attempt { get; internal set; }

    /// <summary>Хотя бы раз пытались сохранить: пока нет, форма не краснеет.</summary>
    public bool Attempted { get; internal set; }

    public int Count => _errors.Count;

    public IReadOnlyList<string> Messages => [.. _errors.Values];

    /// <summary>Текст ошибки поля (для <c>Field Error</c>) или null.</summary>
    public string? For(string key) => _errors.GetValueOrDefault(key);

    /// <summary>«true» для <c>aria-invalid</c> или null (атрибут тогда не пишется).</summary>
    public string? Invalid(string key) => _errors.ContainsKey(key) ? "true" : null;

    internal void Set(IReadOnlyDictionary<string, string> errors) => _errors = new Dictionary<string, string>(errors);

    internal void Reset()
    {
        _errors = [];
        Attempted = false;
        Attempt = 0;
    }
}
