using System.Text.RegularExpressions;
using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Tests.Documents;

/// <summary>
/// Коды книжных записей остальных справочников (решение владельца 2026-10-02): явные таблицы «имя
/// справочника v1 → английское название книги в kebab-case». Числа строк — справочники v1 (ветка Neon
/// <c>dev</c>, 2026-10-02) без самодельного: его кода нет, и в таблицу оно не входит.
/// </summary>
public sealed partial class CatalogCodesTests
{
    public static TheoryData<string, int> Tables => new()
    {
        { OccupationCodes.Prefix, 31 },
        { WeaponCodes.Prefix, 108 },
        { SpellCodes.Prefix, 92 },
        { BookCodes.Prefix, 106 },
        { ItemCodes.Prefix, 310 }, // 317 минус 4 реквизита сценариев и 3 повтора под другим именем
        { CreatureCodes.Prefix, 86 }, // 87 минус «Гончая Шаб-Ниггурат»
    };

    [GeneratedRegex(@"^[a-z]+\.[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex CodeFormat();

    private static CatalogCodeTable TableOf(string prefix) => prefix switch
    {
        OccupationCodes.Prefix => OccupationCodes.Table,
        WeaponCodes.Prefix => WeaponCodes.Table,
        SpellCodes.Prefix => SpellCodes.Table,
        BookCodes.Prefix => BookCodes.Table,
        ItemCodes.Prefix => ItemCodes.Table,
        CreatureCodes.Prefix => CreatureCodes.Table,
        _ => throw new ArgumentOutOfRangeException(nameof(prefix), prefix, null),
    };

    [Theory]
    [MemberData(nameof(Tables))]
    public void EveryRow_HasKebabCaseCode_WithPrefix_UniqueInCatalog(string prefix, int count)
    {
        var table = TableOf(prefix);

        Assert.Equal(prefix, table.Prefix);
        Assert.Equal(count, table.BookNames.Count);
        foreach (var (code, name) in table.BookNames)
        {
            Assert.StartsWith(prefix, code, StringComparison.Ordinal);
            Assert.Matches(CodeFormat(), code);
            Assert.Equal(code, table.FromName(name));
        }

        // Коды — ключи словаря, повтор кода таблица не примет; имена уникальны и после нормализации,
        // как индекс lower(name) в cm
        Assert.Equal(count, table.BookNames.Values.Select(CatalogCodeTable.NormalizeName).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("occupation.", "Частный сыщик", "occupation.private-investigator")]
    [InlineData("occupation.", "  ЛЕТЧИК ", "occupation.pilot")] // регистр, пробелы, «ё»
    [InlineData("weapon.", "ПП Томпсона", "weapon.thompson")]
    [InlineData("weapon.", "Револьвер 38-го калибра  (9 мм)", "weapon.38-or-9mm-revolver")]
    [InlineData("weapon.", "«Миниган»*", "weapon.minigun")]
    [InlineData("spell.", "Знак Старших богов", "spell.elder-sign")]
    [InlineData("spell.", "призыв/укрощение бьякхи", "spell.summon-bind-byakhee")]
    [InlineData("spell.", "Хватка Ниогты", "spell.clutch-of-nyogtha")]
    [InlineData("book.", "Некрономикон (латинский перевод Вормия)", "book.necronomicon-latin")]
    [InlineData("book.", "Черная книга Альсофокуса", "book.black-tome")]
    [InlineData("item.", "Верёвка (15 метров)", "item.rope-50-feet")]
    [InlineData("item.", "«Форд» Model T", "item.ford-model-t")]
    [InlineData("creature.", "Глубоководный", "creature.deep-one")]
    [InlineData("creature.", "Гла'аки", "creature.glaaki")]
    [InlineData("creature.", "Шагнер Фан", "creature.chaugnar-faugn")]
    public void FromName_FindsBookEntry(string prefix, string name, string expected) =>
        Assert.Equal(expected, TableOf(prefix).FromName(name));

    /// <summary>Самодельное и повторы v1 кода не получают — перенос (T1.3) кладёт их с <c>code</c> null и в отчёт.</summary>
    [Theory]
    [InlineData("item.", "Шип Гла'аки")]
    [InlineData("item.", "Дневник Джозефа Тёрнера")]
    [InlineData("item.", "Проживание в гостинице: неплохая гостиница")]
    [InlineData("creature.", "Гончая Шаб-Ниггурат")]
    [InlineData("weapon.", "")]
    [InlineData("spell.", "Самодельное заклинание")]
    [InlineData("occupation.", "Охотник на вампиров")]
    [InlineData("book.", "Некрономикон")] // издание не указано — не угадываем
    public void FromName_HomebrewOrUnknown_IsNull(string prefix, string name) =>
        Assert.Null(TableOf(prefix).FromName(name));

    [Fact]
    public void Table_RejectsDuplicates_AndForeignPrefix()
    {
        Assert.Throws<InvalidOperationException>(() => new CatalogCodeTable("x.", [new("x.a", "А"), new("x.a", "Б")]));
        Assert.Throws<InvalidOperationException>(() => new CatalogCodeTable("x.", [new("x.a", "А"), new("x.b", "а")]));
        Assert.Throws<InvalidOperationException>(() => new CatalogCodeTable("x.", [new("y.a", "А")]));
    }
}
