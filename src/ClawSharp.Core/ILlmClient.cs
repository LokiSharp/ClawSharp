using ClawSharp.Core.Models;

namespace ClawSharp.Core;

/// <summary>
/// 封装与 LLM 的直接通信
/// </summary>
public interface ILlmClient
{
    Task<string> ChatAsync(IEnumerable<ChatMessage> messages, IEnumerable<IClawTool>? tools = null, string? model = null, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, IEnumerable<IClawTool>? tools = null, string? model = null, CancellationToken ct = default);
}