using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Characters.Services;

public sealed class CharacterGenerationService
{
    private readonly Random _random = Random.Shared;

    /// <summary>
    /// Результат генерации: персонаж + лог
    /// </summary>
    public sealed class GenerationResult
    {
        public required Character Character { get; init; }
        public required CharacterGenerationLog Log { get; init; }
        public required Occupation Occupation { get; init; }
    }

    /// <summary>
    /// Генерирует случайного персонажа. Принимает список доступных профессий из БД и опциональные параметры.
    /// </summary>
    public GenerationResult GenerateRandomCharacter(
        List<Occupation> availableOccupations,
        Occupation? selectedOccupation = null,
        string? gender = null,
        int? age = null)
    {
        var log = new CharacterGenerationLog();
        var character = new Character();

        // 1. Выбор профессии
        var occupations = availableOccupations.Count > 0 ? availableOccupations : Occupation.GetDefaultOccupations();
        var occupation = selectedOccupation ?? PickOccupation(occupations, log);

        // 2. Генерация возраста (15–89 лет)
        var characterAge = age ?? GenerateAge(log);
        if (age.HasValue)
            log.Add("Возраст", $"Заданный возраст: {characterAge}", details: AgeCategory(characterAge));

        // 3. Характеристики по правилам CoC 7e
        GenerateCharacteristics(character, log);

        // 4. Модификаторы возраста
        ApplyAgeModifiers(character, characterAge, log);

        // 5. Производные атрибуты
        CalculateDerivedAttributes(character, characterAge, log);

        // 6. Навыки: база → профессия → личный интерес
        GenerateSkills(character, occupation, log);

        // 7. Личная информация
        GeneratePersonalInfo(character, occupation, characterAge, gender, log);

        // 8. Биография
        GenerateBiography(character, log);

        // 9. Финансы по таблице «Наличные и активы»
        CalculateFinances(character, log);

        return new GenerationResult
        {
            Character = character,
            Log = log,
            Occupation = occupation
        };
    }

    #region Профессия

    private Occupation PickOccupation(List<Occupation> occupations, CharacterGenerationLog log)
    {
        var occupation = occupations[_random.Next(occupations.Count)];
        log.Add("Профессия", $"Выбрана профессия: {occupation.Name}",
            details: $"Формула очков навыков: {FormatFormula(occupation.SkillPointFormula)}, Средства: {occupation.CreditRatingMin}–{occupation.CreditRatingMax}%");
        return occupation;
    }

    private static string FormatFormula(OccupationSkillPointFormula f) => f switch
    {
        OccupationSkillPointFormula.Edu4 => "ОБР × 4",
        OccupationSkillPointFormula.Edu2Dex2 => "ОБР × 2 + ЛВК × 2",
        OccupationSkillPointFormula.Edu2App2 => "ОБР × 2 + НАР × 2",
        OccupationSkillPointFormula.Edu2Str2 => "ОБР × 2 + СИЛ × 2",
        OccupationSkillPointFormula.Edu2Pow2 => "ОБР × 2 + МОЩ × 2",
        OccupationSkillPointFormula.Edu2DexOrStr2 => "ОБР × 2 + max(ЛВК, СИЛ) × 2",
        OccupationSkillPointFormula.Edu2AppOrPow2 => "ОБР × 2 + НАР × 2 или ОБР × 2 + МОЩ × 2",
        OccupationSkillPointFormula.Edu2DexOrPow2 => "ОБР × 2 + ЛВК × 2 или ОБР × 2 + МОЩ × 2",
        OccupationSkillPointFormula.Edu2AppOrDexOrStr2 => "ОБР × 2 + НАР × 2 / ЛВК × 2 / СИЛ × 2",
        _ => "ОБР × 4"
    };

    #endregion

    #region Возраст

    private int GenerateAge(CharacterGenerationLog log)
    {
        // Взвешенное распределение: большинство персонажей 20–49
        var roll = _random.Next(100);
        var age = roll switch
        {
            < 5 => _random.Next(15, 20),   // 5%  — юный
            < 55 => _random.Next(20, 40),   // 50% — молодой
            < 80 => _random.Next(40, 50),   // 25% — средний
            < 92 => _random.Next(50, 60),   // 12% — зрелый
            < 97 => _random.Next(60, 70),   // 5%  — пожилой
            _ => _random.Next(70, 90)       // 3%  — старый/престарелый
        };
        log.Add("Возраст", $"Случайный возраст: {age}",
            details: AgeCategory(age));
        return age;
    }

