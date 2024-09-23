using System;
using System.Collections.Generic;
using System.Linq;
using CampaignManager.Web.Model;

public class CampaignService
{
    private List<Campaign> campaigns = new List<Campaign>();
    private List<User> users = new List<User>();
    private User currentUser;

    public void RegisterUser(string username, string email, bool isKeeper)
    {
        if (isKeeper)
        {
            users.Add(new Keeper { Id = Guid.NewGuid(), Username = username, Email = email });
        }
        else
        {
            users.Add(new Player { Id = Guid.NewGuid(), Username = username, Email = email });
        }
    }

    public bool Login(string username)
    {
        currentUser = users.FirstOrDefault(u => u.Username == username);
        return currentUser != null;
    }

    public void Logout()
    {
        currentUser = null;
    }

    public Campaign CreateCampaign(string name)
    {
        if (currentUser is Keeper keeper)
        {
            var campaign = new Campaign
            {
                Id = Guid.NewGuid(),
                Name = name,
                Keeper = keeper,
                CreatedAt = DateTime.Now,
                LastUpdated = DateTime.Now
            };
            campaigns.Add(campaign);
            keeper.Campaigns.Add(campaign);
            return campaign;
        }
        throw new UnauthorizedAccessException("Only Keepers can create campaigns.");
    }

    public void AddPlayerToCampaign(Guid campaignId, string playerUsername)
    {
        var campaign = campaigns.FirstOrDefault(c => c.Id == campaignId);
        if (campaign == null) throw new ArgumentException("Campaign not found.");

        if (currentUser != campaign.Keeper)
            throw new UnauthorizedAccessException("Only the campaign Keeper can add players.");

        var player = users.FirstOrDefault(u => u.Username == playerUsername) as Player;
        if (player == null) throw new ArgumentException("Player not found.");

        campaign.Players.Add(player);
        player.Campaign = campaign;
        player.Character = new CharacterGenerationService().GenerateRandomCharacter();
        campaign.LastUpdated = DateTime.Now;
    }

    public List<Character> GetCampaignCharacters(Guid campaignId)
    {
        var campaign = campaigns.FirstOrDefault(c => c.Id == campaignId);
        if (campaign == null) throw new ArgumentException("Campaign not found.");

        if (currentUser == campaign.Keeper)
        {
            return campaign.Players.Select(p => p.Character).ToList();
        }
        else if (currentUser is Player player && player.Campaign == campaign)
        {
            return new List<Character> { player.Character };
        }
        throw new UnauthorizedAccessException("You don't have access to this campaign.");
    }

    public List<Campaign> GetUserCampaigns()
    {
        if (currentUser is Keeper keeper)
        {
            return keeper.Campaigns;
        }
        else if (currentUser is Player player)
        {
            return new List<Campaign> { player.Campaign };
        }
        throw new UnauthorizedAccessException("User not logged in.");
    }
}