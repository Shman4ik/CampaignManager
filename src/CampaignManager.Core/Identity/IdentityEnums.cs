namespace CampaignManager.Core.Identity;

/// <summary>Роль пользователя на платформе (<c>cm.users.role</c>).</summary>
public enum UserRole
{
    Player,
    Keeper,
    Admin,
}

/// <summary>Состояние заявки игрока на роль Хранителя.</summary>
public enum KeeperApplicationStatus
{
    Pending,
    Approved,
    Rejected,
}