    private static string AgeCategory(int age) => age switch
    {
        < 20 => "Юный (15–19): –5 к ОБР, –5 к СИЛ и/или ТЕЛ, удача бросается дважды",
        < 40 => "Молодой (20–39): 1 проверка улучшения ОБР",
        < 50 => "Средний (40–49): –5 к НАР, –5 к СИЛ/ВЫН/ЛВК, 2 проверки улучшения ОБР",
        < 60 => "Зрелый (50–59): –10 к НАР, –10 к СИЛ/ВЫН/ЛВК, 3 проверки улучшения ОБР",
        < 70 => "Пожилой (60–69): –15 к НАР, –20 к СИЛ/ВЫН/ЛВК, 4 проверки улучшения ОБР",
        < 80 => "Старый (70–79): –20 к НАР, –40 к СИЛ/ВЫН/ЛВК, 4 проверки улучшения ОБР",
        _ => "Престарелый (80+): –25 к НАР, –80 к СИЛ/ВЫН/ЛВК, 4 проверки улучшения ОБР"
    };

    #endregion

    #region Характеристики

    private void GenerateCharacteristics(Character character, CharacterGenerationLog log)
    {
        log.Add("Характеристики", "--- Генерация характеристик ---");

        // СИЛ, ВЫН, ЛВК, НАР, МОЩ — 3d6 × 5
        character.Characteristics.Strength = RollAndLog3d6x5("СИЛ (Сила)", log);
        character.Characteristics.Constitution = RollAndLog3d6x5("ВЫН (Выносливость)", log);
        character.Characteristics.Dexterity = RollAndLog3d6x5("ЛВК (Ловкость)", log);
        character.Characteristics.Appearance = RollAndLog3d6x5("НАР (Наружность)", log);
        character.Characteristics.Power = RollAndLog3d6x5("МОЩ (Мощь)", log);

        // ИНТ, ТЕЛ, ОБР — (2d6+6) × 5
        character.Characteristics.Intelligence = RollAndLog2d6p6x5("ИНТ (Интеллект)", log);
        character.Characteristics.Size = RollAndLog2d6p6x5("ТЕЛ (Телосложение)", log);
        character.Characteristics.Education = RollAndLog2d6p6x5("ОБР (Образование)", log);
    }

    private AttributeValue RollAndLog3d6x5(string name, CharacterGenerationLog log)
    {
        var d1 = _random.Next(1, 7);
        var d2 = _random.Next(1, 7);
        var d3 = _random.Next(1, 7);
        var sum = d1 + d2 + d3;
        var result = sum * 5;
        log.Add("Характеристики", name,
            diceRoll: $"3d6 = ({d1} + {d2} + {d3}) = {sum} × 5",
            result: result);
        return new AttributeValue(result);
    }

    private AttributeValue RollAndLog2d6p6x5(string name, CharacterGenerationLog log)
    {
        var d1 = _random.Next(1, 7);
        var d2 = _random.Next(1, 7);
        var sum = d1 + d2 + 6;
        var result = sum * 5;
        log.Add("Характеристики", name,
            diceRoll: $"(2d6+6) = ({d1} + {d2} + 6) = {sum} × 5",
            result: result);
        return new AttributeValue(result);
    }

    #endregion

    #region Модификаторы возраста

    private void ApplyAgeModifiers(Character character, int age, CharacterGenerationLog log)
    {
        if (age < 20)
        {
            // Юный: –5 ОБР, –5 суммарно из СИЛ и/или ТЕЛ, удача дважды — лучший
            log.Add("Возраст", "Юный возраст: –5 к ОБР, –5 суммарно к СИЛ и/или ТЕЛ");
            var eduBefore = character.Characteristics.Education.Regular;
            character.Characteristics.Education = new AttributeValue(Math.Max(1, eduBefore - 5));
            log.Add("Возраст", "ОБР уменьшена", result: character.Characteristics.Education.Regular,
                details: $"{eduBefore} → {character.Characteristics.Education.Regular}");

            // Распределяем –5 случайно между СИЛ и ТЕЛ (SIZE)
            DistributeYouthReduction(character, 5, log);
        }
        else if (age is >= 20 and <= 39)
        {
            // Молодой: улучшение ОБР
            TryImproveEducation(character, 1, log);
        }
        else if (age is >= 40 and <= 49)
        {
            // Средний: –5 ВНШ, –5 распределяемых по СИЛ/КОН/ЛВК, улучшение ОБР ×2
            ApplyAppReduction(character, 5, log);
            DistributePhysicalReduction(character, 5, log);
            TryImproveEducation(character, 2, log);
        }
        else if (age is >= 50 and <= 59)
        {
            ApplyAppReduction(character, 10, log);
            DistributePhysicalReduction(character, 10, log);
            TryImproveEducation(character, 3, log);
        }
        else if (age is >= 60 and <= 69)
        {
            ApplyAppReduction(character, 15, log);
            DistributePhysicalReduction(character, 20, log);
            TryImproveEducation(character, 4, log);
        }
        else if (age is >= 70 and <= 79)
        {
            ApplyAppReduction(character, 20, log);
            DistributePhysicalReduction(character, 40, log);
            TryImproveEducation(character, 4, log);
        }
        else if (age >= 80)
        {
            ApplyAppReduction(character, 25, log);
            DistributePhysicalReduction(character, 80, log);
            TryImproveEducation(character, 4, log);
        }
    }

