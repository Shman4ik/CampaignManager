using System.Text.RegularExpressions;
using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Tests.Documents;

/// <summary>
/// Коды книжных навыков — явная таблица «русское имя → английский код книги» (решение владельца
/// 2026-10-02), а не транслит. Перенос (T1.3) и сиды берут коды отсюда. Названия сверены с книгой Хранителя
/// и книгой сыщика 7e (2026-10-02): расхождений нет, «Lore (Dream Lore)» — по примерам специализаций книги.
/// </summary>
public sealed partial class SkillCodesTests
{
    /// <summary>
    /// 93 навыка справочника v1 (<c>games."Skills"</c> на 2026-10-02) — все книжные — без трёх только современной
    /// эпохи («Ближний бой (бензопила)», «Работа с компьютером», «Электроника»; решение владельца 2026-10-02).
    /// </summary>
    private static readonly string[] V1CatalogNames =
    [
        "Верховая езда", "Вождение автомобиля", "Лазание", "Пилотирование", "Пилотирование (лодка)",
        "Пилотирование (самолёт)", "Плавание", "Подводное плавание", "Прыжки", "Управление тяжёлыми машинами",
        "Артиллерия", "Стрельба", "Стрельба (винтовка/дробовик)", "Стрельба (лук)", "Стрельба (огнемёт)",
        "Стрельба (пистолет)", "Стрельба (пистолет-пулемёт)", "Стрельба (пулемёт)", "Стрельба (тяжёлое вооружение)",
        "Ближний бой", "Ближний бой (драка)", "Ближний бой (копьё)", "Ближний бой (меч)",
        "Ближний бой (топор)", "Ближний бой (удавка)", "Ближний бой (хлыст)", "Ближний бой (цеп)", "Метание",
        "Уклонение", "Гипноз", "Медицина", "Наука (фармакология)", "Первая помощь", "Психоанализ", "Внимание",
        "Наука (криминалистика)", "Работа в библиотеке", "Слух", "Антропология", "Археология",
        "Бухгалтерское дело", "Естествознание", "История", "Наука", "Наука (астрономия)", "Наука (биология)",
        "Наука (ботаника)", "Наука (геология)", "Наука (зоология)", "Наука (инженерия)", "Наука (криптография)",
        "Наука (математика)", "Наука (метеорология)", "Наука (физика)", "Наука (химия)", "Оккультизм", "Оценка",
        "Юриспруденция", "Взлом", "Взрывчатка", "Выживание", "Выживание (море)", "Выживание (полярные области)",
        "Выживание (пустыня)", "Ловкость рук", "Механика", "Ориентирование", "Скрытность",
        "Чтение следов", "Электрика", "Запугивание", "Красноречие", "Маскировка", "Обаяние",
        "Психология", "Убеждение", "Чтение по губам", "Язык, иностранный", "Язык, иностранный (английский)",
        "Язык, родной", "Искусство/ремесло", "Искусство/ремесло (актёрская игра)",
        "Искусство/ремесло (изобразительное искусство)", "Искусство/ремесло (подделка)",
        "Искусство/ремесло (фотография)", "Мифы Ктулху", "Обращение с животными", "Средства", "Тайные знания",
        "Тайные знания (сновидения)",
    ];

    /// <summary>Специализации Стрельбы для инопланетного оружия главы 13 (стр. 269, 273): в справочнике v1 их не было.</summary>
    private static readonly string[] ArtifactWeaponSkills = ["Стрельба (молниемёт)", "Стрельба (электропушка)"];

    private static IEnumerable<string> BookSkillNames => V1CatalogNames.Concat(ArtifactWeaponSkills);

    [GeneratedRegex(@"^skill\.[a-z]+(-[a-z]+)*(\.[a-z]+(-[a-z]+)*)?$")]
    private static partial Regex CodeFormat();

