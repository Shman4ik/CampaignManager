using CampaignManager.Web.Components.Features.Characters.Model;
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
    [PersistentState]
    public CharacterDraft? PersistedDraft
    {
        get => Character is null ? null : new CharacterDraft { CharacterId = CharacterId, Character = Character };
        set => _restoredDraft = value;
    }

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