    private void DistributeYouthReduction(Character character, int totalReduction, CharacterGenerationLog log)
    {
        log.Add("Возраст", $"Распределение –{totalReduction} между СИЛ и ТЕЛ");
        var remaining = totalReduction;

        while (remaining > 0)
        {
            var pick = _random.Next(2); // 0 = СИЛ, 1 = ТЕЛ
            var reduce = Math.Min(remaining, _random.Next(1, remaining + 1));

            if (pick == 0 && character.Characteristics.Strength.Regular > 1)
            {
                var before = character.Characteristics.Strength.Regular;
                var actual = Math.Min(reduce, before - 1);
                character.Characteristics.Strength = new AttributeValue(before - actual);
                log.Add("Возраст", $"  СИЛ –{actual}", result: character.Characteristics.Strength.Regular,
                    details: $"{before} → {character.Characteristics.Strength.Regular}");
                remaining -= actual;
            }
            else if (pick == 1 && character.Characteristics.Size.Regular > 1)
            {
                var before = character.Characteristics.Size.Regular;
                var actual = Math.Min(reduce, before - 1);
                character.Characteristics.Size = new AttributeValue(before - actual);
                log.Add("Возраст", $"  ТЕЛ –{actual}", result: character.Characteristics.Size.Regular,
                    details: $"{before} → {character.Characteristics.Size.Regular}");
                remaining -= actual;
            }
            else if (character.Characteristics.Strength.Regular <= 1 && character.Characteristics.Size.Regular <= 1)
            {
                remaining = 0;
            }
        }
    }

    private void ApplyAppReduction(Character character, int amount, CharacterGenerationLog log)
    {
        var before = character.Characteristics.Appearance.Regular;
        character.Characteristics.Appearance = new AttributeValue(Math.Max(1, before - amount));
        log.Add("Возраст", $"НАР –{amount}", result: character.Characteristics.Appearance.Regular,
            details: $"{before} → {character.Characteristics.Appearance.Regular}");
    }

    private void DistributePhysicalReduction(Character character, int totalReduction, CharacterGenerationLog log)
    {
        // Распределяем снижение случайно между СИЛ, ВЫН, ЛВК
        log.Add("Возраст", $"Распределение –{totalReduction} между СИЛ/ВЫН/ЛВК");
        var remaining = totalReduction;

        while (remaining > 0)
        {
            var pick = _random.Next(3);
            var reduce = Math.Min(remaining, _random.Next(1, remaining + 1));

            switch (pick)
            {
                case 0 when character.Characteristics.Strength.Regular > 1:
                {
                    var before = character.Characteristics.Strength.Regular;
                    var actual = Math.Min(reduce, before - 1);
                    character.Characteristics.Strength = new AttributeValue(before - actual);
                    log.Add("Возраст", $"  СИЛ –{actual}", result: character.Characteristics.Strength.Regular,
                        details: $"{before} → {character.Characteristics.Strength.Regular}");
                    remaining -= actual;
                    break;
                }
                case 1 when character.Characteristics.Constitution.Regular > 1:
                {
                    var before = character.Characteristics.Constitution.Regular;
                    var actual = Math.Min(reduce, before - 1);
                    character.Characteristics.Constitution = new AttributeValue(before - actual);
                    log.Add("Возраст", $"  ВЫН –{actual}", result: character.Characteristics.Constitution.Regular,
                        details: $"{before} → {character.Characteristics.Constitution.Regular}");
                    remaining -= actual;
                    break;
                }
                case 2 when character.Characteristics.Dexterity.Regular > 1:
                {
                    var before = character.Characteristics.Dexterity.Regular;
                    var actual = Math.Min(reduce, before - 1);
                    character.Characteristics.Dexterity = new AttributeValue(before - actual);
                    log.Add("Возраст", $"  ЛВК –{actual}", result: character.Characteristics.Dexterity.Regular,
                        details: $"{before} → {character.Characteristics.Dexterity.Regular}");
                    remaining -= actual;
                    break;
                }
                default:
                    // Все характеристики на минимуме
                    if (character.Characteristics.Strength.Regular <= 1 &&
                        character.Characteristics.Constitution.Regular <= 1 &&
                        character.Characteristics.Dexterity.Regular <= 1)
                    {
                        remaining = 0;
                    }
                    break;
            }
        }
    }

