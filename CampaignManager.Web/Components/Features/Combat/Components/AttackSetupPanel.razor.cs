using CampaignManager.Web.Components.Features.Combat.Components.Attack;
using CampaignManager.Web.Components.Features.Combat.Model;
using Microsoft.AspNetCore.Components;

namespace CampaignManager.Web.Components.Features.Combat.Components;

/// <summary>
///     Вкладка «Атака» боевого помощника. Состояние панели — один <see cref="AttackSetupState" />,
///     его правят и эта разметка, и разделы из <c>Components/Attack</c>; любой раздел после правки
///     зовёт <see cref="Refresh" />, и панель перерисовывается целиком, как до разбиения на части.
/// </summary>
public partial class AttackSetupPanel
{
    [Parameter] public List<Combatant> Combatants { get; set; } = [];
    [Parameter] public EventCallback<CombatActionResult> OnAttackResolved { get; set; }
    [Parameter] public Guid? ActiveCombatantId { get; set; }

    private readonly AttackSetupState _state = new();
    private Guid? _lastActiveId;

    protected override void OnParametersSet()
    {
        if (ActiveCombatantId.HasValue && ActiveCombatantId != _lastActiveId)
        {
            _lastActiveId = ActiveCombatantId;
            var active = Combatants.FirstOrDefault(c => c.Id == ActiveCombatantId.Value && !c.IsDead && !c.IsUnconscious);
            if (active != null) _state.SelectAttacker(active);
        }
    }

    private IEnumerable<Combatant> AvailableAttackers =>
        Combatants.Where(c => !c.IsDead && !c.IsUnconscious);

    private IEnumerable<Combatant> AvailableDefenders =>
        Combatants.Where(c => !c.IsDead && c.Id != _state.SelectedAttacker?.Id);

    /// <summary>
    ///     Раздел панели что-то поменял. Делать тут нечего: вызов <see cref="EventCallback" />
    ///     сам перерисовывает панель, а с ней — и все разделы.
    /// </summary>
    private static void Refresh()
    {
    }

    private void OnAttackerChanged(ChangeEventArgs e)
    {
        var id = e.Value?.ToString() ?? "";
        var combatant = Guid.TryParse(id, out var guid) ? Combatants.FirstOrDefault(c => c.Id == guid) : null;
        _state.SelectAttacker(combatant);
    }

    private void OnWeaponChanged(ChangeEventArgs e) =>
        _state.SelectWeapon(int.TryParse(e.Value?.ToString(), out var idx) ? idx : -1);

    private void OnDefenderChanged(ChangeEventArgs e)
    {
        var defenderId = e.Value?.ToString() ?? "";
        var defender = Guid.TryParse(defenderId, out var id)
            ? Combatants.FirstOrDefault(c => c.Id == id)
            : null;
        _state.SelectDefender(defenderId, defender);
    }

    /// <summary>Почему атака сейчас невозможна: укрытие или израсходованные атаки за раунд.</summary>
    private string? AttackBlockReason =>
        _state.SelectedAttacker is null ? null : CombatService.GetAttackBlockReason(_state.SelectedAttacker);

    private bool CanResolve() =>
        _state.SelectedAttacker != null && _state.SelectedWeaponIndex >= 0
        && AttackBlockReason is null
        && (_state.SelectedDefender != null || !_state.IsMelee)
        && !_state.WeaponIsJammed
        && !_state.ShotIsImpossible
        && (_state.AmmoCapacity <= 0 || _state.AmmoLeft >= _state.ShotsPerCheck);

    private async Task ResolveAttack()
    {
        if (_state.SelectedAttacker == null || _state.SelectedWeaponIndex < 0) return;
        if (_state.IsMelee && _state.SelectedDefender == null) return;

        var setup = _state.BuildSetup();

        var result = _state.IsMelee
            ? CombatService.ResolveMeleeAttack(setup)
            : CombatService.ResolveRangedAttack(setup);

        _state.ResetRolls();
        await OnAttackResolved.InvokeAsync(result);
    }
}
