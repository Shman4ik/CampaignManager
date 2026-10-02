using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CampaignManager.Data.Files;

/// <summary>
/// Кто ссылается на <c>cm.files</c>. Список берётся из модели EF, а не пишется руками: новая
/// таблица с <c>*_file_id</c> сама попадает в поиск сирот, и забытый в запросе FK не сделает
/// живую картинку «сиротой», которую админ удалит.
/// </summary>
public static class FileReferences
{
    /// <summary>Пары «таблица — колонка» всех FK на <see cref="StoredFile"/>.</summary>
    public static IReadOnlyList<(string Table, string Column)> All(IModel model) =>
    [
        .. model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(StoredFile) && !fk.DeclaringEntityType.IsOwned())
            .Select(fk =>
            {
                var entity = fk.DeclaringEntityType;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                return ($"{entity.GetSchema() ?? CmDbContext.Schema}.{table.Name}",
                    fk.Properties.Single().GetColumnName(table)!);
            })
            .Distinct()
            .OrderBy(reference => reference.Item1, StringComparer.Ordinal)
            .ThenBy(reference => reference.Item2, StringComparer.Ordinal),
    ];

    /// <summary>
    /// Условие «на файл <paramref name="alias"/> никто не ссылается» — anti-join из SCHEMA.md.
    /// Имена — из модели, не из ввода, поэтому склеиваются в SQL без параметров.
    /// </summary>
    public static string NotReferencedSql(IModel model, string alias) =>
        string.Join("\n  and ", All(model).Select(reference =>
            $"not exists (select 1 from {reference.Table} r where r.{reference.Column} = {alias}.id)"));
}
