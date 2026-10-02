using System.Text.Json.Serialization;
using CampaignManager.Core.Documents;

namespace CampaignManager.Core.Encounters;

/// <summary>
/// Состояние сцены — документ <c>encounters.state</c> (SCHEMA, «Документы»): общее для боя и погони ядро (T2.6a).
/// Участники со снимком чисел, очередь по id активного, раунд, типизированный журнал, предложенный результат
/// (<see cref="Pending"/>) и очередь записей в листы (<see cref="SheetWrites"/>). Специфичную часть боя и погони
/// добавляют T2.6b/c своими свойствами; неизвестное этой версии сохраняется через <see cref="DocumentPart.Extra"/>.
/// <para>
/// Меняет документ только <see cref="EncounterEngine"/> и <see cref="EncounterQueue"/>: в v1 бой правил участников
/// ещё при разрешении атаки и прямо в разметке (AUDIT, «Сцены → Два движка»).
/// </para>
/// </summary>
public sealed record EncounterState : DocumentPart
{
    /// <summary>Текущая версия документа (<c>encounters.state_version</c>).</summary>
    public const int CurrentVersion = 1;

    /// <summary>Раунд; 0 — сцена ещё не начата (расстановка).</summary>
    public int Round { get; set; }

    /// <summary>Чей ход — по id участника, а не по индексу: в v1 индекс сбивался при удалении и выбывании (F-C04, F-P03, F-P04).</summary>
    public Guid? ActiveParticipantId { get; set; }

    /// <summary>Все участники сцены в порядке добавления. Порядок хода — <see cref="TurnOrder"/>.</summary>
    public List<EncounterParticipant> Participants { get; set; } = [];

    /// <summary>
    /// Порядок ходов <b>этого</b> раунда (id участников). Строит <see cref="EncounterQueue"/> в начале раунда; отложенный
    /// ход переставляет id только здесь, и следующий раунд снова идёт по порядку (в v1 перестановка оставалась навсегда, F-P05).
    /// </summary>
    public List<Guid> TurnOrder { get; set; } = [];

    public List<EncounterLogEntry> Log { get; set; } = [];

    /// <summary>
    /// Предложенный, но не применённый результат — предпросмотр. «Применить» — <see cref="EncounterEngine.Apply"/>,
    /// «Отменить» — выбросить его. Лежит в документе, поэтому переживает перезагрузку вкладки.
    /// </summary>
    public EncounterResolution? Pending { get; set; }

    /// <summary>
    /// Применённые эффекты, которые ещё не записаны в листы (через API листа, с его версиями). Очередь в документе:
    /// перезагрузка или обрыв связи посреди записи не теряют урон, а дописывают его.
    /// </summary>
    public List<SheetWrite> SheetWrites { get; set; } = [];

    [JsonIgnore]
    public EncounterParticipant? Active =>
        ActiveParticipantId is { } id ? Participants.FirstOrDefault(p => p.Id == id) : null;

    public EncounterParticipant? Find(Guid participantId) => Participants.FirstOrDefault(p => p.Id == participantId);
}

/// <summary>Сторона участника сцены. Шальная пуля ищет союзника по стороне (T2.6b), погоня — жертв и преследователей (T2.6c).</summary>
public enum EncounterSide
{
    Investigators,
    Enemies,
    Neutral,
}

/// <summary>Откуда участник: лист (сыщик, НПС) или тварь бестиария. От вида зависит, есть ли рассудок и Удача.</summary>
public enum ParticipantKind
{
    /// <summary>Сыщик игрока — лист кампании.</summary>
    Investigator,

    /// <summary>НПС — тоже лист (библиотека, кампания, состав сценария).</summary>
    Npc,

    /// <summary>Тварь бестиария или сценария — статблок; рассудка и Удачи нет.</summary>
    Creature,
}