    private void TryImproveEducation(Character character, int attempts, CharacterGenerationLog log)
    {
        for (var i = 0; i < attempts; i++)
        {
            var roll = _random.Next(1, 101);
            var edu = character.Characteristics.Education.Regular;
            if (roll > edu)
            {
                var improvement = _random.Next(1, 11); // 1d10
                var newEdu = Math.Min(99, edu + improvement);
                character.Characteristics.Education = new AttributeValue(newEdu);
                log.Add("Возраст", $"Улучшение ОБР (попытка {i + 1})",
                    diceRoll: $"1d100 = {roll} > {edu} → успех, +1d10 = {improvement}",
                    result: newEdu,
                    details: $"{edu} → {newEdu}");
            }
            else
            {
                log.Add("Возраст", $"Улучшение ОБР (попытка {i + 1})",
                    diceRoll: $"1d100 = {roll} ≤ {edu} → неудача");
            }
        }
    }

    #endregion

    #region Производные атрибуты

    private void CalculateDerivedAttributes(Character character, int age, CharacterGenerationLog log)
    {
        log.Add("Производные", "--- Расчёт производных характеристик ---");

        // Хиты: (ТЕЛ + ВЫН) / 10
        var hp = (character.Characteristics.Size.Regular + character.Characteristics.Constitution.Regular) / 10;
        character.DerivedAttributes.HitPoints = new AttributeWithMaxValue(hp, hp);
        log.Add("Производные", "Очки здоровья = (ТЕЛ + ВЫН) / 10",
            result: hp,
            details: $"({character.Characteristics.Size.Regular} + {character.Characteristics.Constitution.Regular}) / 10 = {hp}");

        // Магия: МОЩ / 5
        var mp = character.Characteristics.Power.Regular / 5;
        character.DerivedAttributes.MagicPoints = new AttributeWithMaxValue(mp, mp);
        log.Add("Производные", "Очки магии = МОЩ / 5",
            result: mp,
            details: $"{character.Characteristics.Power.Regular} / 5 = {mp}");

        // Рассудок = МОЩ
        var sanity = character.Characteristics.Power.Regular;
        character.DerivedAttributes.Sanity = new AttributeWithMaxValue(sanity, 99);
        log.Add("Производные", "Рассудок = МОЩ", result: sanity);

        // Удача: 3d6 × 5
        var luck = RollLuck(age, log);
        character.DerivedAttributes.Luck = new AttributeWithMaxValue(luck, luck);

        // Скорость (c учётом возраста)
        var move = CalculateMoveRate(character, age);
        character.PersonalInfo.MoveSpeed = move;
        log.Add("Производные", "Скорость передвижения", result: move,
            details: age >= 40
                ? $"СИЛ={character.Characteristics.Strength.Regular}, ЛВК={character.Characteristics.Dexterity.Regular}, ТЕЛ={character.Characteristics.Size.Regular}, возраст {age} (–{(age - 40) / 10 + 1} за возраст)"
                : $"СИЛ={character.Characteristics.Strength.Regular}, ЛВК={character.Characteristics.Dexterity.Regular}, ТЕЛ={character.Characteristics.Size.Regular}");

        // Телосложение и бонус к урону
        CalculateBuildAndDamageBonus(character, log);

        // Уклонение = ЛВК / 2
        character.PersonalInfo.Dodge = character.Characteristics.Dexterity.Regular / 2;
        log.Add("Производные", "Уклонение = ЛВК / 2", result: character.PersonalInfo.Dodge);
    }

    private int RollLuck(int age, CharacterGenerationLog log)
    {
        var roll1 = Roll3d6x5();

        if (age < 20)
        {
            // CoC 7e: юные бросают удачу дважды и берут лучший
            var roll2 = Roll3d6x5();
            var best = Math.Max(roll1, roll2);
            log.Add("Производные", "Удача (юный: 2 броска, лучший)",
                diceRoll: $"Бросок 1 = {roll1}, Бросок 2 = {roll2} → лучший = {best}",
                result: best);
            return best;
        }

        log.Add("Производные", "Удача",
            diceRoll: $"3d6 × 5 = {roll1}",
            result: roll1);
        return roll1;
    }

    private int Roll3d6x5()
    {
        var d1 = _random.Next(1, 7);
        var d2 = _random.Next(1, 7);
        var d3 = _random.Next(1, 7);
        return (d1 + d2 + d3) * 5;
    }

    private static int CalculateMoveRate(Character character, int age)
    {
        var str = character.Characteristics.Strength.Regular;
        var dex = character.Characteristics.Dexterity.Regular;
        var siz = character.Characteristics.Size.Regular;

        int baseMove;
        if (str < siz && dex < siz) baseMove = 7;
        else if (str > siz && dex > siz) baseMove = 9;
        else baseMove = 8;

        // CoC 7e: –1 скорость за каждое десятилетие свыше 40
        if (age >= 40)
        {
            var decades = (age - 40) / 10 + 1;
            baseMove = Math.Max(1, baseMove - decades);
        }

        return baseMove;
    }

