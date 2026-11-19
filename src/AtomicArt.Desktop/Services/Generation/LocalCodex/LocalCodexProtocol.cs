using System.Text.Json;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

internal sealed class LocalCodexProtocol(TextReader input, TextWriter output)
{
    private const string ModelProvider = "openai";
    private int _requestId;

    public async Task InitializeAsync(CancellationToken ct)
    {
        await RequestAsync("initialize", new
        {
            clientInfo = new { name = "atomicart", title = "AtomicArt", version = "1.0" }
        }, ct).ConfigureAwait(false);
        await SendAsync(new { method = "initialized" }, ct).ConfigureAwait(false);

        JsonElement result = await RequestAsync("account/read", new { refreshToken = false }, ct)
            .ConfigureAwait(false);
        if (!result.TryGetProperty("account", out JsonElement account)
            || account.ValueKind != JsonValueKind.Object
            || account.GetProperty("type").GetString() != "chatgpt")
        {
            throw new GenerationAttemptException(
                "Codex requires a ChatGPT account.", GenerationClientFailureCodes.CodexSignInRequired, false);
        }
    }

    public async Task<(IReadOnlyList<LocalCodexTextModel> Models, string? DefaultModelId, string? ConfiguredEffort)> ListTextModelsAsync(
        CancellationToken ct)
    {
        Dictionary<string, LocalCodexTextModel> models = new(StringComparer.Ordinal);
        HashSet<string> cursors = new(StringComparer.Ordinal);
        string? cursor = null;
        string? defaultModel = null;

        do
        {
            JsonElement page = await RequestAsync("model/list", new { modelProvider = ModelProvider, cursor }, ct)
                .ConfigureAwait(false);
            foreach (JsonElement entry in page.GetProperty("data").EnumerateArray())
            {
                if (GetOptionalString(entry, "model") is not { } id)
                {
                    continue;
                }

                IReadOnlyList<string> efforts = entry.TryGetProperty("supportedReasoningEfforts", out JsonElement choices)
                    && choices.ValueKind == JsonValueKind.Array
                    ? choices.EnumerateArray().Select(choice => GetOptionalString(choice, "reasoningEffort"))
                        .OfType<string>().Distinct(StringComparer.Ordinal).ToArray()
                    : Array.Empty<string>();
                models.TryAdd(id, new LocalCodexTextModel(id, GetOptionalString(entry, "displayName") ?? id,
                    efforts, GetOptionalString(entry, "defaultReasoningEffort")));
                if (entry.TryGetProperty("isDefault", out JsonElement isDefault) && isDefault.ValueKind == JsonValueKind.True)
                {
                    defaultModel ??= id;
                }
            }

            cursor = GetOptionalString(page, "nextCursor");
            if (cursor is not null && !cursors.Add(cursor))
            {
                throw new InvalidDataException("Codex returned a repeated model catalog cursor.");
            }
        }
        while (cursor is not null);

        JsonElement configuration = await RequestAsync("config/read", new { includeLayers = false }, ct).ConfigureAwait(false);
        JsonElement config = configuration.GetProperty("config");
        string? configuredModel = GetOptionalString(config, "model");
        return (models.Values.ToArray(), configuredModel ?? defaultModel, GetOptionalString(config, "model_reasoning_effort"));
    }