/// <summary>
/// Участник: ссылка на источник плюс снимок чисел. <b>Id участника ≠ id листа</b>: тварей одного вида бывает
/// несколько («#2»), а захват, удаление и «чей ход» в v1 путались, когда id совпадали. Один лист в сцене — один раз
/// (<see cref="EncounterEngine.Add"/>).
/// <para>
/// Числа — снимок на момент добавления; у листа истина — сам лист: после записи эффекта в лист снимок обновляется
/// из него (<see cref="EncounterParticipants.Refresh"/>), страница при открытии перечитывает листы участников.
/// </para>
/// </summary>
public sealed record EncounterParticipant : DocumentPart
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public ParticipantKind Kind { get; set; }

    /// <summary>Лист сыщика или НПС; эффекты участника пишутся в него.</summary>
    public Guid? SourceCharacterId { get; set; }

    /// <summary>Тварь бестиария (по ней — привыкание и имя вида).</summary>
    public Guid? SourceCreatureId { get; set; }

    /// <summary>Имя в сцене: «Глубоководный #2».</summary>
    public string Name { get; set; } = "";

    /// <summary>Имя источника без номера — вид твари для привыкания, имя листа.</summary>
    public string SourceName { get; set; } = "";

    public EncounterSide Side { get; set; }

    /// <summary>Выбыл из очереди (без сознания, сбежал, спрятался), но остаётся в сцене; ход его пропускает.</summary>
    public bool IsOut { get; set; }

    /// <summary>Порядок хода: ЛВК (стр. 110) или инициатива твари; бой с огнестрелом поправляет его сам (T2.6b).</summary>
    public int Initiative { get; set; }

    public ParticipantStats Stats { get; set; } = new();

    public int HitPoints { get; set; }

    public int MaxHitPoints { get; set; }

    public int MagicPoints { get; set; }

    public int MaxMagicPoints { get; set; }

    /// <summary>Рассудок; null — у твари.</summary>
    public int? Sanity { get; set; }

    public int? MaxSanity { get; set; }

    /// <summary>Удача; null — у твари (Удачи у чудовищ нет).</summary>
    public int? Luck { get; set; }

    public bool MajorWound { get; set; }

    public bool Unconscious { get; set; }

    public bool Dying { get; set; }

    /// <summary>Потеря рассудка при встрече с тварью «успех/провал» — по ней привыкание у сыщиков.</summary>
    public string? SanityLoss { get; set; }

    /// <summary>Заметка Хранителя к участнику (кто это, где стоит).</summary>
    public string? Note { get; set; }

    [JsonIgnore]
    public bool HasSheet => SourceCharacterId is not null;
}

/// <summary>Снимок характеристик и производных участника: то, что читают правила сцены (атака, манёвр, погоня).</summary>
public sealed record ParticipantStats : DocumentPart
{
    public int Str { get; set; }
    public int Con { get; set; }
    public int Siz { get; set; }
    public int Dex { get; set; }
    public int Int { get; set; }
    public int Pow { get; set; }

    public int Build { get; set; }

    /// <summary>Бонус к урону строкой книги: «+1D4», «0», «-1».</summary>
    public string DamageBonus { get; set; } = "0";

    public int Move { get; set; }

    public int Dodge { get; set; }

    public int Armor { get; set; }
}

/// <summary>
/// Результат действия до «Применить»: что случилось (строки для журнала) и что изменится (эффекты). Резолв правил
/// возвращает его и ничего не меняет сам — так «Отменить» действительно ничего не оставляет (в v1 атака успевала
/// потратить патрон, ход и защиту цели, F-C02).
/// </summary>
public sealed record EncounterResolution : DocumentPart
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public EncounterLogKind Kind { get; set; } = EncounterLogKind.Effect;

    /// <summary>Кто действует; null — Хранитель (ручной эффект, событие сцены).</summary>
    public Guid? ActorId { get; set; }

    /// <summary>Заголовок предпросмотра и записи журнала: «Урон: Артур, Глубоководный #2».</summary>
    public string Title { get; set; } = "";

    /// <summary>Подробности: броски, уровни, расчёт — каждая строка как есть уходит в журнал.</summary>
    public List<string> Lines { get; set; } = [];

    public List<EncounterEffect> Effects { get; set; } = [];
}

/// <summary>
/// Что меняет эффект. В JSON — имя члена: члены можно переставлять, переименовывать нельзя. Новые виды (патроны,
/// перемещение, Комплекция транспорта, преграда) добавляют T2.6b/c — и ветку в <see cref="EncounterEngine"/>, если
/// эффект меняет участника, и в <see cref="EncounterSheetEffects"/>, если он пишется в лист.
/// </summary>
public enum EncounterEffectKind
{
    /// <summary>Урон одной атакой (<see cref="EncounterEffect.Amount"/> ≥ 0): серьёзная рана и сознание — <c>WoundRules</c>.</summary>
    Damage,

