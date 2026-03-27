namespace CampaignManager.Web.Model;

/// <summary>
/// Marks an entity as having a unique Name property and an Id,
/// enabling generic CRUD helpers to operate on it.
/// </summary>
public interface INamedEntity
{
    Guid Id { get; }
    string Name { get; }
}
