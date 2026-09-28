using CampaignManager.Web.Components.Features.Chase.Components.Actions;
using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Combat.Services;
using Microsoft.AspNetCore.Components;

namespace CampaignManager.Web.Components.Features.Chase.Components;

/// <summary>
///     Ход активного участника погони: шапка с действиями перемещения, предупреждения о помехе и
///     преграде впереди, ряд кнопок действий и панель выбранного действия. Сами панели — по
///     компоненту на вид действия в <c>Components/Actions</c>: каждая рисует свою форму и сама зовёт
///     нужный <c>ChaseService.Resolve*</c>, а сюда отдаёт готовый результат.
/// </summary>
public partial class ChaseActionPanel
{
    [Parameter] public ChaseParticipant? ActiveParticipant { get; set; }
    [Parameter] public List<ChaseLocation> Locations { get; set; } = [];
    [Parameter] public EventCallback<ChaseActionResult> OnActionResolved { get; set; }

    private enum ActionMode
    {
        None, Move, Hazard, Barrier, Destroy, Melee, Ranged, Maneuver,
        FloorIt, Ram, Tyres, Hide, Track, CreateObstacle, DriverControl, Navigate
    }

    private ActionMode _selectedAction = ActionMode.None;

    /// <summary>Поля ввода, общие для всех панелей действий (см. <see cref="ChaseActionForm" />).</summary>
    private readonly ChaseActionForm _form = new();

    protected override void OnParametersSet()
    {
        if (ActiveParticipant is null) return;

        var nextLoc = ChaseService.GetLocation(ActiveParticipant.CurrentLocation + 1);
        if (nextLoc is null) return;

        // Авто-заполнение навыка из локации
        if (nextLoc.HasBarrier && !nextLoc.IsBarrierDestroyed && string.IsNullOrEmpty(_form.SkillName))
        {
            _form.SkillName = nextLoc.BarrierSkillName ?? "";
            _form.SkillValue = nextLoc.BarrierSkillValue;
            if (ActiveParticipant.CharacterSource is not null && !string.IsNullOrEmpty(nextLoc.BarrierSkillName))
            {
                var v = CombatService.FindSkillValue(ActiveParticipant.CharacterSource, nextLoc.BarrierSkillName);
                if (v > 0) _form.SkillValue = v;
            }
        }
        else if (nextLoc.HasHazard && string.IsNullOrEmpty(_form.SkillName))
        {
            _form.SkillName = nextLoc.HazardSkillName ?? "";
            _form.SkillValue = nextLoc.HazardSkillValue;
            if (ActiveParticipant.CharacterSource is not null && !string.IsNullOrEmpty(nextLoc.HazardSkillName))
            {
                var v = CombatService.FindSkillValue(ActiveParticipant.CharacterSource, nextLoc.HazardSkillName);
                if (v > 0) _form.SkillValue = v;
            }
        }
    }

    private void SelectAction(ActionMode mode)
    {
        _selectedAction = mode;
        _form.ResetForAction(ActiveParticipant?.CurrentLocation ?? 1);

        // Остановиться для выстрела можно, только если есть действие перемещения (стр. 139)
        if (mode == ActionMode.Ranged)
            _form.StoppedToShoot = ActiveParticipant?.MovementActionsRemaining > 0;
    }

    private void CancelAction()
    {
        _selectedAction = ActionMode.None;
        _form.ClearSkill();
    }

    /// <summary>Панель разрешила действие: закрываем её и отдаём результат странице на предпросмотр.</summary>
    private async Task HandleResolved(ChaseActionResult result)
    {
        CancelAction();
        await OnActionResolved.InvokeAsync(result);
    }

    /// <summary>
    ///     Шаг вперёд закрывает панель, но навык не очищает: помеха или преграда на следующей
    ///     локации подставит свой, только если поле пустое.
    /// </summary>
    private async Task HandleMoved(ChaseActionResult result)
    {
        _selectedAction = ActionMode.None;
        await OnActionResolved.InvokeAsync(result);
    }

    private void DelayAction()
    {
        if (ActiveParticipant is null) return;
        ChaseService.DelayAction(ActiveParticipant.Id);
    }

    private void SkipTurn()
    {
        if (ActiveParticipant is null) return;
        ChaseService.SkipAction(ActiveParticipant.Id);
    }

    private void EndTurn()
    {
        if (ActiveParticipant is null) return;
        ChaseService.EndTurn(ActiveParticipant.Id);
    }
}
