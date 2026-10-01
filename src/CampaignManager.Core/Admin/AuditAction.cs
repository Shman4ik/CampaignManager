namespace CampaignManager.Core.Admin;

/// <summary>Действие в истории правок справочников (<c>cm.audit_log</c>).</summary>
public enum AuditAction
{
    Created,
    Updated,
    Deleted,
}
