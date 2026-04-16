using System.Collections.Frozen;
using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
/// Статический каталог тегов навыков CoC 7e для взвешенного выбора при генерации персонажа.
/// Ключ — русское название навыка (как в SkillService.BuildDefaultSkillsModelAsync).
/// Навыки без записи получают OccupationTag.None → минимальный вес (шум сохраняется).
/// </summary>
public static class SkillTagCatalog
{
    private static readonly FrozenDictionary<string, OccupationTag> Tags = new Dictionary<string, OccupationTag>
    {
        // === Решение Проблем ===
        ["Взлом"]              = OccupationTag.Criminal | OccupationTag.Stealth,
        ["Выживание"]          = OccupationTag.Outdoor | OccupationTag.Physical,
        ["Ловкость рук"]       = OccupationTag.Criminal | OccupationTag.Stealth,
        ["Механика"]           = OccupationTag.Technical,
        ["Ориентирование"]     = OccupationTag.Outdoor | OccupationTag.Investigative,
        ["Скрытность"]         = OccupationTag.Stealth | OccupationTag.Criminal,
        ["Чтение следов"]      = OccupationTag.Outdoor | OccupationTag.Investigative,
        ["Электрика"]          = OccupationTag.Technical,

        // === Социальные ===
        ["Антропология"]       = OccupationTag.Academic | OccupationTag.Social,
        ["Запугивание"]        = OccupationTag.Social | OccupationTag.Criminal,
        ["Красноречие"]        = OccupationTag.Social | OccupationTag.Artistic,
        ["Маскировка"]         = OccupationTag.Stealth | OccupationTag.Criminal | OccupationTag.Social,
        ["Обаяние"]            = OccupationTag.Social,
        ["Психология"]         = OccupationTag.Investigative | OccupationTag.Social,
        ["Убеждение"]          = OccupationTag.Social,
        ["Языки (родной)"]     = OccupationTag.Academic | OccupationTag.Language,
        ["Языки (иностр.)"]   = OccupationTag.Academic | OccupationTag.Language,

        // === Сбор Информации ===
        ["Внимание"]           = OccupationTag.Investigative,
        ["Работа в библиотеке"] = OccupationTag.Academic | OccupationTag.Investigative | OccupationTag.Scholarly,
        ["Слух"]               = OccupationTag.Investigative,

        // === Лечение ===
        ["Медицина"]           = OccupationTag.Medical | OccupationTag.Academic,
        ["Первая помощь"]      = OccupationTag.Medical,
        ["Психоанализ"]        = OccupationTag.Medical | OccupationTag.Social,

        // === Знания ===
        ["Археология"]         = OccupationTag.Academic | OccupationTag.Investigative | OccupationTag.Scholarly,
        ["Бухгалтерское дело"] = OccupationTag.Academic,
        ["Естествознание"]     = OccupationTag.Academic | OccupationTag.Outdoor,
        ["Искусство/ремесло"]  = OccupationTag.Artistic | OccupationTag.Technical,
        ["История"]            = OccupationTag.Academic | OccupationTag.Scholarly,
        ["Наука"]              = OccupationTag.Academic | OccupationTag.Scholarly,
        ["Оккультизм"]         = OccupationTag.Occult | OccupationTag.Academic,
        ["Оценка"]             = OccupationTag.Investigative | OccupationTag.Academic,
        ["Юриспруденция"]      = OccupationTag.Academic | OccupationTag.Social,

        // === Специальные ===
        ["Мифы Ктулху"]        = OccupationTag.Occult,
        ["Средства"]           = OccupationTag.None,

        // === Сражение (общее) ===
        ["Ближний бой (драка)"] = OccupationTag.Combat | OccupationTag.Physical,
        ["Метание"]            = OccupationTag.Combat | OccupationTag.Physical,
        ["Уклонение"]          = OccupationTag.Combat | OccupationTag.Physical,

        // === Сражение (Огнестрельное) ===
        ["Автомат"]            = OccupationTag.Combat,
        ["Стрельба (винт./дроб.)"] = OccupationTag.Combat,
        ["Стрельба (пистолет)"] = OccupationTag.Combat,

        // === Действия ===
        ["Верховая езда"]      = OccupationTag.Outdoor | OccupationTag.Physical,
        ["Вождение"]           = OccupationTag.Technical,
        ["Лазание"]            = OccupationTag.Physical | OccupationTag.Outdoor,
        ["Пилотирование"]      = OccupationTag.Technical,
        ["Плавание"]           = OccupationTag.Physical | OccupationTag.Outdoor,
        ["Прыжки"]             = OccupationTag.Physical,
        ["Упр. тяж. машинами"] = OccupationTag.Technical,
    }.ToFrozenDictionary();

    /// <summary>
    /// Возвращает теги навыка по имени. Если навык не найден, возвращает OccupationTag.None.
    /// </summary>
    public static OccupationTag GetTags(string skillName) =>
        Tags.GetValueOrDefault(skillName, OccupationTag.None);
}
