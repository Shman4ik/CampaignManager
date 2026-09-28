using System.Text.Json;

namespace CampaignManager.Web.Utilities.Services;

/// <summary>
///     Полная копия записи — например, каталожной, чтобы править её в модалке, не трогая экземпляр
///     из общего кэша, пока Хранитель не нажал «Сохранить».
///     <para>
///         Копия снимается круговым прогоном через System.Text.Json с настройками по умолчанию —
///         теми же, с какими Npgsql пишет и читает jsonb (<c>EnableDynamicJson</c> без своих опций).
///         Поэтому новое свойство модели попадает в копию само: раньше страницы перечисляли поля
///         руками, и забытое поле редактирование молча обнуляло (так чуть не потерялся
///         <c>Weapon.IsRare</c>). Копия глубокая: списки и разобранные jsonb-блоки не делятся
///         с оригиналом. <c>Id</c>, <c>CreatedAt</c> и <c>LastUpdated</c> переносятся как есть.
///     </para>
///     <para>
///         Не копируются свойства под <c>[JsonIgnore]</c> и свойства без сеттера — то есть
///         вычисляемые, которые и так пересчитываются из остальных.
///     </para>
/// </summary>
public static class EntityCloner
{
    public static T Clone<T>(T source) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);

        var json = JsonSerializer.SerializeToUtf8Bytes(source, JsonSerializerOptions.Default);
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Default)
               ?? throw new InvalidOperationException($"Не удалось скопировать {typeof(T).Name}.");
    }
}