    [Fact]
    public void EveryBookSkill_HasCode_AndCodesAreUnique()
    {
        Assert.Equal(90, V1CatalogNames.Length);
        var codes = BookSkillNames.Select(name => SkillCodes.FromName(name)
            ?? throw new Xunit.Sdk.XunitException($"Нет кода у книжного навыка «{name}»")).ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(codes.Count, SkillCodes.BookNames.Count);
        Assert.Equal(BookSkillNames.Order(StringComparer.Ordinal), SkillCodes.BookNames.Values.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Codes_AreKebabCase_SpecializationUnderItsParent()
    {
        foreach (var (code, name) in SkillCodes.BookNames)
        {
            Assert.Matches(CodeFormat(), code);
            Assert.Equal(code, SkillCodes.FromName(name));

            if (SkillCodes.ParentOf(code) is { } parent)
            {
                // «Стрельба (пистолет)» — специализация «Стрельба»: и по коду, и по имени
                Assert.True(SkillCodes.BookNames.TryGetValue(parent, out var parentName), $"нет родителя {parent} у {code}");
                Assert.StartsWith(parentName + " (", name, StringComparison.Ordinal);
            }
            else
            {
                Assert.DoesNotContain('(', name);
            }
        }
    }

    [Fact]
    public void RuleCodes_AreInTable()
    {
        string[] ruleCodes =
        [
            SkillCodes.Mythos, SkillCodes.CreditRating, SkillCodes.Dodge, SkillCodes.LanguageOwn,
            SkillCodes.LanguageForeign, SkillCodes.Fighting, SkillCodes.Firearms, SkillCodes.Survival,
            .. SkillCodes.Social,
        ];

        foreach (var code in ruleCodes)
            Assert.Contains(code, SkillCodes.BookNames.Keys);

        Assert.Equal("skill.cthulhu-mythos", SkillCodes.Mythos);
        Assert.Equal("skill.credit-rating", SkillCodes.CreditRating);
        Assert.Equal("skill.dodge", SkillCodes.Dodge);
        Assert.Equal("skill.language-own", SkillCodes.LanguageOwn);
    }

    [Theory]
    [InlineData("Стрельба (пистолет)", "skill.firearms.handgun")]
    [InlineData("Ближний бой (драка)", "skill.fighting.brawl")]
    [InlineData("  МИФЫ   ктулху ", "skill.cthulhu-mythos")] // регистр и пробелы
    [InlineData("Ближний бой (копье)", "skill.fighting.spear")] // «ё» = «е»
    [InlineData("взлом", "skill.locksmith")]
    // Написания стандартного листа v1 (SkillsModel.DefaultSkillsModel) — 46–47 листов в базе
    [InlineData("Языки (родной)", "skill.language-own")]
    [InlineData("Языки (иностр.)", "skill.language-other")]
    [InlineData("Вождение", "skill.drive-auto")]
    [InlineData("Упр. тяж. машинами", "skill.operate-heavy-machinery")]
    [InlineData("Стрельба (винт./дроб.)", "skill.firearms.rifle-shotgun")]
    [InlineData("Автомат", "skill.firearms.submachine-gun")]
    // Написания с листов v1, которых нет ни в справочнике, ни в стандартном листе (перенос T1.3)
    [InlineData("Фотография", "skill.art-craft.photography")]
    [InlineData("Наука фармакология ", "skill.science.pharmacy")]
    [InlineData("Наука судмедэксперт", "skill.science.forensics")]
    public void FromName_BookAndV1Spellings(string name, string expected) =>
        Assert.Equal(expected, SkillCodes.FromName(name));

    [Theory]
    [InlineData("Латынь")]
    [InlineData("Язык, иностранный (латынь)")] // специализации нет в справочнике — своя
    [InlineData("Стрельба (пистолет")]
    [InlineData("Работа с компьютером")] // только современная эпоха — убран
    [InlineData("")]
    public void FromName_Unknown_IsNull(string name) => Assert.Null(SkillCodes.FromName(name));

    [Theory]
    [InlineData("skill.firearms.handgun", "skill.firearms")]
    [InlineData("skill.language-other.english", "skill.language-other")]
    [InlineData("skill.dodge", null)]
    public void ParentOf(string code, string? expected) => Assert.Equal(expected, SkillCodes.ParentOf(code));
}
