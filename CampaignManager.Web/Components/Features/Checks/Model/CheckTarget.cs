namespace CampaignManager.Web.Components.Features.Checks.Model;

/// <summary>Что проверяется — от этого зависят повторная проверка, Удача и отметка развития.</summary>
public enum CheckTargetKind
{
    /// <summary>Навык с листа: его можно отметить для фазы развития.</summary>
    Skill,

    /// <summary>Характеристика (СИЛ, ЛВК…): повторять можно, отмечать нечего.</summary>
    Characteristic,

    /// <summary>Удача: ни повтора, ни траты пунктов Удачи на неё (стр. 83, 97).</summary>
    Luck,

    /// <summary>Число, вписанное Хранителем руками, без листа за ним.</summary>
    Other
}

/// <summary>
///     Цель проверки: вид, подпись и значение, против которого бросают d100.
///     У характеристики <see cref="Name" /> — сокращение («СИЛ»), у навыка — имя ровно как на листе.
/// </summary>
public sealed record CheckTarget(CheckTargetKind Kind, string Name, int Value)
{
    /// <summary>Ключ для выпадающего списка: имена навыков произвольны, поэтому вид идёт префиксом.</summary>
    public string Key => KeyFor(Kind, Name);

    public static string KeyFor(CheckTargetKind kind, string name) => kind switch
    {
        CheckTargetKind.Skill => "skill:" + name,
        CheckTargetKind.Characteristic => "char:" + name,
        CheckTargetKind.Luck => "luck",
        _ => "other"
    };
}
