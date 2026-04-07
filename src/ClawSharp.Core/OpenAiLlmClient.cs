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
    public async Task<string> ChatAsync(IEnumerable<ChatMessage> messages, IEnumerable<IClawTool>? tools = null, CancellationToken ct = default)
    {
        var request = new ChatRequest(
            modelName,
            messages.Select(m => new ChatRequestMessage(
                m.Role.ToString().ToLowerInvariant(),
                m.Content
            )),
            0.7
        );

        var response = await httpClient.PostAsJsonAsync("v1/chat/completions", request, SourceGenerationContext.Default.ChatRequest, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(SourceGenerationContext.Default.ChatCompletionResponse, ct);
        return result?.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, IEnumerable<IClawTool>? tools = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // 简易流式实现，后续可优化为真正的流式解析
        yield return await ChatAsync(messages, tools, ct);
    }

    public record ChatCompletionResponse(List<Choice> Choices);
    public record Choice(ResponseMessage Message);
    public record ResponseMessage(string Content);

    public record ChatRequest(string Model, IEnumerable<ChatRequestMessage> Messages, double Temperature);
    public record ChatRequestMessage(string Role, string Content);
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OpenAiLlmClient.ChatRequest))]
[JsonSerializable(typeof(OpenAiLlmClient.ChatCompletionResponse))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
