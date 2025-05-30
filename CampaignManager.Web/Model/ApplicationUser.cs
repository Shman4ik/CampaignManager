﻿using CampaignManager.Web.Components.Features.Characters.Model;
using Microsoft.AspNetCore.Identity;

namespace CampaignManager.Web.Model;

public class ApplicationUser : IdentityUser
{
    public PlayerRole Role { get; set; }
}