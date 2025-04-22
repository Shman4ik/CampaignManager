namespace CampaignManager.Web.Model;

public class SkillsModel
{
    public List<SkillGroup> SkillGroups { get; set; } = [];

    public static SkillsModel DefaultSkillsModel()
    {
        return new SkillsModel
        {
            SkillGroups =
            [
                new SkillGroup
                {
                    Name = "Решение Проблем",
                    Skills =
                    [
                        new Skill { Name = "Взлом", Value = new AttributeValue(1), BaseValue = "01%" },
                        new Skill { Name = "Выживание", Value = new AttributeValue(10), BaseValue = "10%" },
                        new Skill { Name = "Ловкость рук", Value = new AttributeValue(10), BaseValue = "10%" },
                        new Skill { Name = "Механика", Value = new AttributeValue(10), BaseValue = "10%" },
                        new Skill { Name = "Ориентирование", Value = new AttributeValue(10), BaseValue = "10%" },
                        new Skill { Name = "Скрытность", Value = new AttributeValue(20), BaseValue = "20%" },
                        new Skill { Name = "Чтение следов", Value = new AttributeValue(10), BaseValue = "10%" },
                        new Skill { Name = "Электрика", Value = new AttributeValue(10), BaseValue = "10%" }
                    ]
                },

                new SkillGroup
                {
                    Name = "Социальные",
                    Skills =
                    [
                        new Skill { Name = "Антропология", Value = new AttributeValue(1), BaseValue = "01%" },
                        new Skill { Name = "Запугивание", Value = new AttributeValue(15), BaseValue = "15%" },
                        new Skill { Name = "Красноречие", Value = new AttributeValue(5), BaseValue = "05%" },
                        new Skill { Name = "Маскировка", Value = new AttributeValue(5), BaseValue = "05%" },
                        new Skill { Name = "Обаяние", Value = new AttributeValue(15), BaseValue = "15%" },
                        new Skill { Name = "Психология", Value = new AttributeValue(10), BaseValue = "10%" },
                        new Skill { Name = "Убеждение", Value = new AttributeValue(10), BaseValue = "10%" },
                        new Skill { Name = "Языки (родной)", Value = new AttributeValue(0), BaseValue = "ОБР" },
                        new Skill { Name = "Языки (иностр.)", Value = new AttributeValue(1), BaseValue = "01%" }
                    ]
                },

                new SkillGroup
                {
                    Name = "Сбор Информации",
                    Skills =
                    [
                        new Skill { Name = "Внимание", Value = new AttributeValue(25), BaseValue = "25%" },
                        new Skill { Name = "Работа в библиотеке", Value = new AttributeValue(20), BaseValue = "20%" },
                        new Skill { Name = "Слух", Value = new AttributeValue(20), BaseValue = "20%" }
                    ]
                },

                new SkillGroup
                {
                    Name = "Лечение",
                    Skills =
                    [
                        new Skill { Name = "Медицина", Value = new AttributeValue(1), BaseValue = "01%" },
                        new Skill { Name = "Первая помощь", Value = new AttributeValue(30), BaseValue = "30%" },
                        new Skill { Name = "Психоанализ", Value = new AttributeValue(1), BaseValue = "01%" }
                    ]
                },

                new SkillGroup
                {
                    Name = "Знания",
                    Skills =
                    [
                        new Skill { Name = "Археология", Value = new AttributeValue(1), BaseValue = "01%" },
                        new Skill { Name = "Бухгалтерское дело", Value = new AttributeValue(5), BaseValue = "05%" },
                        new Skill { Name = "Естествознание", Value = new AttributeValue(10), BaseValue = "10%" },
                        new Skill { Name = "Искусство/ремесло", Value = new AttributeValue(5), BaseValue = "05%" },
                        new Skill { Name = "История", Value = new AttributeValue(5), BaseValue = "05%" },
                        new Skill { Name = "Наука", Value = new AttributeValue(1), BaseValue = "01%" },
                        new Skill { Name = "Оккультизм", Value = new AttributeValue(5), BaseValue = "05%" },
                        new Skill { Name = "Оценка", Value = new AttributeValue(5), BaseValue = "05%" },
                        new Skill { Name = "Юриспруденция", Value = new AttributeValue(5), BaseValue = "05%" }
                    ]
                },

                new SkillGroup
                {
                    Name = "Специальные",
                    Skills =
                    [
                        new Skill { Name = "Мифы Ктулху", Value = new AttributeValue(0), BaseValue = "00%" },
                        new Skill { Name = "Средства", Value = new AttributeValue(0), BaseValue = "00%" }
                    ]
                },

                new SkillGroup
                {
                    Name = "Сражение (общее)",
                    Skills =
                    [
                        new Skill { Name = "Ближний бой (драка)", Value = new AttributeValue(25), BaseValue = "25%" },
                        new Skill { Name = "Метание", Value = new AttributeValue(20), BaseValue = "20%" },
                        new Skill { Name = "Уклонение", Value = new AttributeValue(0), BaseValue = "½ ЛВК" }
                    ]
                },

                new SkillGroup
                {
                    Name = "Сражение (Огнестрельное)",
                    Skills =
                    [
                        new Skill { Name = "Автомат", Value = new AttributeValue(15), BaseValue = "15%" },
                        new Skill { Name = "Стрельба (винт./дроб.)", Value = new AttributeValue(25), BaseValue = "25%" },
                        new Skill { Name = "Стрельба (пистолет)", Value = new AttributeValue(20), BaseValue = "20%" }
                    ]
                },

                new SkillGroup
                {
                    Name = "Действия",
                    Skills =
                    [
                        new Skill { Name = "Верховая езда", Value = new AttributeValue(5), BaseValue = "05%" },
                        new Skill { Name = "Вождение", Value = new AttributeValue(20), BaseValue = "20%" },
                        new Skill { Name = "Лазание", Value = new AttributeValue(20), BaseValue = "20%" },
                        new Skill { Name = "Пилотирование", Value = new AttributeValue(1), BaseValue = "01%" },
                        new Skill { Name = "Плавание", Value = new AttributeValue(20), BaseValue = "20%" },
                        new Skill { Name = "Прыжки", Value = new AttributeValue(20), BaseValue = "20%" },
                        new Skill { Name = "Упр. тяж. машинами", Value = new AttributeValue(1), BaseValue = "01%" }
                    ]
                }
            ]
        };
    }
}