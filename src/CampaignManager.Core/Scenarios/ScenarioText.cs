namespace CampaignManager.Core.Scenarios;

/// <summary>
/// Подписи перечислений сценария — одна таблица на приложение (в v1 вид факта был переведён трижды, роль НПС — дважды).
/// Названия видов фактов — разделы подготовки сценария в книге Хранителя.
/// </summary>
public static class ScenarioText
{
    public static string Of(KeyFactType type) => type switch
    {
        KeyFactType.Backstory => "Предыстория",
        KeyFactType.Truth => "Зловещая истина",
        KeyFactType.Timeline => "Хронология",
        KeyFactType.Reward => "Награды",
        _ => type.ToString(),
    };

    public static string Of(NpcRole role) => role switch
    {
        NpcRole.Neutral => "Нейтрал",
        NpcRole.Enemy => "Враг",
        NpcRole.Ally => "Союзник",
        _ => role.ToString(),
    };

    public static string Of(CheckTarget target) => target switch
    {
        CheckTarget.Skill => "Навык",
        CheckTarget.Characteristic => "Характеристика",
        CheckTarget.Luck => "Удача",
        _ => target.ToString(),
    };
}