    public async Task<(string ThreadId, string? ModelId, JsonElement Image)> GenerateAsync(
        string workspace, IReadOnlyList<object> inputs, LocalCodexTurnOptions? options, CancellationToken ct)
    {
        JsonElement start = await RequestAsync("thread/start", new
        {
            cwd = workspace,
            ephemeral = true,
            approvalPolicy = "never",
            sandbox = "read-only",
            modelProvider = ModelProvider,
            model = options?.ModelId,
            developerInstructions = "Use the built-in image generator once for the user's image request. "
                + "Use attached images as references when supplied. Only generate the image. Do not run commands, "
                + "call external tools, or modify files."
        }, ct).ConfigureAwait(false);
        string threadId = GetId(start.GetProperty("thread"));
        string? modelId = GetOptionalString(start, "model") ?? options?.ModelId;
        Queue<JsonElement> pending = new();
        JsonElement turn = await RequestAsync("turn/start", new { threadId, input = inputs, effort = options?.ReasoningEffort }, ct, message =>
        {
            if (message.TryGetProperty("method", out JsonElement method)
                && method.GetString() is "turn/completed" or "item/completed")
            {
                pending.Enqueue(message);
            }
        }).ConfigureAwait(false);
        string turnId = GetId(turn.GetProperty("turn"));
        JsonElement image = default;
        bool limitReached = false;

        while (true)
        {
            JsonElement message = pending.TryDequeue(out JsonElement notification)
                ? notification
                : await ReadAsync(ct).ConfigureAwait(false);
            if (!message.TryGetProperty("method", out JsonElement method)
                || !message.TryGetProperty("params", out JsonElement parameters)
                || !parameters.TryGetProperty("threadId", out JsonElement eventThreadId)
                || eventThreadId.GetString() != threadId)
            {
                continue;
            }

            switch (method.GetString())
            {
                case "item/completed" when parameters.GetProperty("turnId").GetString() == turnId:
                    JsonElement item = parameters.GetProperty("item");
                    if (item.GetProperty("type").GetString() == "imageGeneration")
                    {
                        if (image.ValueKind == JsonValueKind.Undefined
                            && item.GetProperty("status").GetString() == "completed")
                        {
                            image = item;
                        }

                        limitReached |= IsUsageLimit(item, "failure", "type");
                    }

                    break;
                case "turn/completed":
                    JsonElement completed = parameters.GetProperty("turn");
                    if (GetId(completed) != turnId)
                    {
                        break;
                    }

                    if (completed.GetProperty("status").GetString() == "completed"
                        && image.ValueKind != JsonValueKind.Undefined)
                    {
                        return (threadId, modelId, image);
                    }

                    limitReached |= IsUsageLimit(completed, "error", "codexErrorInfo");
                    throw new GenerationAttemptException(
                        "Codex did not complete image generation.",
                        limitReached ? GenerationProviderFailureErrorCodes.RateLimited : GenerationClientFailureCodes.CodexNoImage,
                        false);
            }
        }
    }

    private async Task<JsonElement> RequestAsync(
        string method, object parameters, CancellationToken ct, Action<JsonElement>? onNotification = null)
    {
        int id = ++_requestId;
        await SendAsync(new { id, method, @params = parameters }, ct).ConfigureAwait(false);

        while (true)
        {
            JsonElement message = await ReadAsync(ct).ConfigureAwait(false);
            if (!message.TryGetProperty("id", out JsonElement replyId)
                || replyId.ValueKind != JsonValueKind.Number
                || replyId.GetInt32() != id)
            {
                onNotification?.Invoke(message);
                continue;
            }

            return message.TryGetProperty("result", out JsonElement result)
                ? result
                : throw new InvalidDataException($"Codex rejected {method}.");
        }
    }

    private async Task<JsonElement> ReadAsync(CancellationToken ct)
    {
        while (true)
        {
            string line = await input.ReadLineAsync(ct).ConfigureAwait(false)
                ?? throw new EndOfStreamException("Codex closed the App Server connection.");
            JsonElement message = JsonSerializer.Deserialize<JsonElement>(line);
            if (message.TryGetProperty("id", out JsonElement id) && message.TryGetProperty("method", out _))
            {
                await SendAsync(new { id, error = new { code = -32601, message = "Unsupported request" } }, ct)
                    .ConfigureAwait(false);
                continue;
            }

            return message;
        }
    }

    private async Task SendAsync(object message, CancellationToken ct)
    {
        string line = JsonSerializer.Serialize(message);
        await output.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
        await output.FlushAsync(ct).ConfigureAwait(false);
    }

    private static string GetId(JsonElement value)
    {
        return value.GetProperty("id").GetString()
            ?? throw new InvalidDataException("Codex did not return an identifier.");
    }

    private static string? GetOptionalString(JsonElement value, string property)
    {
        return value.TryGetProperty(property, out JsonElement element)
            && element.ValueKind == JsonValueKind.String
            && element.GetString() is { Length: > 0 } text
            ? text
            : null;
    }

    private static bool IsUsageLimit(JsonElement value, string property, string codeProperty)
    {
        return value.TryGetProperty(property, out JsonElement error)
            && error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty(codeProperty, out JsonElement code)
            && code.ValueKind == JsonValueKind.String
            && code.GetString() == "usageLimitExceeded";
    }
}
