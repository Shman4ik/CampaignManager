using CampaignManager.Core.Documents;

namespace CampaignManager.Core.Encounters;

/// <summary>
/// Состояние сцены — документ <c>encounters.state</c> (SCHEMA, «Документы»). Здесь только каркас: общие
/// для боя и погони участники, очередь, раунд и журнал. Специфичную часть, типизированные записи журнала
/// и движок сцены наполняет T2.6; неизвестное этой версии сохраняется через <see cref="DocumentPart.Extra"/>.
/// </summary>
public sealed record EncounterState : DocumentPart
{
    /// <summary>Текущая версия документа (<c>encounters.state_version</c>).</summary>
    public const int CurrentVersion = 1;

    public int Round { get; set; } = 1;

    /// <summary>Чей ход — по id участника, а не по индексу: в v1 индекс сбивался при удалении и выбывании.</summary>
    public Guid? ActiveParticipantId { get; set; }

    public List<EncounterParticipant> Participants { get; set; } = [];

    public List<EncounterLogEntry> Log { get; set; } = [];
}

/// <summary>Сторона участника сцены.</summary>
public enum EncounterSide
{
    Investigators,
    Enemies,
    Neutral,
}

/// <summary>
/// Участник: ссылка на источник плюс снимок чисел. Id участника ≠ id листа — один лист может войти в
/// сцену только раз, но тварей одного вида бывает несколько («#2»).
/// </summary>
public sealed record EncounterParticipant : DocumentPart
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid? SourceCharacterId { get; set; }

    public Guid? SourceCreatureId { get; set; }

    public string Name { get; set; } = "";

    public EncounterSide Side { get; set; }

    public int HitPoints { get; set; }

    public int MaxHitPoints { get; set; }

    /// <summary>Выбыл из очереди (ранен, пойман, спрятался), но остаётся в сцене.</summary>
    public bool IsOut { get; set; }
}

/// <summary>Запись журнала сцены. Вид — строкой: в v1 он лежал числом, и члены нельзя было переставлять.</summary>
public sealed record EncounterLogEntry : DocumentPart
{
    public int Round { get; set; }

    public string Kind { get; set; } = "";

    public string Text { get; set; } = "";

    public DateTimeOffset At { get; set; }
}
