namespace CampaignManager.Web.Model;

public class Character
{
    public Guid Id { get; set; }

    /// <summary>
    ///     Персональная информация о персонаже
    /// </summary>
    public PersonalInfo PersonalInfo { get; set; } = new();

    /// <summary>
    ///     Характеристики персонажа
    /// </summary>
    public Characteristics Characteristics { get; set; } = new();

    /// <summary>
    ///     Производные характеристики персонажа
    /// </summary>
    public DerivedAttributes DerivedAttributes { get; set; } = new();

    /// <summary>
    ///     Навыки персонажа
    /// </summary>
    public SkillsModel Skills { get; set; } = new();

    /// <summary>
    ///     Предыстория персонажа
    /// </summary>
    public string Backstory { get; set; } = "";

    /// <summary>
    ///     Состояние персонажа
    /// </summary>
    public CharacterState State { get; set; } = new();

    /// <summary>
    ///     Биография персонажа
    /// </summary>
    public BiographyInfo Biography { get; set; } = new();

    /// <summary>
    ///     Снаряжение и имущество персонажа
    /// </summary>
    public Equipment Equipment { get; set; } = new();

    /// <summary>
    ///     Финансы персонажа
    /// </summary>
    public Finances Finances { get; set; } = new();

    /// <summary>
    ///     Детали боя ближнего действия
    /// </summary>
    public CloseCombatDetails CloseCombat { get; set; } = new();

    /// <summary>
    ///     Детали боя дальнего действия
    /// </summary>
    public RangedCombatDetails RangedCombat { get; set; } = new();
}

public class PersonalInfo
{
    /// <summary>
    ///     Имя игрока, управляющего персонажем
    /// </summary>
    public string PlayerName { get; set; } = "";

    /// <summary>
    ///     Имя персонажа
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Профессия или род занятий персонажа
    /// </summary>
    public string Occupation { get; set; } = "";

    /// <summary>
    ///     Возраст персонажа
    /// </summary>
    public int Age { get; set; } = 0;

    /// <summary>
    ///     Пол персонажа
    /// </summary>
    public string Gender { get; set; } = "";

    /// <summary>
    ///     Место рождения персонажа
    /// </summary>
    public string Birthplace { get; set; } = "";

    /// <summary>
    ///     Текущее место жительства персонажа
    /// </summary>
    public string Residence { get; set; } = "";

    /// <summary>
    ///     Бонус к урону персонажа
    /// </summary>
    public string DamageBonus { get; set; } = "0";

    /// <summary>
    ///     Скорость передвижения персонажа
    /// </summary>
    public int MoveSpeed { get; set; } = 0;

    /// <summary>
    ///     Значение уклонения персонажа
    /// </summary>
    public int Dodge { get; set; } = 0;

    /// <summary>
    ///     Телосложение персонажа
    /// </summary>
    public string Build { get; set; } = "0";
}

public class Characteristics
{
    public AttributeValue Strength { get; set; } = new(50);
    public AttributeValue Dexterity { get; set; } = new(45);
    public AttributeValue Intelligence { get; set; } = new(80);
    public AttributeValue Constitution { get; set; } = new(80);
    public AttributeValue Appearance { get; set; } = new(40);
    public AttributeValue Power { get; set; } = new(70);
    public AttributeValue Size { get; set; } = new(60);
    public AttributeValue Education { get; set; } = new(75);
}

public class DerivedAttributes
{
    /// <summary>
    ///     Очки здоровья персонажа
    /// </summary>
    public AttributeWithMaxValue HitPoints { get; set; } = new(0, 0);

    /// <summary>
    ///     Очки магии персонажа
    /// </summary>
    public AttributeWithMaxValue MagicPoints { get; set; } = new(0, 0);

    /// <summary>
    ///     Уровень рассудка персонажа
    /// </summary>
    public AttributeWithMaxValue Sanity { get; set; } = new(0, 99);

    /// <summary>
    ///     Уровень удачи персонажа
    /// </summary>
    public AttributeWithMaxValue Luck { get; set; } = new(0, 99);
}

public class AttributeWithMaxValue
{
    public AttributeWithMaxValue(int value, int maxValue)
    {
        Value = value;
        MaxValue = maxValue;
    }

    public int Value { get; set; }
    public int MaxValue { get; set; }
}

public class CharacterState
{
    /// <summary>
    ///     Находится ли персонаж без сознания
    /// </summary>
    public bool IsUnconscious { get; set; }

    /// <summary>
    ///     Имеет ли персонаж серьезную рану
    /// </summary>
    public bool HasSeriousInjury { get; set; }

    /// <summary>
    ///     Находится ли персонаж при смерти
    /// </summary>
    public bool IsDying { get; set; }

    /// <summary>
    ///     Страдает ли персонаж временным безумием
    /// </summary>
    public bool HasTemporaryInsanity { get; set; }

    /// <summary>
    ///     Страдает ли персонаж бессрочным безумием
    /// </summary>
    public bool HasIndefiniteInsanity { get; set; }
}

public class AttributeValue
{
    public AttributeValue(int regular)
    {
        Regular = regular;
        UpdateDerived();
    }

    public int Regular { get; set; }
    public int Half { get; set; }
    public int Fifth { get; set; }

    public void UpdateDerived()
    {
        Half = Regular / 2;
        Fifth = Regular / 5;
    }
}

