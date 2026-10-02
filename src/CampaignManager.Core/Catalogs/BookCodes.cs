namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Коды книжных томов (<c>books.code</c>): английское название из главы «Тома Мифов» книги Хранителя 7e
/// (таблица томов Мифов и оккультные книги) в kebab-case — <c>book.pnakotic-manuscripts</c>. Разные издания
/// одного тома различаются хвостом кода (<c>book.necronomicon-latin</c>, <c>book.nameless-cults-1845</c>).
/// Написание — как в книге, даже где оно расходится с привычным («Unausprechlichen», «Cthonic»).
/// Все 106 томов справочника v1 на 2026-10-02 книжные; порядок — по алфавиту имён v1.
/// Правило и поиск — <see cref="CatalogCodeTable"/>.
/// </summary>
public static class BookCodes
{
    public const string Prefix = "book.";

    public static CatalogCodeTable Table { get; } = new(Prefix,
    [
        new("book.beatus-methodivo", "Beatus Methodivo"),
        new("book.chronike-von-nath", "Chronike von Nath"),
        new("book.cthaat-aquadingen-english", "Cthaat Aquadingen (английский)"),
        new("book.cthaat-aquadingen-latin", "Cthaat Aquadingen (латынь)"),
        new("book.cultes-des-goules", "Cultes des Goules"),
        new("book.daemonolatreia", "Daemonolatreia"),
        new("book.de-vermiis-mysteriis", "De Vermiis Mysteriis"),
        new("book.fischbuch", "Fischbuch"),
        new("book.hydrophinnae", "Hydrophinnae"),
        new("book.liber-damnatus-damnationum", "Liber Damnatus Damnationum"),
        new("book.liber-ivonis", "Liber Ivonis"),
        new("book.livre-divon", "Livre d'Ivon"),
        new("book.malleus-maleficarum", "Malleus Maleficarum"),
        new("book.necrolatry", "Necrolatry"),
        new("book.othuum-omnicia", "Othuum Omnicia"),
        new("book.saducismus-triumphatus", "Saducismus Triumphatus"),
        new("book.sapientia-maglorum", "Sapientia Maglorum"),
        new("book.unausprechlichen-kulten", "Unaussprechlichen Kulten"), // так в книге 7e (одно «s»)
        new("book.unter-zee-kulten", "Unter Zee Kulten"),
        new("book.uralteschrecken", "Uralteschrecken"), // в таблице томов «Uralteschrecken», в выходных данных «Uralte Schrecken»
        new("book.von-denen-verdammten-reprint", "Von denen Verdammten (переиздание)"), // немецкое переиздание 1907 г.
        new("book.von-denen-verdammten-oder", "Von denen Verdammten Oder"),
        new("book.azathoth-and-others", "Азатот и другие"),
        new("book.al-azif", "Аль-Азиф"),
        new("book.visions-from-yaddith", "Видения из Яддита"),
        new("book.invocations-to-dagon", "Воззвания к Дагону"),
        new("book.ghorl-nigral", "Гхорл Нигралл"),
        new("book.prehistoric-pacific-in-the-light-of-the-ponape-scripture", "Доисторическая Океания в свете «Понапского писания»"),
        new("book.prehistory-in-the-pacific-a-preliminary-investigation", "Доисторическая Океания: начальные исследования"),
        new("book.soul-of-chaos", "Душа хаоса"),
        new("book.life-of-eibon", "Жизнь Эйбона"),
        new("book.testament-of-carnamagos", "Завет Карнамагоса"),
        new("book.green-book", "Зелёная книга"),
        new("book.zohar", "Зоар"),
        new("book.golden-bough", "Золотая ветвь"),
        new("book.i-ching", "И Цзин"),
        new("book.emerald-tablet", "Изумрудная скрижаль"),
        new("book.ilarnek-papyri", "Иларнекские папирусы"),
        new("book.confessions-of-the-mad-monk-clithanus", "Исповедь безумного монаха Клифана"),
        new("book.true-magick", "Истинная магия"),
        new("book.cabala-of-saboth", "Каббала Саваофа"),
        new("book.key-of-solomon", "Ключ Соломона"),
        new("book.naacal-key", "Ключ наакаля"),
        new("book.book-of-dzyan", "Книга Дзиан"),
        new("book.book-of-iod", "Книга Иода"),
        new("book.book-of-iod-english", "Книга Иода (перевод на английский)"), // перевод Иоганна Негуса
        new("book.book-of-skelos", "Книга Скелоса"),
        new("book.book-of-eibon", "Книга Эйбона"),
        new("book.king-in-yellow", "Король в жёлтом"),
        new("book.cthulhu-in-the-necronomicon", "Ктулху в «Некрономиконе»"),
        new("book.witch-cult-in-western-europe", "Культ ведьм в Западной Европе"),
        new("book.legends-of-liqualia", "Легенды Ликвалии"),
        new("book.people-of-the-monolith", "Люди монолита"),
        new("book.magic-and-the-black-arts", "Магия и чернокнижие"), // спорно: сопоставлено по смыслу и автору (Кейн)
        new("book.we-pass-from-view", "Мы уходим из виду"),
        new("book.watchers-on-the-other-side", "Наблюдатели с иной стороны"),
        new("book.nameless-cults-golden-goblin", "Невыразимые культы (издание Golden Goblin Press)"), // Nameless Cults (English, 1909, Golden Goblin Press)
        new("book.nameless-cults-1845", "Невыразимые культы (перевод 1845 г.)"), // Nameless Cults (English, 1845, Bridewell)
        new("book.necronomicon-greek", "Некрономикон (греческий перевод)"), // Necronomicon (Greek, Theodoras Philetas)
        new("book.necronomicon-latin", "Некрономикон (латинский перевод Вормия)"), // Necronomicon (Latin, Olaus Wormius)
        new("book.necronomicon-english-dee", "Некрономикон (перевод Джона Ди)"), // Necronomicon (English, Dr. John Dee)
        new("book.night-gaunt", "Ночные призраки"), // в книге в единственном числе
        new("book.of-evil-sorceries-done-in-new-england", "О злодеяниях чародеев в Новой Англии"),
        new("book.monstres-and-their-kynde", "О природе чудовищ"), // спорно: единственный подходящий том книги, перевод вольный
        new("book.dwellers-in-the-depths", "Обитатели глубин"),
        new("book.occult-foundation", "Основы оккультизма"),
        new("book.remnants-of-lost-empires", "Останки древних империй"),
        new("book.revelations-of-glaaki", "Откровения Гла'аки"),
        new("book.revelations-of-hali", "Откровения Хали"),
        new("book.reflections", "Отражения"),
        new("book.mum-rath-papyri", "Папирусы Мум-Рата"),
        new("book.parchments-of-pnom", "Пергаменты Пнома"),
        new("book.geph-transcriptions", "Переводы Гефа"),
        new("book.dhol-chants", "Песнопения Дхол"),
        new("book.yuggya-chants", "Песнопения Юггья"),
        new("book.song-of-yste", "Песнь Исте"),
        new("book.pnakotic-manuscripts", "Пнакотические манускрипты"),
        new("book.in-pressured-places", "Под высоким давлением"),
        new("book.polynesian-mythology", "Полинезийская мифология (с примечаниями к циклу легенд Ктулху)"), // Polynesian Mythology, with a Note on the Cthulhu Legend-Cycle
        new("book.ponape-scripture", "Понапское писание"),
        new("book.oracles-of-nostradamus", "Предсказания Мишеля Нострадамуса"),
        new("book.isis-unveiled", "Разоблачённая Изида"),
        new("book.johansen-narrative", "Рассказ Йохансена"),
        new("book.massa-di-requiem-per-shuggay", "Реквием по Шаггаю"),
        new("book.yhe-ritual", "Ритуал Ихе"), // в таблице томов «Yhe Ritual», в выходных данных «Yhe Rituals»
        new("book.tunneler-below", "Роющие в глубине"),
        new("book.saracenic-rituals", "Сарацинские ритуалы"),
        new("book.sussex-manuscript", "Сассекская рукопись"),
        new("book.seven-cryptical-books-of-hsan", "Семь загадочных книг Хсана"),
        new("book.zanthu-tablets", "Скрижали Занту"),
        new("book.secret-mysteries-of-asia", "Тайны и загадки Азии (с примечаниями)"), // Secret Mysteries of Asia, with a Commentary on the Ghorl Nigral
        new("book.secret-watcher", "Тайный наблюдатель"),
        new("book.rlyeh-text", "Текст Р'льеха"),
        new("book.tuscan-rituals", "Тосканские ритуалы"),
        new("book.gharne-fragments", "Фрагменты Г'харна"),
        new("book.celaeno-fragments", "Фрагменты с Келено"),
        new("book.cthonic-revelations", "Хтонические откровения"), // так в книге: «Cthonic», не «Chthonic»
        new("book.fourth-book-of-dharsis", "Четвёртая книга Дхарсиса"),
        new("book.marvels-of-science", "Чудеса науки"),
        new("book.thaumaturgical-prodigies-in-the-new-england-canaan", "Чудеса тавматургии в новоанглийском Ханаане"),
        new("book.black-tome", "Чёрная книга Альсофокуса"), // в книге 7e — «Black Tome» (автор Alsophocus)
        new("book.black-book-of-the-skull", "Чёрная книга черепа"),
        new("book.black-sutra", "Чёрная сутра"),
        new("book.black-rites", "Чёрные обряды"),
        new("book.black-god-of-madness", "Чёрный бог безумия"),
        new("book.eltdown-shards", "Эльтдаунские таблички"),
    ]);

    /// <summary>Код книжной записи по имени; null — самодельная.</summary>
    public static string? FromName(string name) => Table.FromName(name);
}