    private static void CalculateBuildAndDamageBonus(Character character, CharacterGenerationLog log)
    {
        var sum = character.Characteristics.Strength.Regular + character.Characteristics.Size.Regular;

        var (build, dmg) = sum switch
        {
            >= 2 and <= 64 => ("-2", "-2"),
            >= 65 and <= 84 => ("-1", "-1"),
            >= 85 and <= 124 => ("0", "0"),
            >= 125 and <= 164 => ("1", "+1D4"),
            >= 165 and <= 204 => ("2", "+1D6"),
            >= 205 and <= 284 => ("3", "+2D6"),
            >= 285 and <= 364 => ("4", "+3D6"),
            >= 365 and <= 444 => ("5", "+4D6"),
            _ => ($"{(sum - 365) / 80 + 5}", $"+{(sum - 365) / 80 + 4}D6")
        };

        character.PersonalInfo.Build = build;
        character.PersonalInfo.DamageBonus = dmg;
        log.Add("Производные", $"Комплекция={build}, Бонус к урону={dmg}",
            details: $"СИЛ + ТЕЛ = {sum}");
    }

    #endregion

    #region Навыки

    private void GenerateSkills(Character character, Occupation occupation, CharacterGenerationLog log)
    {
        character.Skills = SkillsModel.DefaultSkillsModel();
        var allSkills = character.Skills.SkillGroups.SelectMany(g => g.Skills).ToList();

        // 1. Установить базовые значения Уклонения и Родного языка
        log.Add("Навыки", "--- Установка базовых значений навыков ---");

        var dodge = allSkills.FirstOrDefault(s => s.Name == "Уклонение");
        if (dodge is not null)
        {
            dodge.Value.Regular = character.Characteristics.Dexterity.Regular / 2;
            dodge.Value.UpdateDerived();
            character.PersonalInfo.Dodge = dodge.Value.Regular;
            log.Add("Навыки", "Уклонение = ЛВК / 2", result: dodge.Value.Regular);
        }

        var ownLang = allSkills.FirstOrDefault(s => s.Name == "Языки (родной)");
        if (ownLang is not null)
        {
            ownLang.Value.Regular = character.Characteristics.Education.Regular;
            ownLang.Value.UpdateDerived();
            log.Add("Навыки", "Языки (родной) = ОБР", result: ownLang.Value.Regular);
        }

        // 2. Очки навыков профессии
        var occupationPoints = occupation.CalculateSkillPoints(character.Characteristics);
        log.Add("Навыки", $"--- Очки профессии ({occupation.Name}) ---",
            details: $"{FormatFormula(occupation.SkillPointFormula)} = {occupationPoints}");

        // Собрать навыки профессии (исключая Мифы Ктулху)
        var occupationSkillNames = occupation.OccupationSkills
            .Where(n => n != "Мифы Ктулху")
            .ToList();

        // Добавить социальные слоты — случайный выбор из пула
        if (occupation.SocialSkillSlots > 0)
        {
            string[] socialPool = ["Запугивание", "Красноречие", "Обаяние", "Убеждение"];
            var availableSocial = socialPool.Where(s => !occupationSkillNames.Contains(s)).ToList();
            var socialChoices = availableSocial.OrderBy(_ => _random.Next()).Take(occupation.SocialSkillSlots).ToList();
            foreach (var social in socialChoices)
            {
                occupationSkillNames.Add(social);
                log.Add("Навыки", $"Социальный слот профессии: {social}");
            }
        }

        // Добавить свободные слоты — выбрать случайные навыки, которых нет в списке профессии
        var freeChoices = allSkills
            .Where(s => s.Name != "Мифы Ктулху" && s.Name != "Уклонение" && !occupationSkillNames.Contains(s.Name))
            .OrderBy(_ => _random.Next())
            .Take(occupation.FreeSkillSlots)
            .Select(s => s.Name)
            .ToList();

        foreach (var free in freeChoices)
        {
            occupationSkillNames.Add(free);
            log.Add("Навыки", $"Свободный слот профессии: {free}");
        }

        // Распределить очки профессии
        DistributeOccupationPoints(allSkills, occupationSkillNames, occupationPoints, occupation, log);

        // 3. Очки личного интереса: ИНТ × 2
        var personalPoints = character.Characteristics.Intelligence.Regular * 2;
        log.Add("Навыки", "--- Очки личного интереса ---",
            details: $"ИНТ × 2 = {character.Characteristics.Intelligence.Regular} × 2 = {personalPoints}");

        DistributePersonalInterestPoints(allSkills, occupationSkillNames, personalPoints, log);

        // Обновить производные для всех навыков
        foreach (var skill in allSkills)
            skill.Value.UpdateDerived();
    }

