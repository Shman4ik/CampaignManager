using Microsoft.JSInterop;

namespace CampaignManager.UI.Platform;

/// <summary>
/// <c>localStorage</c> браузера — для несохранённых черновиков (README, «Принципы»: черновик правки живёт
/// в браузере, а не в <c>[PersistentState]</c> circuit). Зовёт встроенные <c>localStorage.*</c> напрямую,
/// своих глобальных скриптов нет. Хранилище бывает недоступно (приватный режим Safari, переполнение) —
/// тогда черновик просто не сохраняется, страница работает дальше.
/// </summary>
public sealed class BrowserStorage(IJSRuntime js)
{
    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task SetAsync(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            // Нет места или запрещено: черновик не переживёт перезагрузку, но правка на экране цела.
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (JSException)
        {
        }
    }
}
