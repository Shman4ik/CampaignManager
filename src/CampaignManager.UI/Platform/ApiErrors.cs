using System.Net;

namespace CampaignManager.UI.Platform;

/// <summary>
/// Текст отказа API для пользователя. Клиенты <c>ApiClient</c> кладут в сообщение исключения текст
/// ProblemDetails («Название кампании не может быть пустым.»), его и показываем; без ответа — про связь,
/// на сбой сервера — общая фраза вместо внутренностей.
/// </summary>
public static class ApiErrors
{
    public static string Describe(HttpRequestException error) => error.StatusCode switch
    {
        null => "Нет связи с сервером. Повторите, когда связь вернётся.",
        HttpStatusCode.Unauthorized => "Сессия входа закончилась. Войдите снова — несохранённое останется на экране.",
        >= HttpStatusCode.InternalServerError => "Сервер не смог выполнить запрос. Попробуйте ещё раз; если повторится — сообщите.",
        _ => error.Message,
    };
}