    private void DistributeOccupationPoints(List<Skill> allSkills, List<string> occupationSkillNames, int totalPoints, Occupation occupation, CharacterGenerationLog log)
    {
        // Сначала обеспечим Средства в допустимом диапазоне
        var creditRating = allSkills.FirstOrDefault(s => s.Name == "Средства");
        var pointsUsedForCredit = 0;
        if (creditRating is not null && occupationSkillNames.Contains("Средства"))
        {
            var crValue = _random.Next(occupation.CreditRatingMin, occupation.CreditRatingMax + 1);
            creditRating.Value.Regular = crValue;
            pointsUsedForCredit = crValue;
            log.Add("Навыки", "Средства (Кредитный рейтинг)", result: crValue,
                details: $"Диапазон профессии: {occupation.CreditRatingMin}–{occupation.CreditRatingMax}%, потрачено {crValue} очков");
        }

        var remaining = totalPoints - pointsUsedForCredit;
        if (remaining <= 0) return;

        // Навыки профессии без Средств
        var targetSkills = allSkills
            .Where(s => occupationSkillNames.Contains(s.Name) && s.Name != "Средства" && s.Name != "Мифы Ктулху")
            .ToList();

        if (targetSkills.Count == 0) return;

        // Умное распределение: пирамида — первые 2 навыка получают 60% очков, остальные 40%
        var shuffled = targetSkills.OrderBy(_ => _random.Next()).ToList();
        var primaryCount = Math.Min(2, shuffled.Count);
        var primarySkills = shuffled.Take(primaryCount).ToList();
        var secondarySkills = shuffled.Skip(primaryCount).ToList();

        var primaryPool = (int)(remaining * 0.6);
        var secondaryPool = remaining - primaryPool;

        var leftover = DistributePointsToGroup(primarySkills, primaryPool, 80, "профессия", "🎓", log);
        leftover = DistributePointsToGroup(secondarySkills.Count > 0 ? secondarySkills : primarySkills, secondaryPool + leftover, 80, "профессия", "🎓", log);

        if (leftover > 0)
        {
            log.Add("Навыки", $"Нераспределённые очки профессии: {leftover}");
        }
    }

    private void DistributePersonalInterestPoints(List<Skill> allSkills, List<string> occupationSkillNames, int totalPoints, CharacterGenerationLog log)
    {
        // Личные интересы нельзя тратить на Мифы Ктулху и Средства
        var eligibleSkills = allSkills
            .Where(s => s.Name != "Мифы Ктулху" && s.Name != "Средства")
            .ToList();

        if (eligibleSkills.Count == 0) return;

        // Умное распределение: пирамида — приоритет нескольким навыкам
        var shuffled = eligibleSkills.OrderBy(_ => _random.Next()).ToList();
        var primaryCount = Math.Min(4, shuffled.Count);
        var primarySkills = shuffled.Take(primaryCount).ToList();
        var secondarySkills = shuffled.Skip(primaryCount).ToList();

        var primaryPool = (int)(totalPoints * 0.7);
        var secondaryPool = totalPoints - primaryPool;

        var leftover = DistributePointsToGroup(primarySkills, primaryPool, 80, "личный интерес", "📚", log);
        leftover = DistributePointsToGroup(secondarySkills.Count > 0 ? secondarySkills : primarySkills, secondaryPool + leftover, 80, "личный интерес", "📚", log);

        if (leftover > 0)
        {
            log.Add("Навыки", $"Нераспределённые очки личного интереса: {leftover}");
        }
    }

    private int DistributePointsToGroup(List<Skill> skills, int totalPoints, int cap, string source, string emoji, CharacterGenerationLog log)
    {
        var remaining = totalPoints;
        while (remaining > 0 && skills.Any(s => s.Value.Regular < cap))
        {
            var skill = skills[_random.Next(skills.Count)];
            if (skill.Value.Regular >= cap) continue;

            var maxAdd = Math.Min(remaining, cap - skill.Value.Regular);
            var add = _random.Next(1, Math.Max(2, maxAdd + 1));
            add = Math.Min(add, remaining);

            var before = skill.Value.Regular;
            skill.Value.Regular += add;
            remaining -= add;

            log.Add("Навыки", $"{emoji} {skill.Name} ({source})",
                result: skill.Value.Regular,
                details: $"{before} + {add} = {skill.Value.Regular} (осталось {remaining} оч.)");
        }
        return remaining;
    }

    #endregion

    #region Личная информация

