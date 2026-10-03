namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Коды книжного оружия (<c>weapons.code</c>): английское название из таблицы XVII «Оружие» книги Хранителя 7e
/// (Keeper Rulebook, стр. 401–405) в kebab-case — <c>weapon.38-or-9mm-revolver</c>, <c>weapon.thompson</c>.
/// Точка калибра и звёздочка книги пропадают. Пять записей (кинжал, штык, рапира, опасная бритва, дубинка) —
/// не из таблицы XVII, а из прейскуранта 1920-х («Melee Weapons»). Правило и поиск — <see cref="CatalogCodeTable"/>.
/// Все 108 записей справочника v1 на 2026-10-02 книжные; порядок — по алфавиту имён v1. 39 записей только
/// современной эпохи (с ними «РПГ*») убраны по решению владельца 2026-10-02 вместе с современной эпохой
/// (список — docs/v2/migration-report.md); их коды больше не выдаются.
/// </summary>
public static class WeaponCodes
{
    public const string Prefix = "weapon.";

    public static CatalogCodeTable Table { get; } = new(Prefix,
    [
        new("weapon.bergmann-mp18-mp28", "«Бергманн» MP18/MP28", ["«Бергманн» MP18/MP28/11"]), // «/11» в имени v1 — сноски книги (MP18¹/MP28¹¹), убраны
        new("weapon.30-browning-m1917a1", "«Браунинг» M1917A1 30-го калибра"),
        new("weapon.browning-auto-rifle-m1918", "«Браунинг» M1918"),
        new("weapon.bren-gun", "«Брэн»"),
        new("weapon.vickers-303-machine-gun", "«Виккерс» 303-го калибра"),
        new("weapon.model-1882-gatling-gun", "«Гатлинг» модели 1882"),
        new("weapon.25-derringer-1b", "«Дерринджер» 25-го калибра (1С)", ["«Дерринджер» 25-го калибра (IC)"]), // в v1 латиница «IC» вместо «1С» (один ствол), как у «Слонобоя» (2С)
        new("weapon.molotov-cocktail", "«Коктейль Молотова»"),
        new("weapon.303-lee-enfield", "«Ли-Энфилд» 303-го калибра"),
        new("weapon.mark-i-lewis-gun", "«Льюис» модели I"),
        new("weapon.model-p08-luger", "«Люгер» Р08"),
        new("weapon.elephant-gun-2b", "«Слонобой» (2С)"),
        new("weapon.22-short-automatic", "Автоматический пистолет 22-го калибра"),
        new("weapon.32-or-7-65mm-automatic", "Автоматический пистолет 32-го калибра (7,65 мм)"),
        new("weapon.38-automatic", "Автоматический пистолет 38-го калибра"),
        new("weapon.45-automatic", "Автоматический пистолет 45-го калибра"),
        new("weapon.crossbow", "Арбалет"),
        new("weapon.war-boomerang", "Бумеранг боевой"),
        new("weapon.22-bolt-action-rifle", "Винтовка 22-го калибра со скользящим затвором"),
        new("weapon.45-martini-henry-rifle", "Винтовка «Мартини-Генри» 45-го калибра"),
        new("weapon.mace-spray", "Газовый баллончик"),
        new("weapon.burning-torch", "Горящий факел"),
        new("weapon.blasting-cap", "Детонатор"),
        new("weapon.dynamite-stick", "Динамитная шашка", ["Динамитная шашка*"]), // звёздочка — сноска книги, в названии ей не место
        new("weapon.10-gauge-shotgun-2b", "Дробовик 10-го калибра (2C)"),
        new("weapon.12-gauge-shotgun-2b-sawed-off", "Дробовик 12-го калибра (2C обрез)"),
        new("weapon.12-gauge-shotgun-2b", "Дробовик 12-го калибра (2C)"),
        new("weapon.16-gauge-shotgun-2b", "Дробовик 16-го калибра (2C)"),
        new("weapon.20-gauge-shotgun-2b", "Дробовик 20-го калибра (2C)"),
        new("weapon.club-large", "Дубина, большая (бейсбольная/крикетная бита, кочерга)"),
        new("weapon.club-small", "Дубина, малая (полицейская дубинка)"),
        new("weapon.billy-club-12-inch", "Дубинка (12-дюймовая)"), // не таблица XVII, а цены 1920-х («Melee Weapons»)
        new("weapon.blackjack", "Дубинка (кистень, блэкджек)"),
        new("weapon.col-morans-air-rifle", "Духовое ружьё полковника Морана"),
        new("weapon.rock-thrown", "Камень (брошенный)"),
        new("weapon.brass-knuckles", "Кастет"),
        new("weapon.dagger", "Кинжал"), // не таблица XVII, а цены 1920-х («Melee Weapons»)
        new("weapon.bullwhip", "Кнут"),
        new("weapon.spear-cavalry-lance", "Копье (кавалерийская пика)"),
        new("weapon.spear-thrown", "Копье, метательное"),
        new("weapon.flintlock", "Кремнёвый пистолет"),
        new("weapon.bow-and-arrows", "Лук и стрелы"),
        new("weapon.sword-light", "Меч лёгкий (шпага, меч-трость)"),
        new("weapon.sword-heavy", "Меч тяжёлый (сабля)"),
        new("weapon.58-springfield-rifle-musket", "Мушкет «Спрингфилд» 58-го калибра"),
        new("weapon.knife-large", "Нож, большой (мачете и т.п.)"),
        new("weapon.knife-small", "Нож, малый (выкидной нож и т.п.)"),
        new("weapon.knife-medium", "Нож, средний (разделочный нож и т.п.)"),
        new("weapon.nunchaku", "Нунчаку"),
        new("weapon.flamethrower", "Огнемёт"),
        new("weapon.straight-razor", "Опасная бритва"), // не таблица XVII, а цены 1920-х («Melee Weapons»)
        new("weapon.thompson", "ПП Томпсона"),
        new("weapon.75mm-field-gun", "Полевое орудие калибра 75 мм"),
        new("weapon.anti-personnel-mine", "Противопехотная мина"),
        new("weapon.rapier", "Рапира"), // цены 1920-х («Melee Weapons»); в таблице XVII — «Sword, medium (rapier, heavy epee)»
        new("weapon.32-or-7-65mm-revolver", "Револьвер 32-го калибра (7,65 мм)"),
        new("weapon.38-or-9mm-revolver", "Револьвер 38-го калибра (9 мм)"),
        new("weapon.41-revolver", "Револьвер 41-го калибра"),
        new("weapon.45-revolver", "Револьвер 45-го калибра"),
        new("weapon.hand-grenade", "Ручная граната", ["Ручная граната*"]), // то же
        new("weapon.30-06-bolt-action-rifle", "Винтовка калибра .30-06 со скользящим затвором", ["Рычажная винтовка калибра .30-06"]), // ошибка перевода v1 исправлена: в книге .30-06 — со скользящим затвором, не рычажная
        new("weapon.30-lever-action-carbine", "Рычажный карабин 30-го калибра"),
        new("weapon.signal-handgun", "Сигнальный пистолет (ракетница)"),
        new("weapon.shuriken", "Сюрикен"),
        new("weapon.wood-axe", "Топор валочный"),
        new("weapon.hatchet-sickle", "Топорик/Серп"),
        new("weapon.pipe-bomb", "Трубчатая бомба"),
        new("weapon.garrote", "Удавка"),
        new("weapon.bayonet", "Штык"), // не таблица XVII, а цены 1920-х («Melee Weapons»)
    ]);

    /// <summary>Код книжной записи по имени; null — самодельная.</summary>
    public static string? FromName(string name) => Table.FromName(name);
}
