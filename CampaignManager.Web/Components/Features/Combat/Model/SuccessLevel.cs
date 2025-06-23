namespace CampaignManager.Web.Components.Features.Combat.Model;

public enum SuccessLevel
{
    Fumble = 0,
    Failure = 1,
    RegularSuccess = 2,
    HardSuccess = 3,
    ExtremeSuccess = 4,
    CriticalSuccess = 5
}

public static class SuccessLevelExtensions
{
    public static bool IsSuccess(this SuccessLevel level)
    {
        return level >= SuccessLevel.RegularSuccess;
    }

    public static bool BeatsLevel(this SuccessLevel attackerLevel, SuccessLevel defenderLevel)
    {
        return attackerLevel > defenderLevel;
    }

    public static string GetDisplayName(this SuccessLevel level)
    {
        return level switch
        {
            SuccessLevel.Fumble => "Провал",
            SuccessLevel.Failure => "Неудача",
            SuccessLevel.RegularSuccess => "Обычный успех",
            SuccessLevel.HardSuccess => "Сложный успех",
            SuccessLevel.ExtremeSuccess => "Экстремальный успех",
            SuccessLevel.CriticalSuccess => "Критический успех",
            _ => "Неизвестно"
        };
    }
}