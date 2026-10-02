using System.Globalization;
using CampaignManager.Core.Dice;

namespace CampaignManager.UI.Shared;

/// <summary>
/// Как показывать уровень успеха: тон метки и подпись с заглавной. Подпись — <see cref="RulesText"/> (Core),
/// здесь только оформление; им пользуются <c>RollInput</c>, диалог проверки и групповая проверка.
/// </summary>
public static class LevelText
{
    /// <summary>Провал и крах — ошибка; успех ниже нужной сложности — внимание; остальное — успех.</summary>
    public static Tone ToneOf(SuccessLevel level, bool passes) => level switch
    {
        SuccessLevel.Fumble or SuccessLevel.Failure => Tone.Error,
        _ when !passes => Tone.Warning,
        _ => Tone.Success,
    };

    /// <summary>Плотная заливка — только у критического успеха и краха: их нельзя пропустить глазом.</summary>
    public static bool IsSolid(SuccessLevel level) => level is SuccessLevel.Critical or SuccessLevel.Fumble;

    /// <summary>«Трудный успех» — с заглавной, для метки и крупной подписи.</summary>
    public static string Title(SuccessLevel level) => Capitalize(RulesText.Of(level));

    public static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
}
