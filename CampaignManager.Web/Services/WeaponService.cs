using CampaignManager.Web.Model;

namespace CampaignManager.Web.Services;

/// <summary>
/// Сервис для хранения и предоставления информации об оружии в игре "Зов Ктулху" 7-й редакции.
/// </summary>
public class WeaponService
{
    private readonly List<CloseCombatWeapon> _closeCombatWeapons;
    private readonly List<RangedCombatWeapon> _rangedWeapons;

    /// <summary>
    /// Инициализирует новый экземпляр класса WeaponService и загружает данные об оружии.
    /// </summary>
    public WeaponService()
    {
        _closeCombatWeapons = InitializeCloseCombatWeapons();
        _rangedWeapons = InitializeRangedWeapons();
    }

    /// <summary>
    /// Возвращает список всего доступного оружия ближнего боя.
    /// </summary>
    /// <returns>Список оружия ближнего боя.</returns>
    public List<CloseCombatWeapon> GetAllCloseCombatWeapons() => new List<CloseCombatWeapon>(_closeCombatWeapons);

    /// <summary>
    /// Возвращает список всего доступного оружия дальнего боя.
    /// </summary>
    /// <returns>Список оружия дальнего боя.</returns>
    public List<RangedCombatWeapon> GetAllRangedWeapons() => new List<RangedCombatWeapon>(_rangedWeapons);

