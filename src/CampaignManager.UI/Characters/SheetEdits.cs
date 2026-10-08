using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Platform;

namespace CampaignManager.UI.Characters;

/// <summary>
/// Правка чужого листа по id — из сцены (подобранное в бою оружие) и из итогов сценария (награды партии): свежий лист, правка,
/// запись с <c>If-Match</c>; 409 (лист записали между чтением и записью — игрок правит свой) — перечитать и повторить поверх
/// чужой правки. Правка должна быть приращением (прибавить, отнять), а не «поставить значение»: её применяют к свежему листу.
/// </summary>
public static class SheetEdits
{
    /// <summary>Сколько раз перечитать лист при конфликте версий.</summary>
    public const int MaxAttempts = 3;

    /// <summary>Записанный лист или текст ошибки.</summary>
    public static async Task<(CharacterSheet? Sheet, string? Error)> EditAsync(ICharactersApi characters, Guid characterId,
        Action<CharacterSheet> edit, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var character = await characters.GetAsync(characterId, cancellationToken);
                edit(character.Sheet);
                await characters.SaveSheetAsync(characterId, character.Sheet, character.Version, cancellationToken);
                return (character.Sheet, null);
            }
            catch (ApiException error) when (error.IsStale && attempt < MaxAttempts)
            {
                // Лист записали между чтением и записью — правка ляжет поверх свежей версии.
            }
            catch (HttpRequestException error)
            {
                return (null, ApiErrors.Describe(error));
            }
        }

        return (null, "Лист всё время меняется на другом устройстве — попробуйте ещё раз.");
    }
}
