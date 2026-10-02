using System.Text.Json;
using CampaignManager.Core.Identity;

namespace CampaignManager.Contracts.Profile;

/// <summary>Всё, что показывает личный кабинет, — одним запросом.</summary>
/// <param name="Email">Своя почта: ею входят и по ней решается доступ. Другим её не показывают нигде.</param>
/// <param name="CampaignCount">Кампании, где я участник (Хранитель или игрок).</param>
/// <param name="CharacterCount">Мои сыщики, кроме архивных.</param>
/// <param name="AliasCount">В скольких кампаниях у меня своё имя (псевдоним): столько затронет галочка «заменить и в кампаниях».</param>
/// <param name="LatestApplication">Последняя заявка на Хранителя; <c>null</c> — не подавал.</param>
/// <param name="CanApply">Можно подать заявку: я игрок и заявки на рассмотрении нет.</param>
public sealed record ProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    int CampaignCount,
    int CharacterCount,
    int AliasCount,
    MyKeeperApplicationDto? LatestApplication,
    bool CanApply);

/// <summary>Своя заявка — без того, кто её рассмотрел: игроку это ни к чему.</summary>
public sealed record MyKeeperApplicationDto(
    Guid Id,
    KeeperApplicationStatus Status,
    string Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment);

/// <param name="DisplayName">Новое имя: видно в меню, в кампаниях без своего имени и администратору.</param>
/// <param name="ReplaceAliases">
/// Заменить и имена в кампаниях: псевдонимы сбрасываются, и во всех кампаниях я снова под именем профиля.
/// По умолчанию выключено — имена в кампаниях нарочно разные («Дима» в одной, полное имя в другой).
/// </param>
public sealed record UpdateDisplayNameRequest(string DisplayName, bool ReplaceAliases = false);

/// <param name="Message">Пара слов администратору; можно пусто.</param>
public sealed record SubmitKeeperApplicationRequest(string? Message);

/// <summary>Мои настройки: ключ (<see cref="PreferenceKeys"/>) → значение JSON.</summary>
public sealed record PreferencesDto(IReadOnlyDictionary<string, JsonElement> Values);
