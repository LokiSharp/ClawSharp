namespace ClawSharp.Core.Models;

/// <summary>
/// ReAct 思考过程中的单个步进
/// </summary>
public record ReasoningStep
{
    // Thought: AI 的思考路径
    public string? Thought { get; init; }

    // Action: 决定调用的工具
    public ToolCall? Action { get; init; }

    // Observation: 工具执行反馈
    public string? Observation { get; init; }

    // ExecutingTool: 正在执行的工具名称 (用于 UI 反馈)
    public string? ExecutingTool { get; init; }
}

public record ToolCall(string ToolName, string Arguments);