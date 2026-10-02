namespace CampaignManager.Contracts.Profile;

/// <summary>
/// Личный кабинет: только свои данные. Сервис берёт пользователя из сессии, чужой id не принимает —
/// поэтому в адресах его нет.
/// </summary>
public static class ProfileRoutes
{
    /// <summary><c>GET</c> — кабинет одним запросом: <see cref="ProfileDto"/>.</summary>
    public const string Profile = ApiRoutes.Prefix + "/profile";

    /// <summary><c>PUT</c> <see cref="UpdateDisplayNameRequest"/> — сменить отображаемое имя.</summary>
    public const string DisplayName = Profile + "/name";

    /// <summary><c>POST</c> <see cref="SubmitKeeperApplicationRequest"/> — подать заявку на роль Хранителя.</summary>
    public const string KeeperApplication = Profile + "/keeper-application";

    /// <summary><c>GET</c> — все мои настройки: ключ → значение JSON.</summary>
    public const string Preferences = Profile + "/preferences";

    /// <summary><c>PUT</c> значение JSON — записать одну настройку; <c>DELETE</c> — вернуть значение по умолчанию.</summary>
    public const string PreferencePattern = Preferences + "/{key}";

    public static string Preference(string key) => $"{Preferences}/{Uri.EscapeDataString(key)}";
}

/// <summary>Пределы полей кабинета — одни для формы, сервиса и текста ошибки.</summary>
public static class ProfileLimits
{
    /// <summary>Как в v1 (<c>ProfileService.MaxDisplayNameLength</c>).</summary>
    public const int DisplayNameLength = 64;

    /// <summary>Как CHECK <c>ck_keeper_applications_message_length</c> в базе.</summary>
    public const int ApplicationMessageLength = 1000;

    public const int ReviewCommentLength = 1000;

    public const int PreferenceKeyLength = 64;

    /// <summary>Значение настройки в виде JSON-текста. Настройки — флажки и короткие списки, не документы.</summary>
    public const int PreferenceValueLength = 8 * 1024;
}

/// <summary>
/// Ключи настроек (<c>cm.user_preferences</c>, строка на ключ). Ключ — <c>область.имя</c> латиницей; новые
/// ключи заводит модуль, которому настройка нужна. Значения, перенесённые из v1, — JSON-строки
/// (<c>"true"</c>, а не <c>true</c>): v1 хранил словарь строк, читатель обязан принимать обе формы.
/// </summary>
public static class PreferenceKeys
{
    /// <summary>v1: помнить последнего открытого сыщика на всех устройствах (читает нижняя навигация, M2).</summary>
    public const string SyncLastCharacter = "ui.syncLastCharacter";

    /// <summary>v1: закреплённые теги фонотеки (T2.8).</summary>
    public const string MusicPinnedTags = "music.pinnedTags";

    /// <summary>Латиница, цифры, точка, дефис и подчёркивание; начинается с буквы; не длиннее <see cref="ProfileLimits.PreferenceKeyLength"/>.</summary>
    public static bool IsValid(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > ProfileLimits.PreferenceKeyLength || !char.IsAsciiLetter(key[0]))
        {
            return false;
        }

        foreach (var c in key)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
