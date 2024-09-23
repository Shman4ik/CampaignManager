using System;
using System.Collections.Generic;

namespace CampaignManager.Web.Model
{
    public class Campaign
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Keeper Keeper { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }

    public class Keeper : User
    {
        public List<Campaign> Campaigns { get; set; } = new List<Campaign>();
    }

    public class Player : User
    {
        public Character Character { get; set; }
        public Campaign Campaign { get; set; }
    }
}