using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ClawSharp.Core.Models;

namespace ClawSharp.Core;

/// <summary>
/// 手动实现 ReAct 思考循环 (Think-Act-Observe)
/// 100% NativeAOT 友好，不依赖任何重量级框架。
/// </summary>
public partial class AgentWorkflow(ILlmClient llmClient, IEnumerable<IClawTool> tools) : IAgentWorkflow
{
    private readonly List<ChatMessage> _history = [];

    [GeneratedRegex(@"(?:\*\*)*Thought(?:\*\*)*:\s*(.*?)(?=\s*(?:\*\*)*Action(?:\*\*)*:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThoughtRegex();

    [GeneratedRegex(@"(?:\*\*)*Action(?:\*\*)*:\s*(\w+)\((.*)\)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ActionRegex();

    [GeneratedRegex(@"<think>\s*(.*?)\s*</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkRegex();

    public async IAsyncEnumerable<ReasoningStep> RunAsync(
        string prompt, 
        ActionApprovalCallback? onApproval = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 0. 初始化系统提示词 (仅在会话开始时)
        if (_history.Count == 0)
        {
            _history.Add(new ChatMessage(MessageRole.System, BuildSystemPrompt()));
        }

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
                string observation;
                
                // 检查审批回调
                if (onApproval != null && !await onApproval(toolCall))
                {
                    observation = "Action denied by user.";
                }
                else
                {
                    var tool = tools.FirstOrDefault(t => t.Name == toolCall.ToolName);
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
        // 增强版解析：支持 **Thought**:、**Action**: 以及 <think> 标签
        var thinkMatch = ThinkRegex().Match(response);
        var thoughtMatch = ThoughtRegex().Match(response);
        var actionMatch = ActionRegex().Match(response);

        string? think = thinkMatch.Success ? thinkMatch.Groups[1].Value.Trim() : null;
        string? thought = thoughtMatch.Success ? thoughtMatch.Groups[1].Value.Trim() : null;
        ToolCall? action = actionMatch.Success ? new ToolCall(actionMatch.Groups[1].Value.Trim(), actionMatch.Groups[2].Value.Trim()) : null;

        // 合并思考过程
        string? combinedThought = null;
        if (think != null || thought != null)
        {
            combinedThought = (think != null ? $"[Think] {think}\n" : "") + (thought ?? "");
            combinedThought = combinedThought.Trim();
        }

        // 如果没匹配到显式格式，则整体视为 Thought
        if (combinedThought == null && action == null && !string.IsNullOrWhiteSpace(response))
        {
            combinedThought = response.Trim();
        }

        return (combinedThought, action); 
    }

    private string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是一个名为 ClawSharp 的 AI 助手。你生活在用户的本地系统中，拥有执行系统命令和操作文件的能力。");
        sb.AppendLine($"当前运行环境: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        sb.AppendLine("你必须遵循 ReAct (Reasoning and Acting) 模式进行思考和行动。");
        sb.AppendLine();
        sb.AppendLine("你可以使用的工具有：");
        foreach (var tool in tools)
        {
            sb.AppendLine($"- {tool.Name}: {tool.Description}");
            sb.AppendLine($"  参数格式: {tool.ParameterSchema}");
        }
        sb.AppendLine();
        sb.AppendLine("输出格式规范：");
        sb.AppendLine("1. **Thought**: 思考当前的任务、已有的观察结果以及接下来的计划。");
        sb.AppendLine("2. **Action**: 选择一个工具并提供参数，格式为：tool_name(arguments)。如果工具不需要参数，括号留空。");
        sb.AppendLine("   示例：Action: shell_execute({\"command\": \"ls\"})");
        sb.AppendLine("3. **Observation**: 这是工具执行后的结果，由系统提供。");
        sb.AppendLine();
        sb.AppendLine("重要：一次只能输出一个 Action。当任务完成时，请直接输出最终结论，不要再包含 Action。");
        sb.AppendLine("开始！");
        return sb.ToString();
    }
}