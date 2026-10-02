namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Коды книжных тварей бестиария (<c>creatures.code</c>): английское название в книге Хранителя 7e
/// (разделы чудовищ и божеств Мифов, традиционных чудовищ и зверей) — <c>creature.deep-one</c>. Апострофы
/// английских имён пропадают (<c>Gla'aki</c> → <c>glaaki</c>). Правило и поиск — <see cref="CatalogCodeTable"/>.
/// </summary>
public static class CreatureCodes
{
    public const string Prefix = "creature.";

    /// <summary>86 из 87 тварей справочника v1 на 2026-10-02. Порядок — по алфавиту русских имён.</summary>
    public static CatalogCodeTable Table { get; } = new(Prefix,
    [
        new("creature.abhoth", "Абхот"),
        new("creature.azathoth", "Азатот"),
        new("creature.eihort", "Айхорт"),
        new("creature.shark", "Акула"),
        new("creature.atlach-nacha", "Атлач-Нача"),
        new("creature.bast", "Баст"),
        new("creature.formless-spawn", "Бесформенное отродье"),
        new("creature.dimensional-shambler", "Бродящий меж миров"),
        new("creature.byakhee", "Бьякхи"),
        new("creature.vampire", "Вампир"),
        new("creature.great-race-of-yith", "Великая Раса с планеты Йит"),
        new("creature.great-cthulhu", "Великий Ктулху"),
        new("creature.wolf", "Волк"),
        new("creature.ghast", "Гаст"),
        new("creature.ghatanothoa", "Гатанотоа"),
        new("creature.glaaki", "Гла'аки"),
        new("creature.deep-one", "Глубоководный"),
        new("creature.gnoph-keh", "Гноф-кех"),
        new("creature.hound-of-tindalos", "Гончая Тиндала"),
        new("creature.mi-go", "Гриб с Юггота"), // в книге «Mi-Go, the Fungi from Yuggoth»
        new("creature.dagon-and-hydra", "Дагон и Гидра"),
        new("creature.dhole", "Дхоул"),
        new("creature.star-spawn-of-cthulhu", "Звёздное отродье Ктулху"),
        new("creature.star-vampire", "Звёздный вампир"),
        new("creature.serpent-people", "Змеиный народ"),
        new("creature.snake-constrictor", "Змея, Удав"), // спорно: в книге один раздел «Snake», здесь — его блок удава
        new("creature.snake-venomous", "Змея, Ядовитая"), // спорно: блок ядовитой змеи раздела «Snake»
        new("creature.zombie", "Зомби"),
        new("creature.zoth-ommog", "Зот-Оммог"),
        new("creature.ygolonac", "И'голонак"),
        new("creature.ithaqua", "Итакуа"),
        new("creature.yibb-tstll", "Йибб-Тистл"),
        new("creature.yig", "Йиг"),
        new("creature.yog-sothoth", "Йог-Сотот"),
        new("creature.squid-giant", "Кальмар, гигантский"),
        new("creature.king-in-yellow", "Король в жёлтом, воплощение Хастура"),
        new("creature.crocodile", "Крокодил"),
        new("creature.rat", "Крыса"), // в книге раздел «Rat», статблок — на стаю
        new("creature.rat-thing", "Крысиная тварь"),
        new("creature.cthugha", "Ктугха"),
        new("creature.lion", "Лев"),
        new("creature.bat-large", "Летучая мышь, крупная"),
        new("creature.flying-polyp", "Летучий полип"),
        new("creature.lloigor", "Ллойгор"),
        new("creature.horse", "Лошадь"),
        new("creature.bear", "Медведь"),
        new("creature.mummy", "Мумия"),
        new("creature.insect-from-shaggai", "Насекомое с Шаггая"),
        new("creature.nyogtha", "Ниогта"),
        new("creature.nodens", "Ноденс"),
        new("creature.nightgaunt", "Ночной призрак"),
        new("creature.nyarlathotep-human-form", "Ньярлатхотеп (человеческая форма)"), // спорно: в книге один раздел, форма — блок внутри
        new("creature.nyarlathotep-monstrous-form", "Ньярлатхотеп (чудовищная форма)"),
        new("creature.sand-dweller", "Обитатель песков"),
        new("creature.werewolf", "Оборотень"),
        new("creature.fire-vampire", "Огненный вампир"),
        new("creature.wasp-and-bee-swarm", "Осиный или пчелиный рой"),
        new("creature.spawn-of-ubbo-sathla", "Отродье Уббо-Сатлы"), // в книге — блок внутри раздела Ubbo-Sathla
        new("creature.hunting-horror", "Охотящийся ужас"),
        new("creature.crawling-one", "Ползучая тварь"),
        new("creature.deep-one-hybrid", "Потомок Глубоководных"),
        new("creature.ghost", "Привидение"),
        new("creature.bird", "Птица"),
        new("creature.rhan-tegoth", "Ран-Тигот"),
        new("creature.cyaegha", "Саега"),
        new("creature.skeleton-human", "Скелет человека"),
        new("creature.servitor-of-the-outer-gods", "Служитель Внешних богов"),
        new("creature.servant-of-glaaki", "Служитель Гла'аки"),
        new("creature.dog", "Собака"),
        new("creature.elder-thing", "Старец"),
        new("creature.tulzscha", "Тульща"),
        new("creature.dark-young", "Тёмная молодь"),
        new("creature.ubbo-sathla", "Уббо-Сатла"),
        new("creature.ghoul", "Упырь"),
        new("creature.hastur-the-unspeakable", "Хастур Неназываемый"),
        new("creature.chthonian", "Хтониец"),
        new("creature.tsathoggua", "Цатхоггуа"),
        new("creature.colour-out-of-space", "Цвет из иных миров"),
        new("creature.tcho-tcho", "Чо-чо"),
        new("creature.chaugnar-faugn", "Шагнер Фан"),
        new("creature.shantak", "Шантак"),
        new("creature.shoggoth", "Шоггот"),
        new("creature.shoggoth-lord-human-form", "Шоггот-владыка (форма человека)"), // спорно: в книге один раздел, форма — блок внутри
        new("creature.shoggoth-lord-shoggoth-form", "Шоггот-владыка (форма шоггота)"),
        new("creature.shub-niggurath", "Шуб-Ниггурат"),
        new("creature.shudde-mell", "Шудде-Мьелл"),

        // Самодельное, без кода: «Гончая Шаб-Ниггурат» — в книге 7e такой твари нет.
    ]);

    /// <summary>Код книжной твари по имени; null — самодельная.</summary>
    public static string? FromName(string name) => Table.FromName(name);
}