    private void GeneratePersonalInfo(Character character, Occupation occupation, int age, string? gender, CharacterGenerationLog log)
    {
        log.Add("Персонаж", "--- Личные данные ---");

        var currentPlayerName = character.PersonalInfo.PlayerName;

        var actualGender = gender ?? (_random.Next(2) == 0 ? "Мужской" : "Женский");
        character.PersonalInfo.Gender = actualGender;
        character.PersonalInfo.Name = GenerateRandomName(actualGender == "Мужской");
        character.PersonalInfo.Occupation = occupation.Name;
        character.PersonalInfo.Age = age;

        var (birthplace, residence) = GenerateLocations();
        character.PersonalInfo.Birthplace = birthplace;
        character.PersonalInfo.Residence = residence;

        if (!string.IsNullOrEmpty(currentPlayerName))
            character.PersonalInfo.PlayerName = currentPlayerName;

        log.Add("Персонаж", $"Имя: {character.PersonalInfo.Name}");
        log.Add("Персонаж", $"Пол: {character.PersonalInfo.Gender}");
        log.Add("Персонаж", $"Профессия: {occupation.Name}");
        log.Add("Персонаж", $"Возраст: {age}");
        log.Add("Персонаж", $"Место рождения: {birthplace}");
        log.Add("Персонаж", $"Место жительства: {residence}");
    }

    private string GenerateRandomName(bool isMale)
    {
        string[] maleFirstNames =
        [
            "Джон", "Роберт", "Майкл", "Уильям", "Дэвид", "Чарльз", "Томас", "Джеймс",
            "Ричард", "Генри", "Артур", "Эдвард", "Фрэнк", "Джордж", "Гарольд",
            "Уолтер", "Альберт", "Говард", "Рэймонд", "Гарри"
        ];
        string[] femaleFirstNames =
        [
            "Мэри", "Патриция", "Дженнифер", "Линда", "Элизабет", "Маргарет", "Дороти",
            "Рут", "Вирджиния", "Хелен", "Элеанор", "Клара", "Эдит", "Глория",
            "Беатрис", "Агнес", "Кэтрин", "Алиса", "Ирен", "Эвелин"
        ];
        string[] lastNames =
        [
            "Смит", "Джонсон", "Уильямс", "Браун", "Джонс", "Миллер", "Дэвис",
            "Уилсон", "Мур", "Тейлор", "Андерсон", "Томпсон", "Уайт", "Харрис",
            "Кларк", "Льюис", "Робинсон", "Холл", "Аллен", "Янг",
            "Кинг", "Райт", "Хилл", "Скотт", "Грин", "Бейкер", "Нельсон", "Картер"
        ];

        var firstNames = isMale ? maleFirstNames : femaleFirstNames;
        return $"{firstNames[_random.Next(firstNames.Length)]} {lastNames[_random.Next(lastNames.Length)]}";
    }

    private (string Birthplace, string Residence) GenerateLocations()
    {
        string[] locations =
        [
            "Аркхем", "Бостон", "Кингспорт", "Данвич", "Иннсмут",
            "Провиденс", "Нью-Йорк", "Салем", "Чикаго", "Сан-Франциско"
        ];

        var birthplace = locations[_random.Next(locations.Length)];
        // 60% шанс что живёт в Аркхеме (основное место действия), иначе случайный город
        var residence = _random.Next(100) < 60 ? "Аркхем" : locations[_random.Next(locations.Length)];
        return (birthplace, residence);
    }

    #endregion

    #region Биография

    private void GenerateBiography(Character character, CharacterGenerationLog log)
    {
        log.Add("Биография", "--- Генерация биографии ---");

        character.Biography.Appearance = PickRandom(Appearances);
        log.Add("Биография", $"Описание: {character.Biography.Appearance}");

        character.Biography.IdealsAndPrinciples = PickRandom(Ideals);
        log.Add("Биография", $"Идеалы: {character.Biography.IdealsAndPrinciples}");

        var who = PickRandom(SignificantPeopleWho);
        var why = PickRandom(SignificantPeopleWhy);
        character.Biography.SignificantPeople = $"{who}. {why}";
        log.Add("Биография", $"Значимый человек: {character.Biography.SignificantPeople}");

        character.Biography.ImportantPlaces = PickRandom(ImportantPlaces);
        log.Add("Биография", $"Важное место: {character.Biography.ImportantPlaces}");

        character.Biography.ValuablePossessions = PickRandom(ValuablePossessions);
        log.Add("Биография", $"Ценное имущество: {character.Biography.ValuablePossessions}");

        character.Biography.Traits = PickRandom(Traits);
        log.Add("Биография", $"Черта: {character.Biography.Traits}");
    }

    private string PickRandom(string[] items) => items[_random.Next(items.Length)];