public class Weapon
{
    public string Name { get; set; }
    public string Damage { get; set; }
    public string Range { get; set; }
    public string Attacks { get; set; }
    public string Ammo { get; set; }
    public string Malfunction { get; set; }
}

public class SkillsModel
{
    public List<SkillGroup> SkillGroups { get; set; } = [];

    public static SkillsModel DefaultSkillsModel()
    {
        return new SkillsModel()
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

public class SkillGroup
{
    public string Name { get; set; }
    public List<Skill> Skills { get; set; } = new();

    public string NewSkillName { get; set; } = "";
    public string NewSkillBaseValue { get; set; } = "";

    public void AddSkill(Skill skill)
    {
        Skills.Add(skill);
    }

    public void RemoveSkill(Skill skill)
    {
        Skills.Remove(skill);
    }
}

public class Skill
{
    public string Name { get; set; }
    public AttributeValue Value { get; set; }
    public string BaseValue { get; set; }
    public bool IsUsed { get; set; } = false;
}

public class BiographyInfo
{
    /// <summary>
    ///     Описание внешности персонажа
    /// </summary>
    public string Appearance { get; set; } = "";

    /// <summary>
    ///     Черты характера персонажа
    /// </summary>
    public string Traits { get; set; } = "";

    /// <summary>
    ///     Идеалы и принципы персонажа
    /// </summary>
    public string IdealsAndPrinciples { get; set; } = "";

    /// <summary>
    ///     Значимые для персонажа люди
    /// </summary>
    public string SignificantPeople { get; set; } = "";

    /// <summary>
    ///     Важные для персонажа места
    /// </summary>
    public string ImportantPlaces { get; set; } = "";

    /// <summary>
    ///     Ценное имущество персонажа
    /// </summary>
    public string ValuablePossessions { get; set; } = "";

    /// <summary>
    ///     Встречи со сверхъестественным
    /// </summary>
    public string SupernaturalEncounters { get; set; } = "";
}

public class Equipment
{
    /// <summary>
    ///     Список предметов снаряжения
    /// </summary>
    public List<EquipmentItem> Items { get; set; } = new();
}

public class EquipmentItem
{
    /// <summary>
    ///     Название предмета
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Описание или дополнительная информация о предмете
    /// </summary>
    public string Description { get; set; } = "";
}

public class Finances
{
    /// <summary>
    ///     Наличные деньги персонажа
    /// </summary>
    public string Cash { get; set; }

    /// <summary>
    ///     Активы персонажа
    /// </summary>
    public List<string> Assets { get; set; } = [];

    /// <summary>
    ///     Ключевые точки персонажа
    /// </summary>
    public string PocketMoney { get; set; }
}

public class CloseCombatDetails
{
    /// <summary>
    ///     Оружие ближнего боя
    /// </summary>
    public List<CloseCombatWeapon> Weapons { get; set; } = new();
}

/// <summary>
/// Модель для оружия ближнего боя.
/// </summary>
public class CloseCombatWeapon
{
    /// <summary>
    /// Название оружия.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Требуемый навык для использования оружия.
    /// </summary>
    public string Skill { get; set; } = "";

    /// <summary>
    /// Урон, наносимый оружием.
    /// </summary>
    public string Damage { get; set; } = "";

    /// <summary>
    /// Базовая дальность (для метательного или оружия с досягаемостью).
    /// </summary>
    public string Range { get; set; } = "";

    /// <summary>
    /// Количество атак за раунд.
    /// </summary>
    public string Attacks { get; set; } = "";

    /// <summary>
    /// Стоимость оружия в 1920-х годах.
    /// </summary>
    public string Cost { get; set; } = "";

    /// <summary>
    /// Дополнительные примечания (например, +БП, пронзающее (пронз.), горение).
    /// </summary>
    public string Notes { get; set; } = "";
}

public class RangedCombatDetails
{
    /// <summary>
    ///     Оружие дальнего боя
    /// </summary>
    public List<RangedCombatWeapon> Weapons { get; set; } = new();
}

/// <summary>
/// Модель для оружия дальнего боя.
/// </summary>
public class RangedCombatWeapon
{
    /// <summary>
    /// Название оружия.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Требуемый навык для использования оружия.
    /// </summary>
    public string Skill { get; set; } = "";

    /// <summary>
    /// Урон, наносимый оружием.
    /// </summary>
    public string Damage { get; set; } = "";

    /// <summary>
    /// Базовая дальность стрельбы.
    /// </summary>
    public string Range { get; set; } = "";

    /// <summary>
    /// Количество атак/выстрелов за раунд.
    /// </summary>
    public string Attacks { get; set; } = "";

    /// <summary>
    /// Количество патронов в оружии (магазине).
    /// </summary>
    public string Ammo { get; set; } = "";

    /// <summary>
    /// Шанс неисправности оружия (осечки).
    /// </summary>
    public string Malfunction { get; set; } = "";

    /// <summary>
    /// Стоимость оружия в 1920-х годах.
    /// </summary>
    public string Cost { get; set; } = "";

    /// <summary>
    /// Дополнительные примечания (например, пронзающее (пронз.), 2 ствола (2ств.), автоматический огонь (авто)).
    /// </summary>
    public string Notes { get; set; } = "";
}