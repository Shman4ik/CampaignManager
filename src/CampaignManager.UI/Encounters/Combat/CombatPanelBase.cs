using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using Microsoft.AspNetCore.Components;

namespace CampaignManager.UI.Encounters.Combat;

/// <summary>
/// Общее у панелей боя (T2.6b): открытая сцена каскадом (<see cref="EncounterSession"/>), кости, «кто действует» по умолчанию
/// — тот, чей ход (знание v1: панель следовала за активным участником). Панель правил не считает: собирает настройку,
/// зовёт <see cref="CombatRules"/> и кладёт результат на предпросмотр — применяет его «Применить» страницы.
/// </summary>
public abstract class CombatPanelBase : ComponentBase, IDisposable
{
    private EncounterSession? _subscribed;
    private Guid? _followed;

    [CascadingParameter] public EncounterSession Session { get; set; } = null!;

    [Inject] protected IDiceRoller Dice { get; set; } = null!;

    protected EncounterState State => Session.State;

    protected DateTimeOffset Now => Session.Now;

    /// <summary>Кто действует; меняется вслед за ходом, пока Хранитель не выбрал другого.</summary>
    protected Guid? ActorId { get; set; }

    protected EncounterParticipant? Actor => ActorId is { } id ? State.Find(id) : null;

    /// <summary>
    /// Сцена меняется не через параметры (каскад зафиксирован, у панелей параметров нет): панель подписана на
    /// <see cref="EncounterSession.Changed"/> — новый ход, применённый результат, перечитанные листы.
    /// </summary>
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_subscribed, Session))
        {
            if (_subscribed is not null)
                _subscribed.Changed -= OnSessionChanged;
            _subscribed = Session;
            Session.Changed += OnSessionChanged;
        }

        Follow();
    }

    public void Dispose()
    {
        if (_subscribed is not null)
            _subscribed.Changed -= OnSessionChanged;
        GC.SuppressFinalize(this);
    }

    private void OnSessionChanged() => _ = InvokeAsync(() =>
    {
        Follow();
        StateHasChanged();
    });

    /// <summary>Действующий — за ходом; выбранный пропал из сцены — сброс.</summary>
    protected virtual void Follow()
    {
        if (State.ActiveParticipantId != _followed)
        {
            _followed = State.ActiveParticipantId;
            if (_followed is { } active && State.Find(active) is { Dead: false })
            {
                ActorId = active;
                OnActorChanged();
            }
        }

        if (ActorId is { } id && State.Find(id) is null)
            ActorId = null;
    }

    /// <summary>Сменился действующий — сбросить выбранное под него (оружие, броски).</summary>
    protected virtual void OnActorChanged()
    {
    }

    protected void SelectActor(Guid? id)
    {
        ActorId = id;
        OnActorChanged();
    }

    /// <summary>Положить результат на предпросмотр (переживает перезагрузку, применяет «Применить»).</summary>
    protected Task ProposeAsync(EncounterResolution resolution) =>
        Session.ChangeAsync(state => EncounterEngine.Propose(state, resolution));

    /// <summary>Расстановка без предпросмотра (огнестрел наготове, правила, броски инициативы) — сразу в документ.</summary>
    protected Task ChangeAsync(Action<EncounterState> change) => Session.ChangeAsync(change);

    protected Task ApplyNowAsync(EncounterResolution resolution) =>
        Session.ChangeAsync(state => EncounterEngine.Apply(state, resolution, Now));

    /// <summary>Те, кто в бою может действовать: не мёртв.</summary>
    protected static bool Alive(EncounterParticipant p) => !p.Dead;

    /// <summary>Итог −2…+2 пикера костей — бонусные и штрафные (одна бонусная гасит одну штрафную, стр. 89).</summary>
    protected static (int Bonus, int Penalty) Split(int net) => (Math.Max(0, net), Math.Max(0, -net));
}
