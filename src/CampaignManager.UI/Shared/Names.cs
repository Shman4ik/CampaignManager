using Microsoft.AspNetCore.Components;

namespace CampaignManager.UI.Shared;

/// <summary>
/// Имя объекта в сообщениях — одна функция на подтверждения, тосты и ошибки API (правила 7 и 13): без
/// обрамляющих кавычек, жирным («Удалить оружие <b>Кольт «Миротворец»</b>?», не «««Кольт»»»), и шаблоны
/// строятся так, чтобы имя стояло в именительном падеже — библиотеки склонения нет и не будет (решение владельца
/// 2026-10-03): «Цель: Артур Нельсон — промах».
/// </summary>
public static class Names
{
    private static readonly (char Open, char Close)[] Quotes =
        [('«', '»'), ('"', '"'), ('“', '”'), ('„', '“'), ('‘', '’'), ('\'', '\''), ('”', '”')];

    /// <summary>
    /// Имя без обрамляющих кавычек: «Кольт» → Кольт. Кавычки внутри («Кольт «Миротворец»») остаются — это
    /// часть названия; снимается только пара, охватывающая всё имя.
    /// </summary>
    public static string Clean(string? name)
    {
        var text = (name ?? "").Trim();
        var changed = true;
        while (changed && text.Length >= 2)
        {
            changed = false;
            foreach (var (open, close) in Quotes)
            {
                if (text[0] == open && text[^1] == close && Encloses(text, open, close))
                {
                    text = text[1..^1].Trim();
                    changed = true;
                    break;
                }
            }
        }

        return text;
    }

    /// <summary>Имя для текста без разметки (тост, заголовок окна, <c>aria-label</c>, ApiErrors).</summary>
    public static string Plain(string? name) => Clean(name);

    /// <summary>Имя жирным для тела окна и абзацев: <c>@Names.Strong(item.Name)</c>.</summary>
    public static RenderFragment Strong(string? name) => builder =>
    {
        builder.OpenElement(0, "strong");
        builder.AddContent(1, Clean(name));
        builder.CloseElement();
    };

    /// <summary>«Цель: Артур Нельсон — промах» — метка, имя в именительном, итог после тире.</summary>
    public static string Labelled(string label, string? name, string? outcome = null) =>
        outcome is { Length: > 0 } ? $"{label}: {Clean(name)} — {outcome}" : $"{label}: {Clean(name)}";

    // «Кольт» «Миротворец» — это два имени в кавычках, а не одно в обрамлении: пара должна замыкаться только в конце.
    private static bool Encloses(string text, char open, char close)
    {
        if (open == close)
        {
            return text.IndexOf(open, 1) == text.Length - 1;
        }

        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == open)
            {
                depth++;
            }
            else if (text[i] == close)
            {
                depth--;
                if (depth == 0 && i < text.Length - 1)
                {
                    return false;
                }
            }
        }

        return depth == 0;
    }
}
