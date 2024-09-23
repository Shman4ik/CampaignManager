using System;
using System.Collections.Generic;
using System.Linq;
using CampaignManager.Web.Model;

public class CharacterGenerationService
{
    private Random random = new Random();

    public Character GenerateRandomCharacter()
    {
        var character = new Character();
        
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
        int hitPoints = (character.Characteristics.Size.Regular + character.Characteristics.Constitution.Regular) / 10;
        character.DerivedAttributes.HitPoints = new AttributeWithMaxValue(hitPoints, hitPoints);

        // Calculate Magic Points
        int magicPoints = character.Characteristics.Power.Regular / 5;
        character.DerivedAttributes.MagicPoints = new AttributeWithMaxValue(magicPoints, magicPoints);

        // Calculate Sanity
        int sanity = character.Characteristics.Power.Regular;
        character.DerivedAttributes.Sanity = new AttributeWithMaxValue(sanity, 99); // Max sanity is always 99

        // Calculate Luck
        int luck = (random.Next(1, 7) + random.Next(1, 7) + random.Next(1, 7)) * 5;
        character.DerivedAttributes.Luck = new AttributeWithMaxValue(luck, luck);

        // Calculate Move Rate
        character.PersonalInfo.MoveSpeed = CalculateMoveRate(character);

        // Calculate Build and Damage Bonus
        CalculateBuildAndDamageBonus(character);
    }

    private int CalculateMoveRate(Character character)
    {
        int str = character.Characteristics.Strength.Regular;
        int dex = character.Characteristics.Dexterity.Regular;
        int siz = character.Characteristics.Size.Regular;

        if (str < siz && dex < siz) return 7;
        if (str > siz && dex > siz) return 9;
        return 8;
    }

    private void CalculateBuildAndDamageBonus(Character character)
    {
        int strength = character.Characteristics.Strength.Regular;
        int size = character.Characteristics.Size.Regular;
        int sum = strength + size;

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
        // This is a simplified version. You might want to expand this with more detailed random generation
        character.PersonalInfo.Name = GenerateRandomName();
        character.PersonalInfo.Occupation = GenerateRandomOccupation();
        character.PersonalInfo.Age = random.Next(15, 90);
        character.PersonalInfo.Gender = random.Next(2) == 0 ? "Мужской" : "Женский";
        character.PersonalInfo.Residence = "Аркхем"; // Default for simplicity
        character.PersonalInfo.Birthplace = "Аркхем"; // Default for simplicity
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
        foreach (var skillGroup in character.Skills.SkillGroups)
        {
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
                    int baseValue = int.Parse(skill.BaseValue.TrimEnd('%'));
                    skill.Value.Regular = baseValue + random.Next(0, 70); // Случайное увеличение до 70%
                }
                // Для навыков с другими базовыми значениями (например, "00%") оставляем начальное значение

                skill.Value.UpdateDerived();
            }
        }
    }
}