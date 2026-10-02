using System.Text;

namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Код книжной записи справочника (<c>code</c> в SCHEMA) по транслиту имени: префикс вида и транслит —
/// «Антиквар» → <c>occupation.antikvar</c>. <b>Для навыков не используется</b>: их
/// коды — английские названия книги из явной таблицы <see cref="SkillCodes"/> (решение владельца
/// 2026-10-02). Для остальных справочников (профессии, оружие…) правило кода ещё не решено.
/// </summary>
public static class CatalogCode
{
    public const string OccupationPrefix = "occupation";

    /// <summary>Код записи: <c>{prefix}.{транслит}</c>; транслит — латиница, цифры и дефисы.</summary>
    public static string For(string prefix, string name) => $"{prefix}.{Transliterate(name)}";

    /// <summary>
    /// Нижний регистр, кириллица — латиницей (х → kh, щ → shch, ь и ъ пропадают), всё прочее — дефис,
    /// подряд идущие дефисы схлопываются, по краям их нет.
    /// </summary>
    public static string Transliterate(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        foreach (var ch in name.Trim().ToLowerInvariant())
        {
            var latin = ch switch
            {
                'а' => "a", 'б' => "b", 'в' => "v", 'г' => "g", 'д' => "d", 'е' => "e", 'ё' => "e",
                'ж' => "zh", 'з' => "z", 'и' => "i", 'й' => "y", 'к' => "k", 'л' => "l", 'м' => "m",
                'н' => "n", 'о' => "o", 'п' => "p", 'р' => "r", 'с' => "s", 'т' => "t", 'у' => "u",
                'ф' => "f", 'х' => "kh", 'ц' => "ts", 'ч' => "ch", 'ш' => "sh", 'щ' => "shch",
                'ъ' or 'ь' => "", 'ы' => "y", 'э' => "e", 'ю' => "yu", 'я' => "ya",
                _ when ch is >= 'a' and <= 'z' or >= '0' and <= '9' => ch.ToString(),
                _ => "-",
            };

            if (latin == "-" && (builder.Length == 0 || builder[^1] == '-'))
                continue;
            builder.Append(latin);
        }

        while (builder.Length > 0 && builder[^1] == '-')
            builder.Length--;

        return builder.ToString();
    }
}
