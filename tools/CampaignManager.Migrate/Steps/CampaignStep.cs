using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Data.Campaigns;
using CampaignManager.Migrate.V1;

namespace CampaignManager.Migrate.Steps;

/// <summary>
/// Кампании: Хранитель — участник с ролью <c>Keeper</c>; строки «Хранитель — игрок своей кампании» v1
/// выбрасываются; псевдоним игрока переносится, только если он отличается от имени пользователя.
/// </summary>
public static class CampaignStep
{
    public static void Run(MigrationState s)
    {
        var displayNames = s.Db.Users.Local.ToDictionary(u => u.Id, u => u.DisplayName);
        foreach (var row in s.V1.Campaigns)
        {
            var name = row.Text("Name")!;
            var keeperId = s.UserId(row.Str("KeeperEmail"))
                ?? throw new InvalidOperationException($"Кампания «{name}»: Хранителя нет в identity.");
            if (row.Int("Era") is { } era and not 1)
            {
                s.Report.Add(ReportSections.Fixed, $"кампания «{name}»: эпоха v1 {era} → классика (современная эпоха убрана)");
            }

            var campaign = new Campaign
            {
                Id = row.Guid("Id")!.Value,
                Name = name,
                Kind = name.Contains("Ваншот", StringComparison.OrdinalIgnoreCase) ? CampaignKind.OneShot : CampaignKind.Campaign,
                Status = row.Enum<CampaignStatus>("Status") ?? CampaignStatus.Planning,
                Era = Era.Classic,
                CreatedById = keeperId,
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            };
            campaign.Members.Add(new CampaignMember
            {
                UserId = keeperId,
                Role = CampaignRole.Keeper,
                JoinedAt = campaign.CreatedAt,
            });
            s.Campaigns[campaign.Id] = campaign;
            s.Db.Campaigns.Add(campaign);
        }

        var (players, keeperRows, aliases) = (0, 0, 0);
        foreach (var row in s.V1.CampaignPlayers)
        {
            var id = row.Guid("Id")!.Value;
            var campaign = s.Campaigns[row.Guid("CampaignId")!.Value];
            var userId = s.UserId(row.Str("PlayerEmail"))
                ?? throw new InvalidOperationException($"Участник {id} кампании «{campaign.Name}»: пользователя нет в identity.");
            s.CampaignPlayers[id] = (campaign.Id, userId);

            if (campaign.Members.FirstOrDefault(m => m.UserId == userId) is { } existing)
            {
                // Хранитель записан ещё и игроком своей кампании (v1 строил «Ваши кампании» по игрокам)
                keeperRows += existing.Role == CampaignRole.Keeper ? 1 : 0;
                s.Report.Add(ReportSections.DroppedJunk, existing.Role == CampaignRole.Keeper
                    ? $"кампания «{campaign.Name}»: Хранитель записан игроком своей кампании"
                    : $"кампания «{campaign.Name}»: игрок записан дважды");
                continue;
            }

            var alias = row.Text("PlayerName");
            var member = new CampaignMember
            {
                UserId = userId,
                Role = CampaignRole.Player,
                DisplayName = alias is not null && !string.Equals(alias, displayNames[userId], StringComparison.Ordinal) ? alias : null,
                JoinedAt = row.Time("CreatedAt") ?? campaign.CreatedAt,
            };
            aliases += member.DisplayName is null ? 0 : 1;
            campaign.Members.Add(member);
            players++;
        }

        s.Report.Count("games.Campaigns", s.V1.Campaigns.Count, "campaigns", s.Campaigns.Count,
            $"ваншотов — {s.Campaigns.Values.Count(c => c.Kind == CampaignKind.OneShot)}");
        s.Report.Count("games.CampaignPlayers", s.V1.CampaignPlayers.Count, "campaign_members", players + s.Campaigns.Count,
            $"+{s.Campaigns.Count} Хранителя, −{keeperRows} строки Хранителя-игрока; псевдонимов — {aliases}");
    }
}