    private static readonly string[] Appearances =
    [
        "Выносливый", "Привлекательный", "Нескладный", "Симпатичный", "Очаровательный",
        "Миловидный", "Умный", "Неопрятный", "Яркий", "Начитанный",
        "Моложавый", "Измождённый", "Пухлый", "Грузный", "Лохматый",
        "Стройный", "Заросший", "Коренастый", "Бледный", "Хмурый",
        "Заурядный", "Румяный", "Загорелый", "Морщинистый", "Робкий",
        "Мускулистый", "Утончённый", "Рослый", "Простоватый", "Хрупкий", "Со вкусом одетый"
    ];

    private static readonly string[] Ideals =
    [
        "Существует высшая сила, которой вы поклоняетесь",
        "Человечество обошлось бы без религий — убеждённый атеист",
        "Наука знает всё — вера в научный прогресс",
        "Верит в судьбу, карму и приметы",
        "Член тайного или открытого общества",
        "В обществе есть зло, которое нужно искоренить",
        "Увлечён оккультизмом: астрология, спиритизм, Таро",
        "Политические убеждения определяют всю жизнь",
        "Деньги — это власть, и нужно их как можно больше",
        "Активист: борец за права, профсоюзный лидер"
    ];

    private static readonly string[] SignificantPeopleWho =
    [
        "Родитель (мать или отец)",
        "Бабушка или дедушка",
        "Брат или сестра",
        "Сын или дочь",
        "Спутник жизни (супруг/а, возлюбленный/ая)",
        "Тот, кто научил главному профессиональному навыку",
        "Друг детства",
        "Знаменитость — герой или кумир",
        "Другой сыщик из группы",
        "Персонаж Хранителя"
    ];

    private static readonly string[] SignificantPeopleWhy =
    [
        "Он вас чему-то научил",
        "Вы у него в долгу",
        "Человек дал вам смысл в жизни",
        "Вы подвели этого человека и хотите искупить вину",
        "Вас связывает общее прошлое",
        "Вы хотите проявить себя перед ним",
        "Вы преклоняетесь перед ним",
        "Сожаление — о чём-то несказанном или несделанном",
        "Вы хотите доказать, что лучше него",
        "Человек обманул вас, и вы жаждете справедливости"
    ];

    private static readonly string[] ImportantPlaces =
    [
        "Заведение, где вы учились",
        "Ваша малая родина",
        "Место, где вы встретили первую любовь",
        "Тихое место для размышлений",
        "Место общения: клуб, бар, дом друга",
        "Место, связанное с верой или идеологией",
        "Могила важного для вас человека",
        "Семейный дом",
        "Место, где вы были безмятежно счастливы",
        "Ваше рабочее место"
    ];

    private static readonly string[] ValuablePossessions =
    [
        "Предмет, связанный с главным навыком",
        "Предмет, необходимый в работе",
        "Сувенир из детства",
        "Сувенир в память о значимом человеке",
        "Подарок от кого-то важного",
        "Ваша коллекция",
        "Непонятная находка неизвестного происхождения",
        "Предмет, связанный со спортом",
        "Оружие (табельное, наследственное или найденное)",
        "Питомец (кошка, собака, черепаха)"
    ];

    private static readonly string[] Traits =
    [
        "Щедрый — всегда помогает нуждающимся",
        "Любитель животных",
        "Мечтатель и творческая натура",
        "Гедонист — живёт сегодняшним днём",
        "Любитель риска и острых ощущений",
        "Кулинар — готовит с любовью",
        "Дамский угодник / обольстительница",
        "Верный — держит слово",
        "Добрая слава: бесстрашен перед лицом опасности",
        "Амбициозный — стремится к цели любой ценой"
    ];

    #endregion

    #region Финансы

    private void CalculateFinances(Character character, CharacterGenerationLog log)
    {
        log.Add("Финансы", "--- Расчёт финансов (1920-е) ---");

        var creditRatingSkill = character.Skills.SkillGroups
            .SelectMany(g => g.Skills)
            .FirstOrDefault(s => s.Name == "Средства");

        var cr = creditRatingSkill?.Value.Regular ?? 0;

        var (cash, assets, pocket) = cr switch
        {
            0 => ("0.50", "нет", "0.50"),
            >= 1 and <= 9 => ($"{cr * 1}", $"{cr * 10}", "2"),
            >= 10 and <= 49 => ($"{cr * 2}", $"{cr * 50}", "10"),
            >= 50 and <= 89 => ($"{cr * 5}", $"{cr * 500}", "50"),
            >= 90 and <= 98 => ($"{cr * 20}", $"{cr * 2000}", "250"),
            _ => ("50000", "5000000+", "5000")
        };

        character.Finances.Cash = $"${cash}";
        character.Finances.PocketMoney = $"${pocket}";
        character.Finances.Assets = [$"${assets}"];

        log.Add("Финансы", $"Средства: {cr}%",
            details: $"Наличные: ${cash}, Активы: ${assets}, Карманные: ${pocket}");
    }

    #endregion
}