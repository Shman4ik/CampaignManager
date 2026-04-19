using System.Text.Json;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CampaignManager.Web.Components.Features.Characters.Services;

public sealed class LlmCharacterValidationService(
    LlmClientFactory llmClientFactory,
    IOptions<LlmValidationOptions> options,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<LlmCharacterValidationService> logger)
{
    private string? _rulesCache;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool IsEnabled => llmClientFactory.IsEnabled;

    public async IAsyncEnumerable<string> ValidateCharacterStreamingAsync(
        Character character,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await EnsureAuthorizedAsync();

        var chatClient = llmClientFactory.CreateChatClient();
        var rules = await LoadRulesAsync();
        var charContext = BuildCharacterContext(character);
        var skillsSummary = BuildSkillsSummary(character);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, $"""
                Ты — эксперт по настольной ролевой игре «Зов Ктулху» (Call of Cthulhu 7th Edition).
                Твоя задача — проанализировать лист персонажа и найти проблемы.

                Справочник правил:
                {rules}

                Инструкции:
                - Отвечай ТОЛЬКО на русском языке.
                - Структурируй ответ строго по трём разделам с заголовками ## как в запросе.
                - Для каждой проблемы: **навык/атрибут** → текущее значение → в чём ошибка → как должно быть.
                - Если в разделе нет проблем — пиши «Нарушений не обнаружено.»
                - НЕ возвращай JSON. Только текстовый анализ.
                """),
            new(ChatRole.User, $"""
                {charContext}

                ---

                {skillsSummary}

                ---

                Проведи анализ по трём разделам:

                ## 1. Механические ошибки
                Проверь: характеристики в допустимых диапазонах (СИЛ/ТЕЛ/ЛВК/НАР/МОЩ 15–90, РАЗ 40–90, ИНТ 40–90, ОБР 40–90 на старте), HP = (ТЕЛ+РАЗ)/10 округлить вниз, МП = МОЩ/5, Рассудок_макс = МОЩ, Уклонение = ЛВК/2, Credit Rating в диапазоне профессии, базовые значения навыков соответствуют правилам CoC 7e.

                ## 2. Навыки не соответствующие профессии
                Проанализируй навыки, отсортированные по убыванию вложенных очков. Найди навыки с вложением >20 очков, которые НЕ помечены [ПРОФ] и семантически не подходят профессии «{character.PersonalInfo.Occupation}». Для каждого такого навыка объясни почему он вызывает сомнение применительно к данной профессии и биографии.

                ## 3. Недоразвитые профессиональные навыки
                Перечисли навыки помеченные [ПРОФ], в которые вложено 0 очков (значение = базовое). Оцени насколько критично для профессии «{character.PersonalInfo.Occupation}» отсутствие инвестиций в каждый из них.
                """)
        };

        var chatOptions = new ChatOptions
        {
            Temperature = options.Value.TemperatureValidate,
            MaxOutputTokens = options.Value.MaxOutputTokensValidate,
            TopP = options.Value.TopP,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["chat_template_kwargs"] = new Dictionary<string, object?> { ["enable_thinking"] = options.Value.EnableThinking },
                ["reasoning_budget"] = options.Value.ReasoningBudget
            }
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(llmClientFactory.TimeoutSeconds));

        logger.LogInformation("Starting LLM streaming validation for character {CharacterId} via {Provider}",
            character.Id, llmClientFactory.Provider);

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions, cts.Token))
        {
            if (update.Text is { Length: > 0 } text)
            {
                yield return text;
            }
        }

        logger.LogInformation("LLM streaming validation completed for character {CharacterId}", character.Id);
    }

    private static string BuildCharacterContext(Character character)
    {
        var c = character.Characteristics;
        var d = character.DerivedAttributes;
        return $"""
            ## Персонаж: {character.PersonalInfo.Name}
            Профессия: {character.PersonalInfo.Occupation} | Возраст: {character.PersonalInfo.Age} | Пол: {character.PersonalInfo.Gender}

            Характеристики:
            СИЛ {c.Strength.Regular} | ТЕЛ {c.Constitution.Regular} | РАЗ {c.Size.Regular} | ЛВК {c.Dexterity.Regular} | НАР {c.Appearance.Regular} | ИНТ {c.Intelligence.Regular} | МОЩ {c.Power.Regular} | ОБР {c.Education.Regular}

            Производные: HP {d.HitPoints.Value}/{d.HitPoints.MaxValue} | МП {d.MagicPoints.Value}/{d.MagicPoints.MaxValue} | Рассудок {d.Sanity.Value}/{d.Sanity.MaxValue} | Удача {d.Luck.Value} | Движение {character.PersonalInfo.MoveSpeed} | Уклонение {character.PersonalInfo.Dodge} | Build {character.PersonalInfo.Build} | Бонус урона {character.PersonalInfo.DamageBonus}
            """;
    }

    private static string BuildSkillsSummary(Character character)
    {
        var occupation = Occupation.GetDefaultOccupations()
            .FirstOrDefault(o => string.Equals(o.Name, character.PersonalInfo.Occupation, StringComparison.OrdinalIgnoreCase));

        var occupationSkills = occupation?.OccupationSkills ?? [];

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Навыки (отсортированы по убыванию вложенных очков)");
        sb.AppendLine("Формат: Навык: текущее% (база X%, вложено Y) [ПРОФ — если профессиональный]");

        if (occupation is not null)
        {
            sb.AppendLine($"Credit Rating допустим для профессии: {occupation.CreditRatingMin}–{occupation.CreditRatingMax}%");
        }

        sb.AppendLine();

        var allSkills = character.Skills.SkillGroups
            .SelectMany(g => g.Skills)
            .Select(s =>
            {
                var baseVal = ParseBaseValue(s.BaseValue);
                var invested = s.Value.Regular - baseVal;
                var isOccupation = occupationSkills.Any(os =>
                    s.Name.StartsWith(os, StringComparison.OrdinalIgnoreCase) ||
                    os.StartsWith(s.Name, StringComparison.OrdinalIgnoreCase) ||
                    os.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
                return (s.Name, Base: baseVal, Current: s.Value.Regular, Invested: invested, IsOccupation: isOccupation);
            })
            .OrderByDescending(s => s.Invested)
            .ThenBy(s => s.Name)
            .ToList();

        foreach (var (name, baseVal, current, invested, isOcc) in allSkills)
        {
            var marker = isOcc ? " [ПРОФ]" : "";
            sb.AppendLine($"- {name}: {current}% (база {baseVal}%, вложено {invested}){marker}");
        }

        return sb.ToString();
    }

    private static int ParseBaseValue(string value)
    {
        return int.TryParse(value, out var result) ? result : 0;
    }

    public async Task<Character> ApplySuggestionsAsync(
        Character original,
        string editedSuggestions,
        CancellationToken ct)
    {
        await EnsureAuthorizedAsync();

        var chatClient = llmClientFactory.CreateChatClient();
        var rules = await LoadRulesAsync();
        var originalJson = JsonSerializer.Serialize(original, JsonOptions);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, $"""
                Ты — эксперт по настольной ролевой игре «Зов Ктулху» (Call of Cthulhu 7th Edition).
                Твоя задача — применить рекомендации к листу персонажа и вернуть исправленный JSON.

                Правила:
                {rules}

                КРИТИЧЕСКИЕ ИНСТРУКЦИИ:
                - Верни ТОЛЬКО валидный JSON объект персонажа. Никакого текста до или после JSON.
                - Сохрани ВСЕ поля оригинального персонажа. Не удаляй поля, группы навыков, навыки.
                - Применяй ТОЛЬКО те изменения, которые описаны в рекомендациях.
                - Поле "id" должно остаться неизменным: "{original.Id}".
                - Поле "characterType" должно остаться неизменным: {(int)original.CharacterType}.
                - Все группы навыков (skillGroups) должны быть сохранены. Не удаляй и не переименовывай группы.
                - Количество навыков в каждой группе должно быть >= исходному.
                """),
            new(ChatRole.User, $"""
                Оригинальный персонаж:
                ```json
                {originalJson}
                ```

                Рекомендации к применению:
                {editedSuggestions}

                Верни исправленный JSON персонажа.
                """)
        };

        var chatOptions = new ChatOptions
        {
            Temperature = options.Value.TemperatureApply,
            ResponseFormat = ChatResponseFormat.Json,
            TopP = options.Value.TopP
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(llmClientFactory.TimeoutSeconds));

        logger.LogInformation("Applying LLM suggestions for character {CharacterId} via {Provider}",
            original.Id, llmClientFactory.Provider);

        var sb = new System.Text.StringBuilder();
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions, cts.Token))
        {
            if (update.Text is { Length: > 0 } text)
            {
                sb.Append(text);
            }
        }

        var responseText = sb.Length > 0
            ? sb.ToString()
            : throw new InvalidOperationException("LLM вернул пустой ответ");

        Character updated;
        try
        {
            updated = JsonSerializer.Deserialize<Character>(responseText, JsonOptions)
                      ?? throw new InvalidOperationException("LLM вернул null при десериализации");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize LLM response for character {CharacterId}", original.Id);
            throw new InvalidOperationException("LLM вернул невалидный JSON. Попробуйте ещё раз.", ex);
        }

        PostValidate(original, updated);

        logger.LogInformation("LLM suggestions applied successfully for character {CharacterId}", original.Id);

        return updated;
    }

    private static void PostValidate(Character original, Character updated)
    {
        // Preserve identity fields
        updated.Id = original.Id;
        updated.CharacterType = original.CharacterType;

        // Validate skill groups integrity
        if (original.Skills.SkillGroups is { Count: > 0 })
        {
            if (updated.Skills.SkillGroups is null || updated.Skills.SkillGroups.Count < original.Skills.SkillGroups.Count)
            {
                throw new InvalidOperationException(
                    $"LLM вернул повреждённую структуру: ожидалось >= {original.Skills.SkillGroups.Count} групп навыков, " +
                    $"получено {updated.Skills.SkillGroups?.Count ?? 0}");
            }

            for (var i = 0; i < original.Skills.SkillGroups.Count; i++)
            {
                var origGroup = original.Skills.SkillGroups[i];
                var updGroup = updated.Skills.SkillGroups[i];

                if (!string.Equals(origGroup.Name, updGroup.Name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"LLM вернул повреждённую структуру: группа навыков [{i}] " +
                        $"ожидалась \"{origGroup.Name}\", получена \"{updGroup.Name}\"");
                }

                if (updGroup.Skills.Count < origGroup.Skills.Count)
                {
                    throw new InvalidOperationException(
                        $"LLM вернул повреждённую структуру: в группе \"{origGroup.Name}\" " +
                        $"ожидалось >= {origGroup.Skills.Count} навыков, получено {updGroup.Skills.Count}");
                }
            }
        }

        // Validate critical fields are not null
        if (string.IsNullOrWhiteSpace(updated.PersonalInfo.Name))
        {
            throw new InvalidOperationException("LLM вернул повреждённую структуру: имя персонажа пустое");
        }

        if (updated.Characteristics is null)
        {
            throw new InvalidOperationException("LLM вернул повреждённую структуру: характеристики отсутствуют");
        }
    }

    private async Task EnsureAuthorizedAsync()
    {
        if (!IsEnabled)
            throw new InvalidOperationException("LLM-валидация отключена");

        if (!await identityService.IsKeeper())
            throw new InvalidOperationException("Только Keeper/Administrator может использовать LLM-валидацию");
    }

    private async Task<string> LoadRulesAsync()
    {
        if (_rulesCache is not null)
            return _rulesCache;

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var parts = await db.LlmKnowledgeEntries
            .Where(e => e.IsActive)
            .OrderBy(e => e.SortOrder)
            .Select(e => e.Content)
            .ToListAsync();

        _rulesCache = parts.Count > 0
            ? string.Join("\n\n---\n\n", parts)
            : "Правила не найдены. Используй свои знания о Call of Cthulhu 7e.";

        logger.LogDebug("Loaded {Count} LLM knowledge entries from database", parts.Count);
        return _rulesCache;
    }
}
