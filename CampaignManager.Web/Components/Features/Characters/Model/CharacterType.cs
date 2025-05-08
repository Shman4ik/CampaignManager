namespace CampaignManager.Web.Model;

/// <summary>
///     Defines the type of character in the system
/// </summary>
public enum CharacterType
{
    /// <summary>
    ///     Player character controlled by a player
    /// </summary>
    PlayerCharacter = 0,

    /// <summary>
    ///     Non-player character controlled by the game master
    /// </summary>
    NonPlayerCharacter = 1
}