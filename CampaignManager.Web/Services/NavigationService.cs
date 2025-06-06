using CampaignManager.Web.Models.Navigation;

namespace CampaignManager.Web.Services;

public class NavigationService
{
    public List<NavigationItem> GetNavigationItems()
    {
        return new List<NavigationItem>
        {
            new NavigationItem
            {
                Title = "Главная",
                Href = "/",
                Icon = "fas fa-home",
                MatchMode = NavigationMatchMode.All
            },
            new NavigationItem
            {
                Title = "Кампании",
                Icon = "fas fa-dice-d20",
                Children = new List<NavigationItem>
                {
                    new NavigationItem
                    {
                        Title = "Создать кампанию",
                        Href = "/create-campaign",
                        Icon = "fas fa-plus"
                    },
                    new NavigationItem
                    {
                        Title = "Сценарии",
                        Href = "/scenarios",
                        Icon = "fas fa-scroll"
                    },
                    new NavigationItem
                    {
                        Title = "NPC",
                        Href = "/npcs",
                        Icon = "fas fa-users"
                    },
                    new NavigationItem
                    {
                        Title = "Персонажи",
                        Href = "/characters",
                        Icon = "fas fa-user-circle"
                    }
                }
            },
            new NavigationItem
            {
                Title = "Справочник",
                Icon = "fas fa-book",
                Children = new List<NavigationItem>
                {
                    new NavigationItem
                    {
                        Title = "Оружие",
                        Href = "/weapons",
                        Icon = "fas fa-sword"
                    },
                    new NavigationItem
                    {
                        Title = "Заклинания",
                        Href = "/spells",
                        Icon = "fas fa-magic"
                    },
                    new NavigationItem
                    {
                        Title = "Навыки",
                        Href = "/skills",
                        Icon = "fas fa-cog"
                    },
                    new NavigationItem
                    {
                        Title = "Существа",
                        Href = "/bestiary",
                        Icon = "fas fa-dragon"
                    },
                    new NavigationItem
                    {
                        Title = "Предметы",
                        Href = "/items",
                        Icon = "fas fa-gem"
                    }
                }
            }
        };
    }
}