    /// <summary>
    /// Находит оружие ближнего боя по названию.
    /// </summary>
    /// <param name="name">Название оружия (регистронезависимо).</param>
    /// <returns>Найденное оружие или null, если не найдено.</returns>
    public CloseCombatWeapon? GetCloseCombatWeaponByName(string name)
    {
        return _closeCombatWeapons.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Находит оружие дальнего боя по названию.
    /// </summary>
    /// <param name="name">Название оружия (регистронезависимо).</param>
    /// <returns>Найденное оружие или null, если не найдено.</returns>
    public RangedCombatWeapon? GetRangedWeaponByName(string name)
    {
        return _rangedWeapons.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }


    /// <summary>
    /// Инициализирует и возвращает список оружия ближнего боя.
    /// </summary>
    private List<CloseCombatWeapon> InitializeCloseCombatWeapons()
    {
        // Примечание: БП = Бонус повреждений
        return new List<CloseCombatWeapon>
        {
            new CloseCombatWeapon { Name = "Лук и стрелы", Skill = "Стрельба (Лук)", Damage = "1D6+1/2 БП", Range = "30 ярдов", Attacks = "1", Cost = "$7", Notes = "Осечка 97" },
            new CloseCombatWeapon { Name = "Кастет", Skill = "Ближний бой (Драка)", Damage = "1D3+1+БП", Range = "Касание", Attacks = "1", Cost = "$1", Notes = "" },
            new CloseCombatWeapon { Name = "Кнут", Skill = "Ближний бой (Кнут)", Damage = "1D3+1/2 БП", Range = "10 футов", Attacks = "1", Cost = "$5", Notes = "" },
            new CloseCombatWeapon { Name = "Горящий факел", Skill = "Ближний бой (Драка)", Damage = "1D6+горение", Range = "Касание", Attacks = "1", Cost = "$0.05", Notes = "Горение" },
            new CloseCombatWeapon { Name = "Дубинка (кистень, блэкджек)", Skill = "Ближний бой (Драка)", Damage = "1D8+БП", Range = "Касание", Attacks = "1", Cost = "$2", Notes = "" },
            new CloseCombatWeapon { Name = "Дубина, большая (бейсбольная/крикетная бита, кочерга)", Skill = "Ближний бой (Драка)", Damage = "1D8+БП", Range = "Касание", Attacks = "1", Cost = "$3", Notes = "" },
            new CloseCombatWeapon { Name = "Дубина, малая (полицейская дубинка)", Skill = "Ближний бой (Драка)", Damage = "1D6+БП", Range = "Касание", Attacks = "1", Cost = "$3", Notes = "" },
            new CloseCombatWeapon { Name = "Арбалет", Skill = "Стрельба (Лук)", Damage = "1D8+2", Range = "50 ярдов", Attacks = "1/2", Cost = "$10", Notes = "(пронз.), Осечка 96" },
            new CloseCombatWeapon { Name = "Гаррота*", Skill = "Ближний бой (Гаррота)", Damage = "1D6+БП", Range = "Касание", Attacks = "1", Cost = "$0.50", Notes = "*(пронз.), Особые правила" },
            new CloseCombatWeapon { Name = "Топорик/Серп", Skill = "Ближний бой (Топор)", Damage = "1D6+1+БП", Range = "Касание", Attacks = "1", Cost = "$3", Notes = "(пронз.)" },
            new CloseCombatWeapon { Name = "Нож, большой (мачете и т.п.)", Skill = "Ближний бой (Драка)", Damage = "1D8+БП", Range = "Касание", Attacks = "1", Cost = "$4", Notes = "(пронз.)" },
            new CloseCombatWeapon { Name = "Нож, средний (разделочный нож и т.п.)", Skill = "Ближний бой (Драка)", Damage = "1D4+2+БП", Range = "Касание", Attacks = "1", Cost = "$2", Notes = "(пронз.)" },
            new CloseCombatWeapon { Name = "Нож, малый (выкидной нож и т.п.)", Skill = "Ближний бой (Драка)", Damage = "1D4+БП", Range = "Касание", Attacks = "1", Cost = "$2", Notes = "(пронз.)" },
            new CloseCombatWeapon { Name = "Нунчаку", Skill = "Ближний бой (Цеп)", Damage = "1D8+БП", Range = "Касание", Attacks = "1", Cost = "$1", Notes = "" },
            new CloseCombatWeapon { Name = "Камень, брошенный", Skill = "Метание", Damage = "1D4+1/2 БП", Range = "СИЛ/5 ярдов", Attacks = "1", Cost = "-", Notes = "" },
            new CloseCombatWeapon { Name = "Сюрикен", Skill = "Метание", Damage = "1D3+1/2 БП", Range = "СИЛ/5 ярдов", Attacks = "2", Cost = "$0.50", Notes = "(пронз.), Однораз., Осечка 100" },
            new CloseCombatWeapon { Name = "Копье (кавалерийская пика)", Skill = "Ближний бой (Копье)", Damage = "1D8+1", Range = "Касание", Attacks = "1", Cost = "$25", Notes = "(пронз.)" },
            new CloseCombatWeapon { Name = "Копье, метательное", Skill = "Метание", Damage = "1D8+1/2 БП", Range = "СИЛ/5 ярдов", Attacks = "1", Cost = "$1", Notes = "(пронз.)" },
            // Дополнительное оружие ближнего боя из списка
            new CloseCombatWeapon { Name = "Рапира", Skill = "Ближний бой (Меч)", Damage = "1D6+1+БП", Range = "Касание", Attacks = "1", Cost = "$12.50", Notes = "(пронз.)" },
            new CloseCombatWeapon { Name = "Штык", Skill = "Ближний бой (Копье/Винтовка)", Damage = "1D6+БП", Range = "Касание", Attacks = "1", Cost = "$3.75", Notes = "(пронз.), Крепится к винтовке" },
            new CloseCombatWeapon { Name = "Кинжал", Skill = "Ближний бой (Драка)", Damage = "1D4+БП", Range = "Касание", Attacks = "1", Cost = "$2.50", Notes = "(пронз.)" },
            new CloseCombatWeapon { Name = "Опасная бритва", Skill = "Ближний бой (Драка)", Damage = "1D4", Range = "Касание", Attacks = "1", Cost = "65¢ - $5.25", Notes = "" },
            new CloseCombatWeapon { Name = "Дубинка (12-дюймовая)", Skill = "Ближний бой (Драка)", Damage = "1D6+БП", Range = "Касание", Attacks = "1", Cost = "$1.98", Notes = "" },
            new CloseCombatWeapon { Name = "Хлыст (для лошадей)", Skill = "Ближний бой (Кнут)", Damage = "1D3", Range = "10 футов", Attacks = "1", Cost = "60¢", Notes = "" },
            new CloseCombatWeapon { Name = "Топор (4 фунта)", Skill = "Ближний бой (Топор)", Damage = "1D8+2+БП", Range = "Касание", Attacks = "1", Cost = "$1.95", Notes = "(пронз.)" },
            new CloseCombatWeapon { Name = "Кнут (16 футов)", Skill = "Ближний бой (Кнут)", Damage = "1D4+1/2 БП", Range = "16 футов", Attacks = "1", Cost = "$1.75", Notes = "" }
        };
    }

    /// <summary>
    /// Инициализирует и возвращает список оружия дальнего боя.
    /// </summary>
    private List<RangedCombatWeapon> InitializeRangedWeapons()
    {
        // Примечания: (П) = Пистолет, (В/Д) = Винтовка/Дробовик, (ППМ) = Пистолет-пулемёт, (ПМ) = Пулемёт, (ТВ) = Тяжёлое вооружение
        // (пронз.) = Пронзающее, (2ств.) = 2 ствола, (авто) = Автоматический огонь
        return new List<RangedCombatWeapon>
        {
            // Пистолеты (П)
            new RangedCombatWeapon { Name = "Автоматический пистолет .22 Short", Skill = "Стрельба (П)", Damage = "1D6", Range = "10 ярдов", Attacks = "1 (3)", Ammo = "6", Malfunction = "100", Cost = "$25", Notes = "(пронз.)*" },
            new RangedCombatWeapon { Name = "Дерринджер .25", Skill = "Стрельба (П)", Damage = "1D6", Range = "3 ярда", Attacks = "1", Ammo = "1", Malfunction = "100", Cost = "$12", Notes = "(1ств.), (пронз.)*" },
            new RangedCombatWeapon { Name = "Револьвер .32 или 7.65мм", Skill = "Стрельба (П)", Damage = "1D8", Range = "15 ярдов", Attacks = "1 (3)", Ammo = "6", Malfunction = "100", Cost = "$15", Notes = "(пронз.)*" },
            new RangedCombatWeapon { Name = "Автоматический пистолет .32 или 7.65мм", Skill = "Стрельба (П)", Damage = "1D8", Range = "15 ярдов", Attacks = "1 (3)", Ammo = "8", Malfunction = "99", Cost = "$20", Notes = "(пронз.)*" },
            new RangedCombatWeapon { Name = "Люгер P08", Skill = "Стрельба (П)", Damage = "1D10", Range = "15 ярдов", Attacks = "1 (3)", Ammo = "8", Malfunction = "99", Cost = "$75", Notes = "(пронз.)*" },
            new RangedCombatWeapon { Name = "Револьвер .45", Skill = "Стрельба (П)", Damage = "1D10+2", Range = "15 ярдов", Attacks = "1 (3)", Ammo = "6", Malfunction = "100", Cost = "$30", Notes = "(пронз.)*" },
            new RangedCombatWeapon { Name = "Автоматический пистолет .45", Skill = "Стрельба (П)", Damage = "1D10+2", Range = "15 ярдов", Attacks = "1 (3)", Ammo = "7", Malfunction = "100", Cost = "$40", Notes = "(пронз.)*" },

            // Винтовки (В/Д)
            new RangedCombatWeapon { Name = "Винтовка .22 с продольно-скользящим затвором", Skill = "Стрельба (В/Д)", Damage = "1D6+1", Range = "30 ярдов", Attacks = "1", Ammo = "6", Malfunction = "99", Cost = "$13", Notes = "(пронз.)*" },
            new RangedCombatWeapon { Name = "Карабин .30 рычажного действия", Skill = "Стрельба (В/Д)", Damage = "2D6", Range = "50 ярдов", Attacks = "1", Ammo = "6", Malfunction = "98", Cost = "$19", Notes = "(пронз.)*" },
            new RangedCombatWeapon { Name = "Винтовка Мартини-Генри .45", Skill = "Стрельба (В/Д)", Damage = "1D8+1D6+3", Range = "80 ярдов", Attacks = "1/3", Ammo = "1", Malfunction = "100", Cost = "$20", Notes = "(пронз.)*" },
            new RangedCombatWeapon { Name = "Пневматическая винтовка полковника Морана", Skill = "Стрельба (В/Д)", Damage = "2D6+1", Range = "20 ярдов", Attacks = "1/3", Ammo = "1", Malfunction = "88", Cost = "$200", Notes = "Относительно тихая" }, // Не помечена (пронз.)
            new RangedCombatWeapon { Name = "Ли-Энфилд .303", Skill = "Стрельба (В/Д)", Damage = "2D6+4", Range = "110 ярдов", Attacks = "1", Ammo = "10", Malfunction = "100", Cost = "$50", Notes = "(пронз.)*" },
            new RangedCombatWeapon { Name = "Винтовка .30-06 с продольно-скользящим затвором", Skill = "Стрельба (В/Д)", Damage = "2D6+4", Range = "110 ярдов", Attacks = "1", Ammo = "5", Malfunction = "100", Cost = "$75", Notes = "(пронз.)*" },
            new RangedCombatWeapon { Name = "Штуцер для слонов", Skill = "Стрельба (В/Д)", Damage = "3D6+4", Range = "100 ярдов", Attacks = "1 или 2", Ammo = "2", Malfunction = "100", Cost = "$400", Notes = "(2ств.), (пронз.)*" },

            // Дробовики (В/Д)
            new RangedCombatWeapon { Name = "Дробовик 20 калибра", Skill = "Стрельба (В/Д)", Damage = "2D6/1D6/1D3", Range = "10/20/50 ярдов", Attacks = "1 или 2", Ammo = "2", Malfunction = "100", Cost = "$35", Notes = "(2ств.)*" },
            new RangedCombatWeapon { Name = "Дробовик 16 калибра", Skill = "Стрельба (В/Д)", Damage = "2D6+2/1D6+1/1D4", Range = "10/20/50 ярдов", Attacks = "1 или 2", Ammo = "2", Malfunction = "100", Cost = "$40", Notes = "(2ств.)*" },
            new RangedCombatWeapon { Name = "Дробовик 12 калибра", Skill = "Стрельба (В/Д)", Damage = "4D6/2D6/1D6", Range = "10/20/50 ярдов", Attacks = "1 или 2", Ammo = "2", Malfunction = "100", Cost = "$40", Notes = "(2ств.)*" },
            new RangedCombatWeapon { Name = "Дробовик 12 калибра (полуавтоматический)", Skill = "Стрельба (В/Д)", Damage = "4D6/2D6/1D6", Range = "10/20/50 ярдов", Attacks = "1 (2)", Ammo = "5", Malfunction = "100", Cost = "$45", Notes = "*" },
            new RangedCombatWeapon { Name = "Дробовик 12 калибра (обрез, 2ств.)", Skill = "Стрельба (В/Д)", Damage = "4D6/1D6", Range = "5/10 ярдов", Attacks = "1 или 2", Ammo = "2", Malfunction = "100", Cost = "N/A", Notes = "(2ств.)*" },

            // Пистолеты-пулемёты (ППМ)
            new RangedCombatWeapon { Name = "Bergmann MP18/MP28", Skill = "Стрельба (ППМ)", Damage = "1D10", Range = "20 ярдов", Attacks = "1 (2) или авто", Ammo = "20/30/32", Malfunction = "96", Cost = "$1,000", Notes = "(пронз.)" },
            new RangedCombatWeapon { Name = "Thompson", Skill = "Стрельба (ППМ)", Damage = "1D10+2", Range = "20 ярдов", Attacks = "1 или авто", Ammo = "20/30/50", Malfunction = "96", Cost = "$200+", Notes = "(пронз.)" },

            // Пулемёты (ПМ)
            new RangedCombatWeapon { Name = "Автоматическая винтовка Browning M1918 (BAR)", Skill = "Стрельба (ПМ)", Damage = "2D6+4", Range = "90 ярдов", Attacks = "1 (2) или авто", Ammo = "20", Malfunction = "100", Cost = "$800", Notes = "(пронз.)" },
            new RangedCombatWeapon { Name = "Browning M1917A1 .30", Skill = "Стрельба (ПМ)", Damage = "2D6+4", Range = "150 ярдов", Attacks = "Авто", Ammo = "250", Malfunction = "96", Cost = "$3,000", Notes = "(пронз.)" },
            new RangedCombatWeapon { Name = "Bren", Skill = "Стрельба (ПМ)", Damage = "2D6+4", Range = "110 ярдов", Attacks = "1 или авто", Ammo = "30/100", Malfunction = "96", Cost = "$3,000", Notes = "(пронз.)" },
            new RangedCombatWeapon { Name = "Lewis Mark I", Skill = "Стрельба (ПМ)", Damage = "2D6+4", Range = "110 ярдов", Attacks = "Авто", Ammo = "47/97", Malfunction = "96", Cost = "$3,000", Notes = "(пронз.)" },
            new RangedCombatWeapon { Name = "Vickers .303", Skill = "Стрельба (ПМ)", Damage = "2D6+4", Range = "110 ярдов", Attacks = "Авто", Ammo = "250", Malfunction = "N/A", Cost = "N/A", Notes = "(пронз.)" },

             // Тяжёлое вооружение (ТВ) - из изображения
            new RangedCombatWeapon { Name = "Танковая пушка 120 мм (со стаб.)", Skill = "Артиллерия", Damage = "15d10 / 4 метра", Range = "2000 метров", Attacks = "1", Ammo = "Отдельно", Malfunction = "100", Cost = "Нет", Notes = "Современное" },
            new RangedCombatWeapon { Name = "5-дюймовое корабельное орудие (со стаб.)", Skill = "Артиллерия", Damage = "12d10 / 4 метра", Range = "3000 метров", Attacks = "1", Ammo = "Автоподача", Malfunction = "98", Cost = "Нет", Notes = "Современное" },
            new RangedCombatWeapon { Name = "Противопехотная мина", Skill = "Взрывчатка", Damage = "4d10 / 5 метров", Range = "На месте", Attacks = "На месте", Ammo = "Однораз.", Malfunction = "99", Cost = "Нет", Notes = "1920-е, Современное" },
            new RangedCombatWeapon { Name = "Мина «Клеймор»*", Skill = "Взрывчатка", Damage = "6d6 / 20 метров", Range = "На месте", Attacks = "На месте", Ammo = "Однораз.", Malfunction = "99", Cost = "Нет", Notes = "Современное" },
            new RangedCombatWeapon { Name = "Огнемёт", Skill = "Стрельба (Огнемёт)", Damage = "2d6 + горение", Range = "25 метров", Attacks = "1", Ammo = "Минимум 10", Malfunction = "93", Cost = "Нет", Notes = "1920-е, Современное" },
            new RangedCombatWeapon { Name = "РПГ*", Skill = "Стрельба (ТВ)", Damage = "8d10 / 1 метр", Range = "150 метров", Attacks = "1", Ammo = "1", Malfunction = "98", Cost = "Нет", Notes = "Современное" } // Название "РПГ" может быть условным для 1920х

            // Примечание: Нелегальное оружие перечислено отдельно в источнике и может потребовать особой обработки
            // при включении (например, другая доступность, вариативность цены).
        };
    }
}
