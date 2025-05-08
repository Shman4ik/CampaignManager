using CampaignManager.Web.Model;

namespace CampaignManager.Web.Services;

public class CharacterGenerationService
{
    private readonly Random random = new();

    public Character GenerateRandomCharacter()
    {
        Character character = new();

        GenerateCharacteristics(character);
        CalculateDerivedAttributes(character);
        GeneratePersonalInfo(character);
        GenerateSkills(character);

        return character;
    }

    private void GenerateCharacteristics(Character character)
    {
        character.Characteristics.Strength = new AttributeValue(RollCharacteristic());
        character.Characteristics.Constitution = new AttributeValue(RollCharacteristic());
        character.Characteristics.Size = new AttributeValue(RollCharacteristic());
        character.Characteristics.Dexterity = new AttributeValue(RollCharacteristic());
        character.Characteristics.Appearance = new AttributeValue(RollCharacteristic());
        character.Characteristics.Intelligence = new AttributeValue(RollCharacteristic());
        character.Characteristics.Power = new AttributeValue(RollCharacteristic());
        character.Characteristics.Education = new AttributeValue(RollCharacteristic());
    }

    private int RollCharacteristic()
    {
        // 3d6 * 5 for most characteristics
        return (random.Next(1, 7) + random.Next(1, 7) + random.Next(1, 7)) * 5;
    }

    private void CalculateDerivedAttributes(Character character)
    {
        // Calculate Hit Points
        var hitPoints = (character.Characteristics.Size.Regular + character.Characteristics.Constitution.Regular) / 10;
        character.DerivedAttributes.HitPoints = new AttributeWithMaxValue(hitPoints, hitPoints);

        // Calculate Magic Points
        var magicPoints = character.Characteristics.Power.Regular / 5;
        character.DerivedAttributes.MagicPoints = new AttributeWithMaxValue(magicPoints, magicPoints);

        // Calculate Sanity
        var sanity = character.Characteristics.Power.Regular;
        character.DerivedAttributes.Sanity = new AttributeWithMaxValue(sanity, 99); // Max sanity is always 99

        // Calculate Luck
        var luck = (random.Next(1, 7) + random.Next(1, 7) + random.Next(1, 7)) * 5;
        character.DerivedAttributes.Luck = new AttributeWithMaxValue(luck, luck);

        // Calculate Move Rate
        character.PersonalInfo.MoveSpeed = CalculateMoveRate(character);

        // Calculate Build and Damage Bonus
        CalculateBuildAndDamageBonus(character);

        // Calculate Dodge value
        character.PersonalInfo.Dodge = character.Characteristics.Dexterity.Regular / 2;
    }

    private int CalculateMoveRate(Character character)
    {
        var str = character.Characteristics.Strength.Regular;
        var dex = character.Characteristics.Dexterity.Regular;
        var siz = character.Characteristics.Size.Regular;

        if (str < siz && dex < siz) return 7;

        if (str > siz && dex > siz) return 9;

        return 8;
    }

    private void CalculateBuildAndDamageBonus(Character character)
    {
        var strength = character.Characteristics.Strength.Regular;
        var size = character.Characteristics.Size.Regular;
        var sum = strength + size;

        if (sum >= 2 && sum <= 64)
        {
            character.PersonalInfo.Build = "-2";
            character.PersonalInfo.DamageBonus = "-2";
        }
        else if (sum >= 65 && sum <= 84)
        {
            character.PersonalInfo.Build = "-1";
            character.PersonalInfo.DamageBonus = "-1";
        }
        else if (sum >= 85 && sum <= 124)
        {
            character.PersonalInfo.Build = "0";
            character.PersonalInfo.DamageBonus = "0";
        }
        else if (sum >= 125 && sum <= 164)
        {
            character.PersonalInfo.Build = "1";
            character.PersonalInfo.DamageBonus = "+1D4";
        }
        else
        {
            character.PersonalInfo.Build = "2";
            character.PersonalInfo.DamageBonus = "+1D6";
        }
    }

    private void GeneratePersonalInfo(Character character)
    {
        // Сохраняем текущее имя игрока
        var currentPlayerName = character.PersonalInfo.PlayerName;

        // Генерируем случайные данные
        character.PersonalInfo.Name = GenerateRandomName();
        character.PersonalInfo.Occupation = GenerateRandomOccupation();
        character.PersonalInfo.Age = random.Next(15, 90);
        character.PersonalInfo.Gender = random.Next(2) == 0 ? "Мужской" : "Женский";
        character.PersonalInfo.Residence = "Аркхем"; // Default for simplicity
        character.PersonalInfo.Birthplace = "Аркхем"; // Default for simplicity

        // Восстанавливаем имя игрока
        if (!string.IsNullOrEmpty(currentPlayerName)) character.PersonalInfo.PlayerName = currentPlayerName;
    }

    private string GenerateRandomName()
    {
        string[] firstNames = { "Джон", "Мэри", "Роберт", "Патриция", "Майкл", "Дженнифер", "Уильям", "Линда", "Дэвид", "Элизабет" };
        string[] lastNames = { "Смит", "Джонсон", "Уильямс", "Браун", "Джонс", "Гарсия", "Миллер", "Дэвис", "Родригес", "Мартинес" };

        return $"{firstNames[random.Next(firstNames.Length)]} {lastNames[random.Next(lastNames.Length)]}";
    }

    private string GenerateRandomOccupation()
    {
        string[] occupations = { "Антиквар", "Автор", "Бухгалтер", "Актер", "Археолог", "Художник", "Атлет", "Бармен", "Ботаник", "Священник" };
        return occupations[random.Next(occupations.Length)];
    }

    private void GenerateSkills(Character character)
    {
        character.Skills = SkillsModel.DefaultSkillsModel();
        foreach (var skillGroup in character.Skills.SkillGroups)
        foreach (var skill in skillGroup.Skills)
        {
            if (skill.Name == "Уклонение")
            {
                skill.Value.Regular = character.Characteristics.Dexterity.Regular / 2;
                character.PersonalInfo.Dodge = skill.Value.Regular;
            }
            else if (skill.Name == "Языки (родной)")
            {
                skill.Value.Regular = character.Characteristics.Education.Regular;
            }
            else if (skill.BaseValue.EndsWith("%"))
            {
                var baseValue = int.Parse(skill.BaseValue.TrimEnd('%'));
                skill.Value.Regular = baseValue + random.Next(0, 70); // Случайное увеличение до 70%
            }

            // Для навыков с другими базовыми значениями (например, "00%") оставляем начальное значение
            skill.Value.UpdateDerived();
        }
    }
}