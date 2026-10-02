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
        // Современная эпоха убрана владельцем 2026-10-02: её записей в таблицах нет
        { OccupationCodes.Prefix, 30 }, // 31 минус «Хакер»
        { WeaponCodes.Prefix, 69 }, // 108 минус 39 только современных (с ними «РПГ*»)
        { SpellCodes.Prefix, 92 },
        { BookCodes.Prefix, 106 },
        { ItemCodes.Prefix, 257 }, // 317 минус 4 реквизита сценария, 3 повтора и 53 несверенных современных
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
    [InlineData("weapon.", "Ручная граната*", "weapon.hand-grenade")]
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

    /// <summary>Ошибки перевода v1 исправлены в имени; код прежний, старое написание находит ту же запись.</summary>
    [Theory]
    [InlineData("item.", "Фляга", "Флаг (1 метр)", "item.canteen")]
    [InlineData("item.", "Железные колышки для палатки (12 шт.)", "Железная кровать для палатки (12 шт.)", "item.iron-tent-stakes")]
    [InlineData("item.", "Когти монтёрские", "Котли монтёрские", "item.linemans-climbers")]
    [InlineData("item.", "Перчатки электрика", "Печатки электрика", "item.electricians-gloves")]
    [InlineData("item.", "Запонки", "Заколки", "item.cuff-links")]
    [InlineData("item.", "Шприц из твёрдой резины", "Стрихнинка", "item.hard-rubber-syringe")]
    [InlineData("item.", "Глобус на подставке", "Трюбка на подставке", "item.globe-on-stand")]
    [InlineData("weapon.", "Винтовка калибра .30-06 со скользящим затвором", "Рычажная винтовка калибра .30-06", "weapon.30-06-bolt-action-rifle")]
    public void Renamed_entry_keeps_code_and_old_spelling(string prefix, string name, string oldName, string code)
    {
        var table = TableOf(prefix);
        Assert.Equal(name, table.BookNames[code]);
        Assert.Equal(code, table.FromName(oldName));
    }

    /// <summary>Записи только современной эпохи убраны (решение владельца 2026-10-02) — их имена кода не дают.</summary>
    [Theory]
    [InlineData("weapon.", "РПГ*")]
    [InlineData("weapon.", "АК-47 или АКМ")]
    [InlineData("weapon.", "Бензопила")]
    [InlineData("item.", "Смартфон")]
    [InlineData("item.", "Мобильный телефон")]
    [InlineData("occupation.", "Хакер")]
    public void FromName_ModernOnlyEntry_IsNull(string prefix, string name) =>
        Assert.Null(TableOf(prefix).FromName(name));

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
