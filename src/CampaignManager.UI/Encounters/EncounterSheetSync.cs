using System.Net;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using CampaignManager.UI.Characters;
using CampaignManager.UI.Platform;

namespace CampaignManager.UI.Encounters;

/// <summary>Итог одной записи в лист.</summary>
/// <param name="Sheet">Записанный лист — из него обновится снимок участника; null — не записано.</param>
/// <param name="Lines">Что изменилось в листе («ПЗ 12 → 7, серьёзная рана»).</param>
/// <param name="Error">Почему не записано; null — записано.</param>
/// <param name="Permanent">Повтор не поможет (листа нет, нет прав) — Хранитель решает «не записывать».</param>
public sealed record SheetWriteResult(CharacterSheet? Sheet, IReadOnlyList<string> Lines, string? Error, bool Permanent);

/// <summary>
/// Запись итогов сцены в листы — через API листа (T2.3), с его версиями. Эффект — приращение, поэтому порядок такой:
/// прочитать свежий лист (версия), применить эффекты правилами Core (<see cref="EncounterSheetEffects"/>), записать с
/// <c>If-Match</c>. Лист изменили между чтением и записью (409 <c>stale</c>: игрок правит свой лист, у Хранителя открыта
/// вторая вкладка) — перечитать и применить ещё раз поверх чужой правки. Так ни урон, ни правка игрока не теряются;
/// открытый у игрока лист при следующем автосохранении получит свой 409 и плашку «перечитать» (T2.3).
/// <para>
/// Очередь — в документе сцены (<see cref="EncounterState.SheetWrites"/>): перезагрузка или обрыв связи посреди записи не
/// теряют эффект. Окно «лист записан, а сцена ещё нет» — один запрос: тогда эффект при повторе ляжет второй раз (см.
/// <c>UI/Encounters/CLAUDE.md</c>).
/// </para>
/// </summary>
public sealed class EncounterSheetSync(ICharactersApi characters)
{
    /// <summary>Сколько раз перечитать лист при конфликте версий, прежде чем отложить запись.</summary>
    public const int MaxAttempts = 3;

    public async Task<SheetWriteResult> WriteAsync(SheetWrite write, SkillCatalog catalog, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var character = await characters.GetAsync(write.CharacterId, cancellationToken);
                var sheet = character.Sheet;
                var lines = EncounterSheetEffects.Apply(sheet, catalog, write.Effects);
                await characters.SaveSheetAsync(write.CharacterId, sheet, character.Version, cancellationToken);
                return new SheetWriteResult(sheet, lines, null, Permanent: false);
            }
            catch (ApiException error) when (error.IsStale && attempt < MaxAttempts)
            {
                // Лист записали между чтением и записью — эффект ляжет поверх свежей версии.
            }
            catch (ApiException error) when (error.IsStale)
            {
                return new SheetWriteResult(null, [], "Лист всё время меняется на другом устройстве — запись повторится.", Permanent: false);
            }
            catch (HttpRequestException error)
            {
                var permanent = error.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest;
                return new SheetWriteResult(null, [], ApiErrors.Describe(error), permanent);
            }
        }

        return new SheetWriteResult(null, [], "Не удалось записать в лист.", Permanent: false);
    }

    /// <summary>
    /// Правка листа сыщика из сцены (подобранное в бою оружие — в снаряжение, «Отменить» — убрать): свежий лист, правка,
    /// запись с <c>If-Match</c>; 409 — перечитать и повторить. Возвращает записанный лист (по нему обновляется снимок) или
    /// текст ошибки.
    /// </summary>
    public Task<(CharacterSheet? Sheet, string? Error)> EditAsync(Guid characterId, Action<CharacterSheet> edit, CancellationToken cancellationToken = default) =>
        SheetEdits.EditAsync(characters, characterId, edit, cancellationToken);

    /// <summary>
    /// Пишет всё из очереди сцены: записанное снимается с очереди (снимок участника — из записанного листа), остальное
    /// остаётся с текстом ошибки. Возвращает true, если состояние сцены изменилось.
    /// </summary>
    public async Task<bool> FlushAsync(EncounterState state, SkillCatalog catalog, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var changed = false;
        foreach (var write in state.SheetWrites.Where(w => !w.Blocked).ToList())
        {
            var result = await WriteAsync(write, catalog, cancellationToken);
            if (result.Sheet is { } sheet)
                EncounterEngine.CompleteSheetWrite(state, write.Id, sheet, catalog, result.Lines, now);
            else
                EncounterEngine.FailSheetWrite(state, write.Id, result.Error ?? "Не удалось записать в лист.", result.Permanent);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Числа участников из их листов — при открытии сцены: лист мог поправить игрок или другая вкладка. Не прочитался
    /// (удалён, нет прав) — остаётся снимок. Возвращает true, если что-то изменилось.
    /// </summary>
    public async Task<bool> RefreshAsync(EncounterState state, SkillCatalog catalog, CancellationToken cancellationToken = default)
    {
        var changed = false;
        foreach (var participant in state.Participants.Where(p => p.SourceCharacterId is not null).ToList())
        {
            try
            {
                var character = await characters.GetAsync(participant.SourceCharacterId!.Value, cancellationToken);
                var before = participant with { Stats = participant.Stats with { } };
                EncounterParticipants.Refresh(participant, character.Sheet, catalog);
                changed |= before != participant;
            }
            catch (HttpRequestException)
            {
                // Снимок остаётся: сцена важнее листа, который сейчас не прочитать.
            }
        }

        return changed;
    }
}
