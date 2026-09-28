using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Model;
using Microsoft.AspNetCore.Components;

namespace CampaignManager.Web.Components.Features.Characters.Pages;

/// <summary>
///     Несохранённая правка листа, переживающая паузу circuit.
/// </summary>
public sealed class CharacterDraft
{
    /// <summary>Лист, к которому относится черновик; <c>null</c> — ещё не сохранённый новый лист.</summary>
    public Guid? CharacterId { get; set; }

    public Character? Character { get; set; }
}

/// <summary>
///     Лист, прочитанный в пререндере, — снимок для первого интерактивного рендера, чтобы тот не
///     читал из базы то же самое второй раз. Не черновик: здесь копия базы, а не правка.
///     <para>
///         <c>SavedSnapshot</c> — слепок автосохранения, снятый с листа <b>до</b>
///         <c>NormalizeLuckCap</c>: иначе правка потолка Удачи у старого листа сочлась бы
///         «уже сохранённой» и в базу не ушла. <c>Player</c> — копия слота без навигаций (у
///         загруженного <c>CampaignPlayer</c> граф через <c>Campaign.Players</c> циклический).
///     </para>
/// </summary>
public sealed record CharacterSheetPrerender(
    Guid CharacterId,
    CharacterStorageDto Storage,
    CampaignPlayer? Player,
    string? SavedSnapshot);

public partial class CharacterPage
{
    /// <summary>
    ///     Точка, за которую Blazor держит открытый лист (см. корневой CLAUDE.md, «Circuit State
    ///     Persistence»). Геттер вызывается при постановке circuit на паузу, сеттер — при
    ///     возобновлении, до <c>OnInitializedAsync</c>.
    ///     <para>
    ///     Без этого свойства правки на листе жили только в памяти circuit: страница собирается
    ///     заново, а восстанавливается лишь помеченное <c>[PersistentState]</c>. Уход на другую
    ///     вкладку, сон планшета или деплой — и несохранённые изменения навыков просто исчезали,
    ///     лист перечитывался из базы.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     Только пауза: в пререндере правок ещё нет, поэтому геттер там молчит, а
    ///     <c>SkipInitialValue</c> не поднимает черновик при старте circuit. Лист из пререндера
    ///     едет отдельно — <see cref="PrerenderedSheet" />.
    /// </remarks>
    [PersistentState(RestoreBehavior = RestoreBehavior.SkipInitialValue)]
    public CharacterDraft? PersistedDraft
    {
        get => !RendererInfo.IsInteractive || Character is null
            ? null
            : new CharacterDraft { CharacterId = CharacterId, Character = Character };
        set => _restoredDraft = value;
    }

    /// <summary>
    ///     Лист из пререндера (см. корневой CLAUDE.md, «Данные пререндера»). Отдаётся только из
    ///     статического рендера и только у существующего листа; <c>SkipLastSnapshot</c> не даёт
    ///     поднять его после паузы — там лист перечитывается из базы, а правки несёт черновик.
    /// </summary>
    [PersistentState(RestoreBehavior = RestoreBehavior.SkipLastSnapshot)]
    public CharacterSheetPrerender? PrerenderedSheet
    {
        get => RendererInfo.IsInteractive || CharacterStorageDto is null || CharacterId is not { } id
            ? null
            : new CharacterSheetPrerender(id, CharacterStorageDto, DetachedPlayer(CampaignPlayer), _savedSnapshot);
        set => _prerenderedSheet = value;
    }

    private CharacterSheetPrerender? _prerenderedSheet;

    /// <summary>Снимок пререндера, если он от этого же листа; берётся один раз.</summary>
    private CharacterSheetPrerender? TakePrerenderedSheet()
    {
        var sheet = _prerenderedSheet;
        _prerenderedSheet = null;
        return sheet is not null && sheet.CharacterId == CharacterId ? sheet : null;
    }

    /// <summary>Слот игрока без навигаций: странице нужны только его идентификатор и имя.</summary>
    private static CampaignPlayer? DetachedPlayer(CampaignPlayer? player) => player is null
        ? null
        : new CampaignPlayer
        {
            Id = player.Id,
            CreatedAt = player.CreatedAt,
            LastUpdated = player.LastUpdated,
            CampaignId = player.CampaignId,
            PlayerEmail = player.PlayerEmail,
            PlayerName = player.PlayerName
        };

    private CharacterDraft? _restoredDraft;

    /// <summary>
    ///     Возвращает на страницу черновик, если он от этого же листа. Чужой (успели открыть
    ///     другого персонажа) молча выбрасывается — иначе лист подменится не тем.
    /// </summary>
    private bool TryRestoreDraft()
    {
        var draft = _restoredDraft;
        _restoredDraft = null;

        if (draft?.Character is null || draft.CharacterId != CharacterId)
            return false;

        Character = draft.Character;
        if (CharacterStorageDto is not null)
            CharacterStorageDto.Character = draft.Character;

        return true;
    }
}
