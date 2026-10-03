using System.Text.Json;

namespace CampaignManager.Contracts.Profile;

/// <summary>
/// Личный кабинет текущего пользователя. Ошибки — <see cref="HttpRequestException"/> с текстом ProblemDetails:
/// 400 — форма (пустое имя, почта в имени, длинный текст, плохой ключ настройки), 409 — заявку подать нельзя
/// (уже Хранитель или заявка на рассмотрении).
/// </summary>
public interface IProfileApi
{
    Task<ProfileDto> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<ProfileDto> UpdateDisplayNameAsync(UpdateDisplayNameRequest request, CancellationToken cancellationToken = default);

    Task<ProfileDto> SubmitKeeperApplicationAsync(SubmitKeeperApplicationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Переписать текст заявки, пока она на рассмотрении; рассмотренной (или без заявки) — 409.</summary>
    Task<ProfileDto> UpdateKeeperApplicationAsync(SubmitKeeperApplicationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Отозвать заявку на рассмотрении: строка удаляется, подать новую можно сразу. Рассмотренную отозвать нельзя — 409.</summary>
    Task<ProfileDto> WithdrawKeeperApplicationAsync(CancellationToken cancellationToken = default);

    Task<PreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken = default);

    Task SetPreferenceAsync(string key, JsonElement value, CancellationToken cancellationToken = default);

    Task RemovePreferenceAsync(string key, CancellationToken cancellationToken = default);
}
