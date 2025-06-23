using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Utilities.Services;

namespace CampaignManager.Web.Components.Features.Combat.Services;

public class CombatService
{
    private readonly IdentityService _identityService;
    private readonly ILogger<CombatService> _logger;
    private readonly Dictionary<Guid, CombatEncounter> _activeCombats = new();

    public CombatService(
        IdentityService identityService,
        ILogger<CombatService> logger)
    {
        _identityService = identityService;
        _logger = logger;
    }

    public CombatEncounter? GetCombatEncounter(Guid id)
    {
        _activeCombats.TryGetValue(id, out var encounter);
        return encounter;
    }

    public List<CombatEncounter> GetActiveCombatEncounters()
    {
        return _activeCombats.Values
            .Where(c => c.State == CombatState.Active || c.State == CombatState.RollingInitiative)
            .ToList();
    }

    public List<CombatEncounter> GetUserCombatEncounters()
    {
        var userEmail = _identityService.GetCurrentUserEmail();
        if (string.IsNullOrEmpty(userEmail))
            return new List<CombatEncounter>();

        return _activeCombats.Values.ToList();
    }

    public CombatEncounter CreateCombatEncounter(CombatEncounter encounter)
    {
        var encounterGuid = Guid.NewGuid();
        _activeCombats[encounterGuid] = encounter;

        _logger.LogInformation("Created in-memory combat encounter {Id} by user {UserEmail}",
            encounterGuid, _identityService.GetCurrentUserEmail() ?? "Anonymous");

        return encounter;
    }

    public bool UpdateCombatEncounter(Guid encounterId, CombatEncounter encounter)
    {
        if (!_activeCombats.ContainsKey(encounterId))
        {
            _logger.LogWarning("Combat encounter {Id} not found for update", encounterId);
            return false;
        }

        _activeCombats[encounterId] = encounter;
        _logger.LogInformation("Updated in-memory combat encounter {Id}", encounterId);
        return true;
    }

    public bool DeleteCombatEncounter(Guid id)
    {
        if (_activeCombats.Remove(id))
        {
            _logger.LogInformation("Deleted in-memory combat encounter {Id}", id);
            return true;
        }

        _logger.LogWarning("Combat encounter {Id} not found for deletion", id);
        return false;
    }

    public void AddParticipant(Guid encounterId, CombatParticipant participant)
    {
        if (_activeCombats.TryGetValue(encounterId, out var encounter))
        {
            encounter.AddParticipant(participant);
            _logger.LogInformation("Added participant {ParticipantName} to combat encounter {EncounterId}",
                participant.Name, encounterId);
        }
        else
        {
            _logger.LogWarning("Combat encounter {EncounterId} not found when adding participant", encounterId);
        }
    }

    public bool RemoveParticipant(Guid encounterId, Guid participantId)
    {
        if (_activeCombats.TryGetValue(encounterId, out var encounter))
        {
            encounter.RemoveParticipant(participantId);
            _logger.LogInformation("Removed participant {ParticipantId} from combat encounter {EncounterId}",
                participantId, encounterId);
            return true;
        }

        _logger.LogWarning("Combat encounter {EncounterId} not found when removing participant", encounterId);
        return false;
    }

    public void AddCombatLogEntry(Guid encounterId, CombatAction action)
    {
        if (_activeCombats.TryGetValue(encounterId, out var encounter))
        {
            encounter.AddToCombatLog(action);
        }
    }

    // Clean up finished combats periodically (could be called by a background service)
    public void CleanupFinishedCombats()
    {
        var toRemove = _activeCombats
            .Where(kvp => kvp.Value.State == CombatState.Finished)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            _activeCombats.Remove(id);
        }

        if (toRemove.Any())
        {
            _logger.LogInformation("Cleaned up {Count} finished combat encounters", toRemove.Count);
        }
    }
}