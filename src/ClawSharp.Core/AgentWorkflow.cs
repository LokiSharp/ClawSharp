using System.Runtime.CompilerServices;
using ClawSharp.Core.Models;

namespace ClawSharp.Core;

/// <summary>
/// 手动实现 ReAct 思考循环 (Think-Act-Observe)
/// 100% NativeAOT 友好，不依赖任何重量级框架。
/// </summary>
public partial class AgentWorkflow(ILlmClient llmClient, IEnumerable<IClawTool> tools) : IAgentWorkflow
{
    private readonly List<ChatMessage> _history = [];

    [System.Text.RegularExpressions.GeneratedRegex(@"Thought:\s*(.*?)(?=\s*Action:|$)", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex ThoughtRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"Action:\s*(\w+)\((.*)\)", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex ActionRegex();

    public async IAsyncEnumerable<ReasoningStep> RunAsync(
        string prompt, 
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1. 记录用户意图
        _history.Add(new ChatMessage(MessageRole.User, prompt));

        bool isTaskComplete = false;
        int maxIterations = 5; // 防止进入无限循环

        for (int i = 0; i < maxIterations && !isTaskComplete; i++)
        {
            // 2. 调用 LLM 获取下一步计划 (Thought + Action)
            // 在自研模式下，我们需要在 System Prompt 中定义 ReAct 的格式要求
            string response = await llmClient.ChatAsync(_history, tools, ct);

            // 3. 解析模型输出 (这里通常需要一个专门的 Parser)
            // 假设模型遵循：Thought: xxx \n Action: tool_name(args)
            var (thought, toolCall) = ParseLlmResponse(response);

            if (toolCall != null)
            {
                yield return new ReasoningStep 
                { 
                    Thought = thought, 
                    Action = toolCall 
                };

                // 4. 执行工具调用 (Observe)
                var tool = tools.FirstOrDefault(t => t.Name == toolCall.ToolName);
                string observation;
                
                if (tool != null)
                {
                    try
                    {
                        observation = await tool.ExecuteAsync(toolCall.Arguments, ct);
                    }
                    catch (Exception ex)
                    {
                        observation = $"Error executing tool '{toolCall.ToolName}': {ex.Message}";
                    }
                }
                else
                {
                    observation = $"Error: Tool '{toolCall.ToolName}' not found.";
                }

                yield return new ReasoningStep { Observation = observation };

                // 5. 将 Observation 反馈给历史，进入下一轮思考
                _history.Add(new ChatMessage(MessageRole.Assistant, response));
                _history.Add(new ChatMessage(MessageRole.Tool, observation, toolCall.ToolName));
            }
            else
            {
                // 没有工具调用，任务可能已完成或只是普通对话
                yield return new ReasoningStep { Thought = thought ?? response };
                _history.Add(new ChatMessage(MessageRole.Assistant, response));
                isTaskComplete = true;
            }
        }
    }

    private static (string? Thought, ToolCall? Action) ParseLlmResponse(string response)
    {
        // 简单正则解析 ReAct 格式：Thought: [思考内容] Action: [工具名]([参数])
        var thoughtMatch = ThoughtRegex().Match(response);
        var actionMatch = ActionRegex().Match(response);

        string? thought = thoughtMatch.Success ? thoughtMatch.Groups[1].Value.Trim() : null;
        ToolCall? action = actionMatch.Success ? new ToolCall(actionMatch.Groups[1].Value.Trim(), actionMatch.Groups[2].Value.Trim()) : null;

        // 如果没匹配到显式格式，则整体视为 Thought
        if (thought == null && action == null && !string.IsNullOrWhiteSpace(response))
        {
            thought = response.Trim();
        }

        return (thought, action); 
    }
}