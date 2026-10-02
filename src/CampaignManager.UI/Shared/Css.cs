namespace CampaignManager.UI.Shared;

/// <summary>
/// Склейка классов и чужих атрибутов. Компонент кита принимает любые атрибуты
/// (<c>CaptureUnmatchedValues</c>): без этого лишний <c>id</c> или <c>data-testid</c> компилируется,
/// а падает в рантайме — ровно когда компонент наконец показали. Переданный <c>class</c>
/// подмешивается к классам компонента, а не затирает их; утилита в нём перебивает компонентный
/// класс без «!» (слои Tailwind).
/// </summary>
internal static class Css
{
    public static string Join(params string?[] classes) =>
        string.Join(' ', classes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()));

    public static string? ClassOf(IReadOnlyDictionary<string, object>? attributes) =>
        attributes is not null && attributes.TryGetValue("class", out var value) ? value?.ToString() : null;

    public static IReadOnlyDictionary<string, object>? WithoutClass(IReadOnlyDictionary<string, object>? attributes) =>
        attributes is null || !attributes.ContainsKey("class")
            ? attributes
            : attributes.Where(a => a.Key != "class").ToDictionary(a => a.Key, a => a.Value);
}
