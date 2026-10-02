namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Коды книжных заклинаний (<c>spells.code</c>): английское название из главы «Гримуар» книги Хранителя 7e
/// в kebab-case — <c>spell.elder-sign</c>. Варианты общих заклинаний — как их пишет книга: «Call Azathoth»
/// (<c>spell.call-azathoth</c>), «Contact Ghoul», «Summon/Bind Byakhee» (<c>spell.summon-bind-byakhee</c>).
/// Все 91 заклинание справочника v1 на 2026-10-02 книжные; одно (Глаз тьмы) — из «Масок Ньярлатхотепа».
/// Порядок — по алфавиту имён v1. Правило и поиск — <see cref="CatalogCodeTable"/>.
/// </summary>
public static class SpellCodes
{
    public const string Prefix = "spell.";

    public static CatalogCodeTable Table { get; } = new(Prefix,
    [
        new("spell.bless-blade", "Благословение клинка"),
        new("spell.view-gate", "Взгляд сквозь врата"), // спорно: сопоставлено по смыслу
        new("spell.mindblast", "Взрыв мозга"),
        new("spell.wave-of-oblivion", "Волна истребления"),
        new("spell.resurrection", "Воскрешение"),
        new("spell.time-gate", "Врата времени"),
        new("spell.voorish-sign", "Вурский знак"),
        new("spell.call-azathoth", "Вызов Азатота"),
        new("spell.call-ithaqua", "Вызов Итакуа"),
        new("spell.call-yog-sothoth", "Вызов Йог-Сотота"),
        new("spell.call-cthugha", "Вызов Ктугхи"),
        new("spell.call-nyogtha", "Вызов Ниогты"),
        new("spell.call-hastur", "Вызов Хастура"),
        new("spell.call-shub-niggurath", "Вызов Шуб-Ниггурат"),
        new("spell.eye-of-light-and-darkness", "Глаз тьмы"), // спорно: не книга Хранителя, а «Маски Ньярлатхотепа»
        new("spell.breath-of-the-deep", "Дыхание глубин"),
        new("spell.death-spell", "Заклятие смерти"),
        new("spell.implant-fear", "Зародить страх"),
        new("spell.cloud-memory", "Затуманить память"),
        new("spell.enchant-book", "Зачаровать книгу"),
        new("spell.enchant-knife", "Зачаровать нож"),
        new("spell.enchant-sacrificial-dagger", "Зачаровать ритуальный кинжал"), // спорно: в справочнике книги «Enchant Dagger»
        new("spell.enchant-whistle", "Зачаровать свисток"),
        new("spell.enchant-pipes", "Зачаровать флейту"), // в английской книге «Pipes», флейты нет
        new("spell.flesh-ward", "Защита плоти"),
        new("spell.green-decay", "Зелёное гниение"),
        new("spell.mirror-of-tarkhun-atep", "Зеркало Таркун-Атепа"),
        new("spell.dread-curse-of-azathoth", "Зловещее проклятье Азатота"),
        new("spell.elder-sign", "Знак Старших богов"),
        new("spell.banishment-of-yde-etad", "Изгнание Йде Итад"),
        new("spell.shrivelling", "Иссушение"),
        new("spell.prinns-crux-ansata", "Коптский крест Принна"),
        new("spell.red-sign-of-shudde-mell", "Красный знак Шудде-Мьелла"),
        new("spell.fist-of-yog-sothoth", "Кулак Йог-Сотота"),
        new("spell.body-warping-of-gorgoroth", "Метаморфозы Горгорота"),
        new("spell.mental-suggestion", "Мысленное внушение"),
        new("spell.cause-cure-blindness", "Наслать/исцелить слепоту"),
        new("spell.warding", "Оберег"), // спорно: единственный несопоставленный оберег книги; другой кандидат — Create Charm («Маски»)
        new("spell.mind-exchange", "Обмен разумом"),
        new("spell.enthrall-victim", "Околдовать жертву"),
        new("spell.mind-transfer", "Перенос разума"),
        new("spell.chant-of-thoth", "Песнь Тота"),
        new("spell.song-of-hastur", "Песнь Хастура"),
        new("spell.melt-flesh", "Плавление плоти"),
        new("spell.consume-likeness", "Поглощение образа"),
        new("spell.find-gate", "Поиск врат"),
        new("spell.dominate", "Покорение"),
        new("spell.powder-of-ibn-ghazi", "Порошок Ибн-Гази"),
        new("spell.dust-of-suleiman", "Прах Сулеймана"),
        new("spell.summon-bind-dimensional-shambler", "Призыв/укрощение бродящего меж миров"),
        new("spell.summon-bind-byakhee", "Призыв/укрощение бьякхи"),
        new("spell.summon-bind-star-vampire", "Призыв/укрощение звёздного вампира"),
        new("spell.summon-bind-nightgaunt", "Призыв/укрощение ночного призрака"),
        new("spell.summon-bind-fire-vampire", "Призыв/укрощение огненного вампира"),
        new("spell.summon-bind-hunting-horror", "Призыв/укрощение охотящегося ужаса"),
        new("spell.summon-bind-servitor-of-the-outer-gods", "Призыв/укрощение служителя Внешних богов"),
        new("spell.summon-bind-dark-young", "Призыв/укрощение тёмной молоди"),
        new("spell.curse-of-the-putrid-husk", "Проклятие гниющей оболочки"),
        new("spell.brew-space-mead", "Пространственный эликсир"),
        new("spell.wrack", "Разрушение"), // спорно: сопоставлено по смыслу
        new("spell.apportion-ka", "Распределение ка"),
        new("spell.contact-deep-one", "Связь с Глубоководными"),
        new("spell.contact-formless-spawn", "Связь с бесформенным отродьем"),
        new("spell.contact-eihort", "Связь с божеством: Айхорт"),
        new("spell.contact-ygolonac", "Связь с божеством: И'голонак"),
        new("spell.contact-cthulhu", "Связь с божеством: Ктулху"),
        new("spell.contact-nodens", "Связь с божеством: Ноденс"),
        new("spell.contact-nyarlathotep", "Связь с божеством: Ньярлатхотеп"),
        new("spell.contact-tsathoggua", "Связь с божеством: Цатхоггуа"),
        new("spell.contact-chaugnar-faugn", "Связь с божеством: Шагнер Фан"),
        new("spell.contact-gnoph-keh", "Связь с гноф-кехом"),
        new("spell.contact-hound-of-tindalos", "Связь с гончей Тиндала"),
        new("spell.contact-spirits-of-the-dead", "Связь с духами мёртвых"),
        new("spell.contact-yithian", "Связь с йитианином"),
        new("spell.contact-rat-thing", "Связь с крысиной тварью"),
        new("spell.contact-flying-polyp", "Связь с летучим полипом"),
        new("spell.contact-mi-go", "Связь с ми-го"),
        new("spell.contact-sand-dweller", "Связь с обитателем песков"),
        new("spell.contact-ghoul", "Связь с упырём"),
        new("spell.contact-chthonian", "Связь с хтонийцем"),
        new("spell.contact-elder-thing", "Связь со Старцем"),
        new("spell.contact-star-spawn-of-cthulhu", "Связь со звёздным отродьем Ктулху"),
        new("spell.contact-servitor-of-the-outer-gods", "Связь со служителем Внешних богов"),
        new("spell.evil-eye", "Сглаз"),
        new("spell.words-of-power", "Слова власти"),
        new("spell.create-gate", "Создание врат"),
        new("spell.create-zombie", "Создание зомби"),
        new("spell.create-barrier-of-naach-tith", "Сотворить барьер Наах-Тита"),
        new("spell.create-mist-of-rlyeh", "Сотворить туман Р'льеха"),
        new("spell.wither-limb", "Усыхание конечности"),
        new("spell.clutch-of-nyogtha", "Хватка Ниогты"),
        new("spell.gate-boxes", "Шкатулки врат"),
    ]);

    /// <summary>Код книжной записи по имени; null — самодельная.</summary>
    public static string? FromName(string name) => Table.FromName(name);
}
