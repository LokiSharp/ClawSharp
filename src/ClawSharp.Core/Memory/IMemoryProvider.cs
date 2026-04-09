using ClawSharp.Core.Models;

namespace ClawSharp.Core.Memory;

/// <summary>
/// 记忆提供者接口，负责对话历史的持久化与恢复。
/// </summary>
public interface IMemoryProvider
{
    /// <summary>
    /// 保存对话历史。
    /// </summary>
    Task SaveHistoryAsync(IEnumerable<ChatMessage> history, CancellationToken ct = default);

    /// <summary>
    /// 加载对话历史。
    /// </summary>
    Task<List<ChatMessage>> LoadHistoryAsync(CancellationToken ct = default);

    /// <summary>
    /// 清除记忆。
    /// </summary>
    Task ClearAsync(CancellationToken ct = default);
}
