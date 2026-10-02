namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Коды книжных предметов (<c>items.code</c>): английское название из прейскуранта книги сыщика 7e
/// (Investigator Handbook; список 1920-х — как на cthulhuwiki.chaosium.com) в kebab-case —
/// <c>item.ford-model-t</c>. Единицы — как в книге (футы, галлоны), даже если имя v1 переведено в метры.
/// Строки «не сверено» — современный прейскурант, для которого открытого источника нет: название дано
/// ближайшее английское. Порядок — по разделам v1, внутри — по алфавиту.
/// Правило и поиск — <see cref="CatalogCodeTable"/>.
/// </summary>
public static class ItemCodes
{
    public const string Prefix = "item.";

    public static CatalogCodeTable Table { get; } = new(Prefix,
    [
        // Боевое снаряжение (современный прейскурант книги сыщика; открытого источника для сверки нет)
        new("item.compound-crossbow", "Арбалет композитный"), // не сверено
        new("item.silencer-handgun", "Глушитель запрещённый (пистолетный)"), // не сверено
        new("item.stun-baton", "Дубинка-электрошокер"), // не сверено
        new("item.aluminum-knuckles", "Кастет алюминиевый"), // не сверено; в прейскуранте 1920-х — Brass Knuckles
        new("item.nunchaku", "Нунчаки"),
        new("item.bandolier", "Патронташ"), // не сверено
        new("item.pepper-spray", "Перцовый баллончик"), // не сверено
        new("item.laser-sight", "Прицел лазерный"), // не сверено
        new("item.telescopic-sight", "Прицел оптический"), // не сверено
        new("item.blowgun", "Трубка духовая (с дротиками)"), // не сверено
        new("item.stun-gun", "Электрошокер ручной"), // не сверено
        // Боеприпасы
        new("item.crossbow-bolts", "Арбалетные болты"), // не сверено
        new("item.22-long-rifle", "Патроны калибра .22 LR"),
        new("item.220-swift", "Патроны калибра .220 Swift"), // не сверено
        new("item.25-acp", "Патроны калибра .25 ACP"), // не сверено
        new("item.30-carbine", "Патроны калибра .30 Carbine"), // не сверено
        new("item.30-06-govt", "Патроны калибра .30-06 винтовочные"),
        new("item.357-magnum", "Патроны калибра .357 «Магнум»"), // не сверено
        new("item.38-special", "Патроны калибра .38 Special"), // не сверено
        new("item.44-magnum", "Патроны калибра .44 «Магнум»"), // не сверено
        new("item.45-automatic", "Патроны калибра .45 ACP"),
        new("item.5-56mm", "Патроны калибра 5.56 мм"), // не сверено
        new("item.9mm-parabellum", "Патроны калибра 9 мм «Парабеллум»"), // не сверено
        new("item.10-gauge-shell", "Ружейные патроны 10-го калибра"),
        new("item.12-gauge-shell", "Ружейные патроны 12-го калибра"),
        new("item.16-gauge-shell", "Ружейные патроны 16-го калибра"),
        new("item.20-gauge-shell", "Ружейные патроны 20-го калибра"),
        // Взрывчатка
        new("item.dynamite-stick", "Динамитная шашка"),
        // Женская одежда
        new("item.chic-designer-dress", "Дизайнерское платье на один вызов"),
        new("item.acrylic-two-piece-suit", "Костюм-двойка из акрила"), // не сверено
        new("item.haute-couture-dress", "Модное платье от-кутюр"), // спорно: в прейскуранте похожего платья нет
        new("item.satin-charmeuse", "Платье из тонкого атласа"),
        new("item.gingham-dress", "Платье из хлопчатой клетчатой ткани"),
        new("item.silk-taffeta-frock", "Платье из шелковой тафты"),
        new("item.rayon-knit-button-front-dress", "Платье на пуговицах трикотажное из вискозы"), // не сверено
        new("item.draped-silk-dress", "Тонкое шелковое платье с драпировкой"), // не сверено
        // Жильё
        new("item.apartment-good", "Апартаменты, аренда"), // спорно: сопоставлено с «Apartment, Good (rent per week)»
        new("item.ymca-furnished-room", "Благотворительное общежитие (койка)"), // спорно: сопоставлено по смыслу
        new("item.large-house", "Большой дом"),
        new("item.town-house", "Городской особняк"),
        new("item.luxury-house-rent", "Дом роскошный, аренда"), // не сверено
        new("item.house-rent-per-year", "Дом, аренда Год"),
        new("item.house-rent-per-month", "Дом, аренда Месяц"),
        new("item.country-house", "Загородный дом"),
        new("item.luxury-apartment-rent", "Квартира роскошная, аренда"), // не сверено
        new("item.apartment-average", "Квартира, аренда"), // спорно: сопоставлено с «Apartment, Average (rent per week)»
        new("item.summer-house-rent", "Летний дом, аренда"),
        new("item.pre-fabricated-house-large", "Модульные дома: большой (9 комнат)"),
        new("item.pre-fabricated-house-small", "Модульные дома: маленький (6 комнат)"),
        new("item.pre-fabricated-house-massive", "Модульные дома: огромный (24 жилых комнаты)"),
        new("item.pre-fabricated-house-mid", "Модульные дома: средний (8 комнат)"),
        new("item.budget-motel", "Мотель эконом-класса"), // не сверено
        new("item.fleabag-hotel", "Ночлежка"),
        new("item.bungalow", "Одноэтажный летний дом"),
        new("item.five-star-hotel", "Пятизвёздочная гостиница"), // не сверено
        new("item.luxury-hotel", "Роскошная гостиница"),
        new("item.average-house", "Средний дом"),
        new("item.average-hotel-with-service-per-week", "Средняя гостиница (в неделю, с обслуживанием)"),
        new("item.studio-apartment-rent", "Студия, аренда"), // не сверено
        new("item.good-hotel", "Хорошая гостиница"),
        new("item.good-hotel-with-service-per-week", "Хорошая гостиница (в неделю, с обслуживанием)"),
        // Запчасти
        new("item.auto-battery", "Аккумулятор"),
        new("item.auto-luggage-carrier", "Багажник на крышу"),
        new("item.jack", "Домкрат"),
        new("item.replacement-headlamp", "Запасная фара"),
        new("item.tire-repair-kit", "Набор для ремонта шин"),
        new("item.auto-spot-light", "Прожектор на крышу"),
        new("item.radiator", "Радиатор"),
        new("item.portable-air-pump", "Ручной насос"),
        new("item.tire-snow-chains", "Цепи противоскольжения"),
        new("item.tire", "Шина"),
        // Иномарки
        new("item.bmw-dixi", "BMW Dixi (Германия)"),
        new("item.bentley-3-litre", "«Бентли» с 3-литровым двигателем (Англия)"),
        new("item.hispano-suiza-alfonso", "«Испано-Сюиза Альфонсо» (Испания)"),
        new("item.lancia-lambda-214", "«Ланча» Lambda 214 (Италия)"),
        new("item.mercedes-benz-ss", "«Мерседес-Бенц» SS (Германия)"),
        new("item.renault-ax", "«Рено» AX (Франция)"),
        new("item.rolls-royce-phantom-i", "«Роллс-Ройс» (Англия): Phantom I"),
        new("item.rolls-royce-silver-ghost", "«Роллс-Ройс» (Англия): Silver Ghost"),
        new("item.citroen-c3", "«Ситроен» С3 (Франция)"),
        // Инструменты
        new("item.large-steel-pulley", "Большой стальной ворот"),
        new("item.rope-50-feet", "Верёвка (15 метров)"),
        new("item.air-compressor", "Воздушный компрессор 220 л"), // не сверено
        new("item.hand-drill", "Дрель ручная (8 свёрл в комплекте)"),
        new("item.padlock", "Замок навесной"),
        new("item.rotary-tool-grinder", "Заточный станок"),
        new("item.linemans-climbers", "Котли монтёрские"), // в имени v1 опечатка: «когти»
        new("item.crowbar", "Лом"),
        new("item.shovel", "Лопата"),
        new("item.tool-outfit-20-tools", "Набор (20 инструментов)"),
        new("item.home-tool-set", "Набор инструментов для дома (в ящике)"),
        new("item.watchmakers-tool-kit", "Набор инструментов часовщика"),
        new("item.lock-picks", "Набор отмычек"), // не сверено
        new("item.locksmiths-tools", "Набор слесарных инструментов"), // не сверено
        new("item.jewelers-tool-set", "Набор ювелира, 48 инструментов"),
        new("item.gasoline-blowtorch", "Паяльная лампа бензиновая"),
        new("item.electricians-gloves", "Печатки электрика"), // в имени v1 опечатка: «перчатки»
        new("item.hand-saw", "Пила ручная"),
        new("item.tool-belt-and-safety-strap", "Пояс для инструментов со страховкой"),
        new("item.welding-outfit", "Сварочный аппарат с принадлежностями"), // не сверено
        new("item.light-chain", "Цепь (за метр)"),
        // Медикаменты
        new("item.epsom-salts", "Английская соль"),
        new("item.burn-kit", "Аптечка первой помощи при ожогах"), // не сверено
        new("item.first-aid-kit", "Аптечка, полностью укомплектованная"), // не сверено
        new("item.aspirin", "Аспирин (12 таблеток)"),
        new("item.gauze-bandages", "Бинт марлевый (5 метров)"),
        new("item.clinical-thermometer", "Градусник"),
        new("item.leather-ankle-supports", "Кожаный бандаж на голеностоп"),
        new("item.maple-crutches", "Костыли деревянные"),
        new("item.wheel-chair", "Кресло-каталка"),
        new("item.adhesive-plaster", "Лейкопластырь"),
        new("item.medical-case", "Медицинский чемодан"),
        new("item.doctors-bag", "Медицинский чемодан-укладка"), // спорно: второй медицинский чемодан, в прейскуранте 1920-х его нет
        new("item.bed-pan", "Медицинское судно"),
        new("item.metal-arch-supports", "Металлический супинатор"),
        new("item.scalpel-set", "Набор скальпелей"),
        new("item.portable-resuscitator", "Портативный аппарат ИВЛ"), // не сверено
        new("item.atomizer", "Распылитель"),
        new("item.disposable-respirator", "Респиратор одноразовый"), // не сверено
        new("item.laxative", "Слабительное натуральное"),
        new("item.alcohol", "Спирт (литр)"),
        new("item.indigestion-medicine", "Средство от несварения"),
        new("item.hard-rubber-syringe", "Стрихнинка"), // спорно: в имени v1, видимо, «спринцовка»
        new("item.hypodermic-syringes", "Шприцы для инъекций"),
        new("item.forceps", "Щипцы"),
        // Мужская одежда
        new("item.hiking-boots", "Ботинки туристические"),
        new("item.silk-necktie", "Галстук шелковый"),
        new("item.batwing-bow-tie", "Галстук-бабочка"),
        new("item.worsted-wool-dress-suit", "Деловой костюм из камвольной шерсти"),
        new("item.cashmere-dress-suit", "Деловой костюм из кашемира"),
        new("item.cuff-links", "Заколки"), // в имени v1 опечатка: «запонки»
        new("item.leather-football-helmet", "Кожаный футбольный шлем"),
        new("item.bathing-suit", "Купальный костюм"),
        new("item.outdoor-coat", "Куртка"), // спорно: сопоставлено по смыслу
        new("item.mohair-suit", "Мохеровый костюм"),
        new("item.felt-fedora", "Мягкая фетровая шляпа"),
        new("item.union-suit-forest-mills", "Нижний фланелевый комбинезон «Форест Миллс»"),
        new("item.chesterfield-overcoat", "Пальто «Честерфилд»"),
        new("item.shaker-sweater", "Пейзжерский свитер"),
        new("item.suspenders", "Подтяжки"),
        new("item.sock-garters", "Подтяжки для носков"),
        new("item.leather-work-shoes", "Рабочие кожаные ботинки"),
        new("item.leather-belt", "Ремень кожаный"),
        new("item.straw-hat", "Соломенная шляпа"),
        new("item.percale-shirt", "Сорочка хлопчатобумажная"), // спорно: сопоставлено по смыслу
        new("item.broadcloth-dress-shirt", "Сорочка шелковая"), // спорно: сопоставлено по смыслу
        new("item.sweatshirt", "Спортивная фуфайка"),
        new("item.wool-golf-cap", "Типовая кепка"), // спорно: сопоставлено по смыслу
        new("item.oxford-dress-shoes", "Туфли-оксфорды"),
        new("item.white-flannel-slacks", "Широкие брюки из белой фланели"),
        // Палатки
        new("item.auto-bed", "Автокровать"),
        new("item.car-tent-7-x-7-foot", "Автопалатка 2×2 метра"),
        new("item.insulated-tank", "Бак-термос (19 литров)"),
        new("item.water-bag-5-gallon", "Бурдюк (19 литров)"),
        new("item.water-bag-1-gallon", "Бурдюк (4 литра)"),
        new("item.iron-tent-stakes", "Железная кровать для палатки (12 шт.)"), // ошибка перевода v1: колышки, а не кровать
        new("item.tent-7-x-7-foot", "Палатка 2×2 метра"),
        new("item.tent-12-x-16-foot", "Палатка 4×5 метров"),
        new("item.tent-16-x-24-foot", "Палатка 5×6 метров"),
        new("item.canteen", "Флаг (1 метр)"), // ошибка перевода v1: «фляга»
        new("item.tarpaulin-24-x-36-foot", "Шатёр 6×12 метров"), // спорно: в книге брезент
        // Питание
        new("item.breakfast", "Завтрак"),
        new("item.lunch", "Обед"),
        new("item.chicken-dinner", "Торжественный ужин"), // спорно: сопоставлено по смыслу
        new("item.dinner", "Ужин"),
        // Походное снаряжение
        new("item.can-of-carbide", "Банка карбида (1 кг)"),
        new("item.batteries", "Батарейки"),
        new("item.gasoline-lantern", "Бензиновая лампа"),
        new("item.binoculars", "Бинокль морской"),
        new("item.field-glasses", "Бинокль полевой (3–6 кратный)"),
        new("item.motor-home", "Дом на колёсах"), // не сверено
        new("item.coil-spring-animal-trap", "Капкан"),
        new("item.carbide-lamp", "Карбидная лампа"),
        new("item.pen-light", "Карманный фонарик"),
        new("item.kerosene-lantern", "Керосиновая лампа"),
        new("item.small-live-animal-trap", "Клетка-ловушка"),
        new("item.jeweled-compass", "Компас военный"), // спорно: сопоставлено по смыслу
        new("item.compass-with-lid", "Компас туристический"), // спорно: сопоставлено по смыслу
        new("item.cooking-kit", "Кухонные принадлежности"),
        new("item.bear-trap", "Медвежий капкан"),
        new("item.waterproof-blanket", "Непромокаемое одеяло"),
        new("item.hunting-knife", "Охотничий нож"),
        new("item.three-person-dome-tent", "Палатка-геокупол 3-местная"), // не сверено
        new("item.dark-lantern", "Потайной фонарь"),
        new("item.camp-stove", "Походная плитка"),
        new("item.heavy-canvas-shoulder-bag", "Походный шлёпкий рюкзак"), // спорно: сопоставлено по смыслу
        new("item.folding-camp-bed", "Раскладушка"),
        new("item.three-room-family-tent", "Семейная палатка 3-комнатная"), // не сверено
        new("item.folding-bathtub", "Складная ванна"),
        new("item.pocket-knife", "Складной нож"),
        new("item.telescope", "Телескоп"),
        new("item.vacuum-bottle", "Термос"),
        new("item.hand-axe", "Топор"),
        new("item.fishing-rod-and-tackle", "Удочка со снастью"),
        new("item.light-stick", "Фальшивый фонарь (одноразовый)"), // не сверено
        new("item.searchlight", "Фонарь"), // спорно: сопоставлено по смыслу
        new("item.mosquito-head-net", "Шляпа (москитка)"), // не сверено
        new("item.electric-torch", "Электрический фонарь"),
        new("item.generator", "Электрогенератор"), // не сверено
        // Прочие покупки
        new("item.bible", "Библия"),
        new("item.sketch-pad", "Блокнот"), // спорно: сопоставлено по смыслу
        new("item.dictaphone", "Диктофон"),
        new("item.glass-door-oak-bookcase", "Дубовый книжный шкаф со стеклянными дверцами"),
        new("item.extra-handcuff-key", "Запасной ключ от наручников"),
        new("item.umbrella", "Зонтик"),
        new("item.pocket-magnifying-glass", "Карманная лупа"),
        new("item.pocket-microscope", "Карманный микроскоп"),
        new("item.mechanical-pencil", "Механический карандаш"),
        new("item.floor-safe", "Напольный сейф (430 кг)"),
        new("item.handcuffs", "Наручники"),
        new("item.desk-microscope", "Настольный микроскоп (увеличение ×110)"),
        new("item.self-filling-fountain-pen", "Перьевая ручка"),
        new("item.remington-typewriter", "Пишущая машинка"),
        new("item.harris-typewriter", "Пишущая машинка «Харрис»"),
        new("item.writing-tablet", "Планшет для письма"),
        new("item.police-whistle", "Полицейский свисток"),
        new("item.complete-diving-suit", "Полный водолазный костюм"),
        new("item.unabridged-dictionary", "Полный толковый словарь"),
        new("item.wire-recorder", "Проволочный самописец"),
        new("item.wet-sponge-respirator", "Респиратор с фильтром"),
        new("item.four-candles", "Свечи (4 шт.)"),
        new("item.cigarettes", "Сигареты (пачка)"),
        new("item.box-of-cigars", "Сигары (коробка)"),
        new("item.folding-writing-desk", "Складной письменный стол"),
        new("item.straitjacket", "Смирительная рубашка"),
        new("item.globe-on-stand", "Трюбка на подставке"), // спорно: в имени v1, видимо, «глобус»
        new("item.turkish-water-pipe", "Турецкий кальян"),
        new("item.gold-pocket-watch", "Часы карманные золотые"),
        new("item.wrist-watch", "Часы наручные"),
        new("item.briefcase", "Чемоданчик"),
        new("item.ten-volume-encyclopedia", "Энциклопедия в десяти томах"),
        // Путешествия
        new("item.four-man-hot-air-balloon", "4-местный воздушный шар"),
        new("item.travel-air-2000-biplane", "Билет Travel Air 2000"), // в книге — покупка самолёта, не билет
        new("item.second-class-round-trip", "Билет вторым классом (туда-обратно)"), // спорно: сопоставлено по смыслу
        new("item.freight-ticket", "Билет грузоперевозки"), // не сверено
        new("item.bus-fare", "Билет на автобус"),
        new("item.international-airfare", "Билет на международный рейс (за 200 км)"),
        new("item.airfare", "Билет на самолёт (за 20 км)"),
        new("item.streetcar-fare", "Билет на трамвай"),
        new("item.first-class-one-way", "Билет первым классом (в один конец)"),
        new("item.airfare-first-class-one-way", "Билет первым классом (в один конец) - Экономкласс"), // не сверено
        new("item.train-fare-50-miles", "Билет по железной дороге (100 км)"),
        new("item.train-fare-500-miles", "Билет по железной дороге (1000 км)"),
        new("item.train-fare-100-miles", "Билет по железной дороге (200 км)"),
        new("item.steerage", "Билет третьим классом"),
        new("item.airfare-economy-one-way", "Билет экономкласс (в один конец)"), // не сверено
        new("item.transcontinental-ticket", "Трансконтинентальный билет"), // не сверено
        // Развлечения
        new("item.jazz-banjo", "Банджо 4-струнное"),
        new("item.movie-ticket-nickelodeon", "Билет в дешёвый кинотеатр"),
        new("item.movie-ticket-seated", "Билет в кинотеатр (сидячее место)"),
        new("item.professional-baseball-ticket", "Билет на бейсбол"),
        new("item.concert-hall-public-seating", "Концертный зал 2, ложа"), // спорно: сопоставлено по смыслу
        new("item.concert-hall-box", "Концертный зал, ложа"),
        new("item.cabinet-phonograph", "Патефон станционный"),
        new("item.phonograph-records", "Патефонная пластинка"),
        new("item.film-developing-kit", "Реактивы для проявки и фиксации снимков"),
        new("item.brass-saxophone", "Саксофон тенор"),
        new("item.box-brownie-camera", "Фотоаппарат «Браун»"),
        new("item.kodak-folding-no-1-camera", "Фотоаппарат «Кодак № 1» складной"),
        new("item.film-24-exposures", "Фотоплёнка, 24 кадра"),
        // Средства связи
        new("item.cordless-phone", "Беспроводной телефон"), // не сверено
        new("item.newspaper", "Газета"),
        new("item.local-phone-call", "Местная телефонная связь"), // не сверено
        new("item.cell-phone", "Мобильный телефон"), // не сверено
        new("item.post-card", "Открытка"),
        new("item.postage-per-ounce", "Посылка (за 100 г)"),
        new("item.telegraph-outfit", "Ручной радиотелеграф"),
        new("item.smartphone", "Смартфон"), // не сверено
        new("item.console-radio-receiver", "Стационарный радиоприёмник"),
        new("item.telegram-12-words", "Телеграмма: до 12 слов"),
        new("item.telegram-per-additional-word", "Телеграмма: каждое дополнительное слово"),
        new("item.telegram-international-per-word", "Телеграмма: международная (за слово)"),
        new("item.desk-phone", "Телефон настольный (индукторный тип)"),
        // Транспорт
        new("item.1917-buick-used", "«Бьюик» 1917 (подержанный)"),
        new("item.buick-model-d-45", "«Бьюик» Model D-45"),
        new("item.dodge-model-s-1", "«Додж» Model S/1"),
        new("item.duesenberg-j", "«Дюзенберг» J"),
        new("item.cadillac-type-55", "«Кадиллак» Type 55"),
        new("item.chrysler-model-f-58", "«Крайслер» Model F-58"),
        new("item.oldsmobile-43-at", "«Олдсмобиль» 43-AT"),
        new("item.packard-twin-six-touring", "«Паккард» Twin Six Touring"),
        new("item.pierce-arrow", "«Пирс-Арроу»"),
        new("item.pontiac-6-28-sedan", "«Понтиак» седан 6-28"),
        new("item.rolls-royce-ghost-sedan", "«Роллс-Ройс» Ghost Sedan"), // не сверено
        new("item.studebaker-standard-dictator", "«Студебеккер» Stand./Dictator"),
        new("item.studebaker-touring", "«Студебеккер» Touring (5-местный)"),
        new("item.ford-model-a", "«Форд» Model A"),
        new("item.ford-model-t", "«Форд» Model T"),
        new("item.hudson-coach", "«Хадсон» 8-местный"),
        new("item.hudson-super-six-series-j", "«Хадсон» Super Six Series J"),
        new("item.chevrolet-capitol", "«Шевроле» Capitol"),
        new("item.chevrolet-fb-coupe-used", "«Шевроле» F. B. Coupe (подержанный)"),
        new("item.chevrolet-roadster", "«Шевроле» Roadster"),
        new("item.armored-car", "Бронеавтомобиль"), // не сверено
        new("item.ford-model-tt-truck", "Грузовик «Форд» Model TT"),
        new("item.dodge-half-ton-truck", "Лёгкий грузовик «Додж»"),
        new("item.bmw-motorcycle", "Мотоцикл BMW"), // не сверено
        new("item.norton-motorcycle", "Мотоцикл Norton"),
        new("item.ducati-motorcycle", "Мотоцикл «Дукати»"), // не сверено
        new("item.chevrolet-pickup-truck", "Пикап «Шевроле»"),
        // «Фотоаппараты» в v1 (на деле — музыкальные инструменты и прочее из раздела развлечений)
        new("item.accordion", "Аккордеон"),
        new("item.army-bugle", "Армейский рожок"),
        new("item.guitar", "Гитара (в футляре)"),
        new("item.16mm-movie-camera-and-projector", "Кинокамера и проектор 16 мм"),
        new("item.player-piano", "Механическое пианино"),
        new("item.portable-radio-receiver", "Переносной радиоприёмник"),
        new("item.violin", "Скрипка (в футляре)"),
        new("item.ukulele", "Укулеле (в футляре)"),
        new("item.parlor-organ", "Фисгармония"),
        new("item.eastman-commercial-camera", "Фотоаппарат «Истмен» профессиональный"),
        // Без кода. Реквизит сценариев (самодельное): «Каменный гроб служителя Гла'аки», «Осколок кристаллической
        // темницы», «Шип Гла'аки», «Дневник Джозефа Тёрнера». Повторы книжных записей под другим именем v1:
        // «Проживание в гостинице: неплохая гостиница» (= «Хорошая гостиница»), «Проживание в гостинице: в целом,
        // с обслуживанием» (= «Средняя гостиница (в неделю, с обслуживанием)»), «Проживание в гостинице: с неплохо,
        // со обслуживанием» (= «Хорошая гостиница (в неделю, с обслуживанием)»).
    ]);

    /// <summary>Код книжной записи по имени; null — самодельная.</summary>
    public static string? FromName(string name) => Table.FromName(name);
}
