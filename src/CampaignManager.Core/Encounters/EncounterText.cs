namespace CampaignManager.Core.Encounters;

/// <summary>
/// Подписи сцены по-русски — одна таблица на приложение (в v1 у журнала погони было три словаря в разметке, и новый вид
/// действия молча оставался без подписи). Тест требует подпись и категорию у каждого члена перечислений.
/// </summary>
public static class EncounterText
{
    public static string Of(EncounterKind kind) => kind switch
    {
        EncounterKind.Combat => "Бой",
        EncounterKind.Chase => "Погоня",
        _ => kind.ToString(),
    };

    public static string Of(EncounterSide side) => side switch
    {
        EncounterSide.Investigators => "сыщики",
        EncounterSide.Enemies => "противники",
        EncounterSide.Neutral => "нейтральные",
        _ => side.ToString(),
    };

    public static string Of(ParticipantKind kind) => kind switch
    {
        ParticipantKind.Investigator => "сыщик",
        ParticipantKind.Npc => "НПС",
        ParticipantKind.Creature => "тварь",
        _ => kind.ToString(),
    };

    public static string Of(EncounterEffectKind kind) => kind switch
    {
        EncounterEffectKind.Damage => "Урон",
        EncounterEffectKind.Heal => "Лечение",
        EncounterEffectKind.MagicPoints => "ПМ",
        EncounterEffectKind.SanityLoss => "Рассудок",
        EncounterEffectKind.Power => "МОЩ",
        EncounterEffectKind.Out => "Выбывание",
        EncounterEffectKind.FirstAid => "Первая помощь",
        EncounterEffectKind.Medicine => "Медицина",
        EncounterEffectKind.DyingCheck => "ВЫН умирающего",
        EncounterEffectKind.Recovery => "Лечение раны",
        EncounterEffectKind.Luck => "Удача",
        EncounterEffectKind.Awake => "Сознание",
        EncounterEffectKind.Attack => "Атака",
        EncounterEffectKind.Defense => "Защита",
        EncounterEffectKind.Autofire => "Очередь",
        EncounterEffectKind.Ammo => "Патроны",
        EncounterEffectKind.Jam => "Заклинивание",
        EncounterEffectKind.Aim => "Прицел",
        EncounterEffectKind.Cover => "Укрытие",
        EncounterEffectKind.Prone => "Положение",
        EncounterEffectKind.Grapple => "Захват",
        EncounterEffectKind.Disarm => "Разоружение",
        EncounterEffectKind.Disadvantage => "Невыгодное положение",
        EncounterEffectKind.Ready => "Огнестрел наготове",
        EncounterEffectKind.Casting => "Сотворение",
        EncounterEffectKind.Treated => "Лечение по ране",
        _ => kind.ToString(),
    };

    public static string Of(EncounterLogKind kind) => kind switch
    {
        EncounterLogKind.Started => "Начало",
        EncounterLogKind.Joined => "Вступил",
        EncounterLogKind.Left => "Покинул",
        EncounterLogKind.Round => "Раунд",
        EncounterLogKind.Delayed => "Отложил ход",
        EncounterLogKind.Effect => "Эффект",
        EncounterLogKind.Sheet => "Лист",
        EncounterLogKind.Note => "Заметка",
        EncounterLogKind.Finished => "Конец",
        EncounterLogKind.Attack => "Атака",
        EncounterLogKind.Maneuver => "Манёвр",
        EncounterLogKind.Spell => "Заклинание",
        EncounterLogKind.Sanity => "Рассудок",
        EncounterLogKind.Medical => "Раны",
        EncounterLogKind.Action => "Действие",
        EncounterLogKind.Initiative => "Инициатива",
        _ => kind.ToString(),
    };

    /// <summary>Категория — цвет записи (журнал погони v1 красил по категории, а не по каждому виду).</summary>
    public static EncounterLogCategory Category(EncounterLogKind kind) => kind switch
    {
        EncounterLogKind.Effect => EncounterLogCategory.Violence,
        EncounterLogKind.Attack or EncounterLogKind.Maneuver => EncounterLogCategory.Violence,
        EncounterLogKind.Spell => EncounterLogCategory.Magic,
        EncounterLogKind.Sanity => EncounterLogCategory.Sanity,
        EncounterLogKind.Action => EncounterLogCategory.Movement,
        EncounterLogKind.Delayed => EncounterLogCategory.Movement,
        _ => EncounterLogCategory.System,
    };
}
