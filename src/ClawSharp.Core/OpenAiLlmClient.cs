using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClawSharp.Core.Models;

namespace ClawSharp.Core;

/// <summary>
/// OpenAI 兼容接口的 LLM 客户端实现
/// </summary>
public class OpenAiLlmClient(HttpClient httpClient, string modelName) : ILlmClient
{
    public async Task<string> ChatAsync(IEnumerable<ChatMessage> messages, IEnumerable<IClawTool>? tools = null, string? model = null, CancellationToken ct = default)
    {
        // 预处理消息：角色转换与空消息过滤
        var processedMessages = messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => 
            {
                var role = m.Role switch
                {
                    MessageRole.System => "system",
                    MessageRole.Assistant => "assistant",
                    _ => "user" // Tool 和 User 都映射为 user
                };
                var content = m.Role == MessageRole.Tool ? $"Observation: {m.Content}" : m.Content;
                return new ChatRequestMessage(role, content);
            }).ToList();

        // 确保至少有一个 user 消息，并且符合常见的 System -> User -> Assistant 序列
        // 如果序列中没有 user 消息，或者 System 之后直接是 Assistant，某些本地模型会报错
        var finalMessages = new List<ChatRequestMessage>();
        
        // 如果第一条不是 system，且我们需要注入 system，后续逻辑会处理
        // 这里我们先处理合并
        foreach (var msg in processedMessages)
        {
            if (finalMessages.Count > 0 && finalMessages[^1].Role == msg.Role)
            {
                finalMessages[^1] = finalMessages[^1] with { Content = finalMessages[^1].Content + "\n\n" + msg.Content };
            }
            else
            {
                finalMessages.Add(msg);
            }
        }

        // 针对本地 LLM 的兼容性优化：
        // 1. 确保如果存在 system 消息，它必须是第一条。
        // 2. 确保 system 之后的第一条消息是 user。
        if (finalMessages.Count > 0 && finalMessages[0].Role == "system")
        {
            if (finalMessages.Count > 1 && finalMessages[1].Role == "assistant")
            {
                // 如果 system 之后直接是 assistant，将 system 内容合并到第一条 assistant 中，或者在中间插入一个占位 user
                // 这里选择将 system 合并到后续消息中，或者简单地在 system 之后插入一个空的 user 消息（如果允许）
                // 更稳健的做法是确保 history pruning 不会产生这种序列。但这里做一层兜底。
                var systemContent = finalMessages[0].Content;
                finalMessages.RemoveAt(0);
                if (finalMessages.Count > 0)
                {
                     finalMessages[0] = finalMessages[0] with { Content = $"System Instructions:\n{systemContent}\n\n{finalMessages[0].Content}" };
                }
            }
        }
        
        // 再次检查是否包含 user 消息 (LM Studio 报错的核心原因)
        if (!finalMessages.Any(m => m.Role == "user"))
        {
            // 如果完全没有 user 消息，将最后一条消息（如果是 assistant）转为 user，或者添加一个
            if (finalMessages.Count > 0 && finalMessages[^1].Role == "assistant")
            {
                var last = finalMessages[^1];
                finalMessages[^1] = last with { Role = "user" };
            }
            else if (finalMessages.Count == 0 || (finalMessages.Count == 1 && finalMessages[0].Role == "system"))
            {
                finalMessages.Add(new ChatRequestMessage("user", "Continue"));
            }
        }

        var request = new ChatRequest(
            model ?? modelName,
            finalMessages,
            0.7,
            4096
        );

        var response = await httpClient.PostAsJsonAsync("v1/chat/completions", request, SourceGenerationContext.Default.ChatRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}).\nError Body: {errorBody}", null, response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync(SourceGenerationContext.Default.ChatCompletionResponse, ct);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, IEnumerable<IClawTool>? tools = null, string? model = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // 简易流式实现，后续可优化为真正的流式解析
        yield return await ChatAsync(messages, tools, model, ct);
    }

    public record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] List<Choice> Choices);
    public record Choice(
        [property: JsonPropertyName("message")] ResponseMessage Message);
    public record ResponseMessage(
        [property: JsonPropertyName("content")] string Content);

    public record ChatRequest(
        [property: JsonPropertyName("model")] string Model, 
        [property: JsonPropertyName("messages")] List<ChatRequestMessage> Messages, 
        [property: JsonPropertyName("temperature")] double Temperature, 
        [property: JsonPropertyName("max_tokens")] int? MaxTokens = null);

    public record ChatRequestMessage(
        [property: JsonPropertyName("role")] string Role, 
        [property: JsonPropertyName("content")] string Content, 
        [property: JsonPropertyName("name")] string? Name = null);
}

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OpenAiLlmClient.ChatRequest))]
[JsonSerializable(typeof(OpenAiLlmClient.ChatCompletionResponse))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