    /// <summary>Возврат ПЗ (не выше максимума).</summary>
    Heal,

    /// <summary>ПМ: отрицательное — трата, положительное — возврат.</summary>
    MagicPoints,

    /// <summary>Потеря рассудка одной причиной (≥ 0); с тварью — через привыкание к её виду.</summary>
    SanityLoss,

    /// <summary>МОЩ навсегда (стр. 177: трата МОЩ на заклинание): отрицательное — трата.</summary>
    Power,

    /// <summary>Выбыл из очереди (<see cref="EncounterEffect.Flag"/> = true) или вернулся (false).</summary>
    Out,
}

/// <summary>Один эффект на одного участника. Плоская запись с видом, а не иерархия типов: новый вид — член enum'а.</summary>
public sealed record EncounterEffect : DocumentPart
{
    public EncounterEffectKind Kind { get; set; }

    public Guid ParticipantId { get; set; }

    public int Amount { get; set; }

    public bool Flag { get; set; }

    /// <summary>Как получено число: «1d6 → 4», «вписано». Только для журнала.</summary>
    public string? Detail { get; set; }

    /// <summary>Потеря рассудка от твари: вид для привыкания (стр. 167) — имя без номера.</summary>
    public string? CreatureName { get; set; }

    public Guid? CreatureId { get; set; }

    /// <summary>Запись потери твари «0/1d6» — из неё предел привыкания.</summary>
    public string? SanityLossFormula { get; set; }
}

/// <summary>
/// Эффекты одного участника, которые надо записать в его лист. Пишет UI через API листа (T2.3) с <c>If-Match</c>:
/// эффект — приращение, поэтому при чужой правке (409) лист перечитывается и эффект ложится поверх неё, а не затирает.
/// </summary>
public sealed record SheetWrite : DocumentPart
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ParticipantId { get; set; }

    public Guid CharacterId { get; set; }

    /// <summary>Чей это результат — для записи журнала об итоге.</summary>
    public string Title { get; set; } = "";

    public List<EncounterEffect> Effects { get; set; } = [];

    /// <summary>Последняя ошибка записи (нет связи, нет прав); null — ещё не пробовали или пишем.</summary>
    public string? Error { get; set; }

    /// <summary>Повтор не поможет (листа нет, нет прав): сама запись больше не пробует — решает Хранитель.</summary>
    public bool Blocked { get; set; }
}

/// <summary>
/// Запись журнала сцены. Вид — enum именем (в v1 журнал боя хранил число, и члены нельзя было переставлять); подпись,
/// значок и цвет — по виду из одной таблицы (<see cref="EncounterText"/>), а не три словаря в разметке, как у погони v1.
/// </summary>
public sealed record EncounterLogEntry : DocumentPart
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Round { get; set; }

    public EncounterLogKind Kind { get; set; }

    public Guid? ActorId { get; set; }

    public string Text { get; set; } = "";

    public List<string> Lines { get; set; } = [];

    public DateTimeOffset At { get; set; }
}

/// <summary>
/// Вид записи журнала. Новые виды (атака, манёвр, заклинание, перемещение, помеха…) добавляют T2.6b/c — и строку в
/// <see cref="EncounterText.Category(EncounterLogKind)"/>, тест проверяет, что подпись есть у каждого члена.
/// </summary>
public enum EncounterLogKind
{
    /// <summary>Сцена начата: первый раунд.</summary>
    Started,

    Joined,

    Left,

    Round,

    /// <summary>Отложенный ход (стр. 110/132): только в этом раунде.</summary>
    Delayed,

    /// <summary>Эффект Хранителя: урон, рассудок, ПМ, МОЩ руками.</summary>
    Effect,

    /// <summary>Итог записи в лист (или ошибка).</summary>
    Sheet,

    Note,

    Finished,
}

/// <summary>Категория записи — цвет в журнале: движение, препятствие, насилие, рассудок, служебное (знание v1 погони).</summary>
public enum EncounterLogCategory
{
    System,
    Movement,
    Obstacle,
    Violence,
    Sanity,
    Magic,
}
