namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Коды артефактов главы 13 книги Хранителя (<c>artifacts.code</c>, «Артефакты и инопланетные устройства», стр. 264–273):
/// английское название записи в kebab-case — <c>artifact.shining-trapezohedron</c>. Порядок — как в русском издании (по
/// алфавиту). Только названия и коды (D5): описаний книги в репозитории нет, они приходят файлом обмена.
/// Правило и поиск — <see cref="CatalogCodeTable"/>.
/// </summary>
public static class ArtifactCodes
{
    public const string Prefix = "artifact.";

    public static CatalogCodeTable Table { get; } = new(Prefix,
    [
        new("artifact.carotid-toxin", "Артериальный токсин"),
        new("artifact.bio-web-armor", "Биопаутинная броня"), // спорно: английского названия под рукой не было
        new("artifact.great-white-space", "Великое белое пространство"),
        new("artifact.star-stones-of-mnar", "Звёздные камни Мнара", ["Звёздный камень Мнара"]),
        new("artifact.elder-thing-crystal", "Кристалл Старцев"),
        new("artifact.dream-crystallizer", "Кристаллизатор снов"),
        new("artifact.lamp-of-alhazred", "Лампа Альхазреда"),
        new("artifact.time-communicator", "Межвременной коммуникатор"), // спорно: по смыслу
        new("artifact.brain-cylinder", "Мозговой цилиндр"),
        new("artifact.lightning-gun", "Молниемёт"),
        new("artifact.plutonian-drug", "Плутонский наркотик"),
        new("artifact.black-lotus-powder", "Порошок чёрного лотоса"),
        new("artifact.tabula-rasa-device", "Прибор «Табула раса»"),
        new("artifact.mist-projector", "Распылитель тумана"),
        new("artifact.seismic-drill", "Сейсмический бур"), // спорно: по смыслу
        new("artifact.shining-trapezohedron", "Сияющий трапецоэдр"),
        new("artifact.stasis-cube", "Стазисный куб"),
        new("artifact.glass-from-leng", "Стекло с Ленга"),
        new("artifact.serum-of-obedience", "Сыворотка подчинения"), // спорно: по смыслу
        new("artifact.electric-gun", "Электропушка"),
    ]);

    /// <summary>Код книжной записи по имени; null — самодельная.</summary>
    public static string? FromName(string name) => Table.FromName(name);
}
