namespace CampaignManager.Web.Components.Features.Chase.Model;

/// <summary>
/// Полное состояние погони для сохранения между подключениями.
/// ChaseService живёт в circuit, поэтому обновление вкладки или обрыв связи (обычное дело на планшете
/// за игровым столом) иначе стирает сцену целиком.
/// </summary>
public class ChaseSnapshot
{
    public ChasePhase Phase { get; set; } = ChasePhase.Setup;
    public List<ChaseParticipant> Participants { get; set; } = [];
    public List<ChaseLocation> Locations { get; set; } = [];
    public int CurrentRound { get; set; } = 1;
    public int CurrentTurnIndex { get; set; }
    public int MinAdjustedMov { get; set; }
    public int StartGap { get; set; } = 2;
    public List<ChaseActionResult> ChaseLog { get; set; } = [];

    // Необязательные правила (часть 5, стр. 137)
    public bool UseRandomHazards { get; set; }
    public bool UseSuddenHazards { get; set; }
    public bool UseFloorIt { get; set; }

    /// <summary>
    /// Чья очередь объявлять внезапную помеху (стр. 137): стороны чередуются,
    /// одна не может объявить дважды подряд.
    /// </summary>
    public bool SuddenHazardIsPlayersTurn { get; set; } = true;
}
