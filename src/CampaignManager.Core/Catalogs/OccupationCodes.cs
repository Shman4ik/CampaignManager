namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Коды книжных профессий (<c>occupations.code</c>): английское название профессии в книге Хранителя 7e
/// (раздел «Примеры профессий») или в книге сыщика (Investigator Handbook) — <c>occupation.antiquarian</c>.
/// Правило и поиск — <see cref="CatalogCodeTable"/>.
/// </summary>
public static class OccupationCodes
{
    public const string Prefix = "occupation.";

    /// <summary>
    /// 31 профессия справочника v1 на 2026-10-02, все книжные, без «Хакера» — он только современной эпохи, которую
    /// владелец убрал 2026-10-02. Порядок — по алфавиту русских имён.
    /// </summary>
    public static CatalogCodeTable Table { get; } = new(Prefix,
    [
        new("occupation.antiquarian", "Антиквар"),
        new("occupation.entertainer", "Артист"),
        new("occupation.archaeologist", "Археолог"), // только в книге сыщика
        new("occupation.librarian", "Библиотекарь"),
        new("occupation.drifter", "Бродяга"),
        new("occupation.accountant", "Бухгалтер"), // только в книге сыщика
        new("occupation.military-officer", "Военный (офицер)"),
        new("occupation.doctor-of-medicine", "Врач"),
        new("occupation.police-detective", "Детектив полиции"),
        new("occupation.tribe-member", "Дикарь"),
        new("occupation.dilettante", "Дилетант"),
        new("occupation.journalist", "Журналист"),
        new("occupation.engineer", "Инженер"),
        new("occupation.pilot", "Лётчик"),
        new("occupation.mechanic", "Механик"), // книга сыщика: «Mechanic (and Skilled Trades)»
        new("occupation.missionary", "Миссионер"),
        new("occupation.musician", "Музыкант"),
        new("occupation.parapsychologist", "Парапсихолог"),
        new("occupation.author", "Писатель"),
        new("occupation.police-officer", "Полицейский"),
        new("occupation.criminal", "Преступник"),
        new("occupation.professor", "Профессор"),
        new("occupation.clergy", "Священник"),
        new("occupation.soldier", "Солдат"),
        new("occupation.athlete", "Спортсмен"),
        new("occupation.zealot", "Фанатик"),
        new("occupation.farmer", "Фермер"),
        new("occupation.artist", "Художник"),
        new("occupation.private-investigator", "Частный сыщик"),
        new("occupation.lawyer", "Юрист"),
    ]);

    /// <summary>Код книжной профессии по имени; null — самодельная.</summary>
    public static string? FromName(string name) => Table.FromName(name);
}
