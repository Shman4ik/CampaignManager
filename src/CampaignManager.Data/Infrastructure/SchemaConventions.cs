using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Infrastructure;

/// <summary>Общие правила схемы <c>cm</c>, которые не выписываются руками на каждой колонке.</summary>
public static class SchemaConventions
{
    /// <summary>
    /// Перечисления — текстом, имя члена C# (SCHEMA, правило 5), и CHECK со списком значений,
    /// сгенерированным из самого enum. Обходит всю модель, поэтому новая колонка-enum получает
    /// и текст, и CHECK без единой строки конфигурации. Массив enum'ов (<c>eras text[]</c>) получает
    /// <c>eras &lt;@ ARRAY[…]</c>. Новый член enum'а меняет CHECK — это видно в следующей миграции.
    /// </summary>
    public static void MapEnumsAsCheckedText(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is not { } table)
            {
                continue;
            }

            var storeObject = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var property in entity.GetDeclaredProperties())
            {
                if (EnumType(property.ClrType) is { } enumType)
                {
                    property.SetProviderClrType(typeof(string));
                    AddCheck(entity, table, property.GetColumnName(storeObject)!, enumType, "{0} IN ({1})");
                }
                else if (property.GetElementType() is { } element && EnumType(element.ClrType) is { } elementEnum)
                {
                    element.SetProviderClrType(typeof(string));
                    AddCheck(entity, table, property.GetColumnName(storeObject)!, elementEnum, "{0} <@ ARRAY[{1}]");
                }
            }
        }
    }

    /// <summary>
    /// Значение по умолчанию в DDL — для сырого SQL (перенос, ручные правки), но не для EF: тот всегда
    /// пишет значение из объекта. Иначе EF считал бы «нулевое» значение незаданным и подставлял
    /// default базы — громкость 0 превращалась бы в 100.
    /// </summary>
    public static PropertyBuilder<T> HasDbDefault<T>(this PropertyBuilder<T> property, T value) =>
        property.HasDefaultValue(value).ValueGeneratedNever();

    /// <inheritdoc cref="HasDbDefault{T}(PropertyBuilder{T}, T)"/>
    public static PropertyBuilder<T> HasDbDefaultSql<T>(this PropertyBuilder<T> property, string sql) =>
        property.HasDefaultValueSql(sql).ValueGeneratedNever();

    /// <inheritdoc cref="HasDbDefault{T}(PropertyBuilder{T}, T)"/>
    public static PrimitiveCollectionBuilder<T> HasDbDefaultSql<T>(this PrimitiveCollectionBuilder<T> collection, string sql) =>
        collection.HasDefaultValueSql(sql).ValueGeneratedNever();

    /// <summary>
    /// <c>created_at</c>/<c>updated_at</c> у всех <see cref="ICreatedAt"/>/<see cref="IUpdatedAt"/>:
    /// <c>timestamptz not null default now()</c>; значения ставит <see cref="TimestampsInterceptor"/>.
    /// </summary>
    public static void MapTimestamps(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Явная реализация интерфейса (campaign_members.joined_at) свойства CreatedAt не даёт —
            // её колонку настраивает конфигурация сущности.
            foreach (var name in new[] { nameof(ICreatedAt.CreatedAt), nameof(IUpdatedAt.UpdatedAt) })
            {
                if (entity.FindProperty(name) is { } property)
                {
                    property.SetDefaultValueSql("now()");
                    property.ValueGenerated = ValueGenerated.Never;
                }
            }
        }
    }

    /// <summary>Пустой массив по умолчанию: <c>text[] not null default '{}'</c>.</summary>
    public const string EmptyArray = "'{}'";

    /// <summary>Обе эпохи: <c>eras text[] not null default '{Classic,Modern}'</c>.</summary>
    public const string AllEras = "'{Classic,Modern}'";

    private static Type? EnumType(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is { IsEnum: true } enumType ? enumType : null;

    private static void AddCheck(IMutableEntityType entity, string table, string column, Type enumType, string format)
    {
        var values = string.Join(", ", Enum.GetNames(enumType).Select(name => $"'{name}'"));
        entity.AddCheckConstraint($"ck_{table}_{column}", string.Format(null, format, column, values));
    }
}
