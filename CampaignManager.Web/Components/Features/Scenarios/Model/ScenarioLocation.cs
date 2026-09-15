namespace CampaignManager.Web.Components.Features.Scenarios.Model;

public sealed class ScenarioLocation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public string? Address { get; set; }

    public string? Description { get; set; }

    public int Order { get; set; }

    public Guid? ParentLocationId { get; set; }

    public List<ScenarioSkillCheck> SkillChecks { get; set; } = [];

    public List<Guid> CreatureIds { get; set; } = [];

    public List<Guid> ItemIds { get; set; } = [];

    public List<Guid> NpcIds { get; set; } = [];

    public List<Guid> HandoutIds { get; set; } = [];

    /// <summary>
    ///     Теги настроения для фонотеки: кнопка «Играть» на локации берёт случайный трек
    ///     из всего, что помечено хотя бы одним из них. Нормализованы через
    ///     <c>MusicSource.NormalizeTag</c>.
    /// </summary>
    public List<string> MusicTags { get; set; } = [];

    /// <summary>
    ///     Треки, прибитые к локации гвоздями, — когда для сцены есть единственно верная вещь.
    ///     Попадают в тот же пул, что и найденное по тегам.
    /// </summary>
    public List<Guid> MusicTrackIds { get; set; } = [];
}
