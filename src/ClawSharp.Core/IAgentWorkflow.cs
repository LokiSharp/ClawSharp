using ClawSharp.Core.Models;

namespace ClawSharp.Core;

public delegate Task<bool> ActionApprovalCallback(ToolCall toolCall);

/// <summary>
/// 定义 AI 代理的工作流引擎
/// </summary>
public interface IAgentWorkflow
{
    /// <summary>
    /// 处理用户请求并进入推理循环 (ReAct)
    /// </summary>
    IAsyncEnumerable<ReasoningStep> RunAsync(string prompt, ActionApprovalCallback? onApproval = null, CancellationToken ct = default);
}