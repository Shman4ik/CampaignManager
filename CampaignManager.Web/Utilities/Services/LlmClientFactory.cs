using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace CampaignManager.Web.Utilities.Services;

public sealed class LlmClientFactory(
    IOptions<LlmOptions> options,
    ILogger<LlmClientFactory> logger)
{
    public bool IsEnabled => options.Value.Enabled;

    public string Provider => options.Value.Provider;

    public int TimeoutSeconds => options.Value.TimeoutSeconds;

    public IChatClient CreateChatClient()
    {
        var opts = options.Value;

        var (endpoint, model, apiKey) = opts.IsNvidia
            ? (new Uri(opts.NvidiaEndpoint + "/v1"), opts.NvidiaModel, opts.NvidiaApiKey)
            : (new Uri(opts.OllamaEndpoint + "/v1"), opts.OllamaModel, "ollama");

        if (opts.IsNvidia && string.IsNullOrWhiteSpace(opts.NvidiaApiKey))
            throw new InvalidOperationException("NVIDIA NIM API ключ не настроен");

        logger.LogDebug("Creating LLM chat client: provider={Provider}, model={Model}", opts.Provider, model);

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = endpoint,
                NetworkTimeout = TimeSpan.FromSeconds(opts.TimeoutSeconds)
            });

        return openAiClient
            .GetChatClient(model)
            .AsIChatClient();
    }
}
