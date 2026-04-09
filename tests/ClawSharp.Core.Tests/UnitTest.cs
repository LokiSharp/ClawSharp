using ClawSharp.Core.Models;

namespace ClawSharp.Core.Tests;

public class UnitTest
{
    private class MockLlmClient(params string[] responses) : ILlmClient
    {
        private int _callCount = 0;
        public List<IEnumerable<ChatMessage>> CapturedMessages { get; } = [];

        public Task<string> ChatAsync(IEnumerable<ChatMessage> messages, IEnumerable<IClawTool>? tools = null, CancellationToken ct = default)
        {
            CapturedMessages.Add(messages.ToList());
            var response = _callCount < responses.Length ? responses[_callCount] : "Done.";
            _callCount++;
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, IEnumerable<IClawTool>? tools = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return await ChatAsync(messages, tools, ct);
        }
    }

    private class MockTool(string name, string response, bool throwError = false) : IClawTool
    {
        public string Name => name;
        public string Description => "Mock Description";
        public string ParameterSchema => "{}";
        public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default) 
        {
            if (throwError) throw new Exception("Tool failed");
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task AgentWorkflow_ReActLoop_Works()
    {
        // Arrange
        var mockLlm = new MockLlmClient(
            "Thought: I need to use the echo tool.\nAction: Echo(Hello)",
            "Thought: The task is finished."
        );
        var mockTool = new MockTool("Echo", "Echoed: Hello");
        var workflow = new AgentWorkflow(mockLlm, [mockTool]);

        // Act
        var steps = new List<ReasoningStep>();
        await foreach (var step in workflow.RunAsync("Run echo"))
        {
            steps.Add(step);
        }

        // Assert
        // 应产生 3 个步骤：1. Action, 2. Observation, 3. Final Thought
        Assert.Equal(3, steps.Count);
        
        Assert.Contains("echo tool", steps[0].Thought);
        Assert.Equal("Echo", steps[0].Action?.ToolName);
        
        Assert.Equal("Echoed: Hello", steps[1].Observation);

        Assert.Contains("finished", steps[2].Thought);
    }

    [Fact]
    public async Task AgentWorkflow_ToolNotFound_ReturnsErrorStep()
    {
        // Arrange
        var mockLlm = new MockLlmClient(
            "Thought: I need to use a non-existent tool.\nAction: BadTool(Hello)",
            "Thought: Tool not found, ending."
        );
        var workflow = new AgentWorkflow(mockLlm, []);

        // Act
        var steps = new List<ReasoningStep>();
        await foreach (var step in workflow.RunAsync("Run bad tool"))
        {
            steps.Add(step);
        }

        // Assert
        Assert.Equal(3, steps.Count);
        Assert.Contains("BadTool", steps[0].Action?.ToolName);
        Assert.Contains("Error", steps[1].Observation);
        Assert.Contains("BadTool", steps[1].Observation);
        Assert.Contains("ending", steps[2].Thought);
    }

    [Fact]
    public async Task AgentWorkflow_MaxIterations_StopsLoop()
    {
        // Arrange
        // Mock LLM will always request a tool call
        var mockLlm = new MockLlmClient(
            "Thought: Loop 1\nAction: Mock(1)",
            "Thought: Loop 2\nAction: Mock(2)",
            "Thought: Loop 3\nAction: Mock(3)",
            "Thought: Loop 4\nAction: Mock(4)",
            "Thought: Loop 5\nAction: Mock(5)",
            "Thought: Loop 6\nAction: Mock(6)"
        );
        var mockTool = new MockTool("Mock", "Done");
        var workflow = new AgentWorkflow(mockLlm, [mockTool]);

        // Act
        var steps = new List<ReasoningStep>();
        await foreach (var step in workflow.RunAsync("Looping"))
        {
            steps.Add(step);
        }

        // Assert
        // 每轮循环产生 2 个 Step (Action/Thought + Observation)，共 5 次循环 = 10 个 Step
        Assert.Equal(10, steps.Count);
    }

    [Fact]
    public async Task AgentWorkflow_EmptyResponse_StopsLoop()
    {
        // Arrange
        var mockLlm = new MockLlmClient("");
        var workflow = new AgentWorkflow(mockLlm, []);

        // Act
        var steps = new List<ReasoningStep>();
        await foreach (var step in workflow.RunAsync("Prompt"))
        {
            steps.Add(step);
        }

        // Assert
        Assert.Single(steps);
        Assert.Equal("", steps[0].Thought);
    }

    [Fact]
    public async Task AgentWorkflow_OnlyThought_StopsLoop()
    {
        // Arrange
        var mockLlm = new MockLlmClient("Thought: I don't need tools, just answering.");
        var workflow = new AgentWorkflow(mockLlm, []);

        // Act
        var steps = new List<ReasoningStep>();
        await foreach (var step in workflow.RunAsync("Hello"))
        {
            steps.Add(step);
        }

        // Assert
        Assert.Single(steps);
        Assert.Contains("answering", steps[0].Thought);
        Assert.Null(steps[0].Action);
    }

    [Fact]
    public async Task AgentWorkflow_ToolException_ShouldHandleError()
    {
        // Arrange
        var mockLlm = new MockLlmClient(
            "Thought: Calling failing tool\nAction: FailTool()",
            "Thought: It failed, I see."
        );
        var mockTool = new MockTool("FailTool", "Success", throwError: true);
        var workflow = new AgentWorkflow(mockLlm, [mockTool]);

        // Act
        var steps = new List<ReasoningStep>();
        await foreach (var step in workflow.RunAsync("Run fail"))
        {
            steps.Add(step);
        }

        // Assert
        Assert.Equal(3, steps.Count);
        Assert.Contains("FailTool", steps[0].Action?.ToolName);
        Assert.Contains("Error executing tool", steps[1].Observation);
        Assert.Contains("Tool failed", steps[1].Observation);
        Assert.Contains("failed, I see", steps[2].Thought);
    }

    [Fact]
    public async Task AgentWorkflow_History_PersistsBetweenCalls()
    {
        // Arrange
        var mockLlm = new MockLlmClient("Thought: Response 1", "Thought: Response 2");
        var workflow = new AgentWorkflow(mockLlm, []);

        // Act
        await foreach (var _ in workflow.RunAsync("Call 1")) { }
        await foreach (var _ in workflow.RunAsync("Call 2")) { }

        // Assert
        Assert.Equal(2, mockLlm.CapturedMessages.Count);
        
        // 第一个 call 有 2 条 message: System, Call 1
        Assert.Equal(2, mockLlm.CapturedMessages[0].Count());
        Assert.Equal(MessageRole.System, mockLlm.CapturedMessages[0].First().Role);
        Assert.Equal("Call 1", mockLlm.CapturedMessages[0].Last().Content);

        // 第二个 call 有 4 条 messages: System, Call 1, Response 1, Call 2
        var secondCallMessages = mockLlm.CapturedMessages[1].ToList();
        Assert.Equal(4, secondCallMessages.Count);
        Assert.Equal(MessageRole.System, secondCallMessages[0].Role);
        Assert.Equal("Call 1", secondCallMessages[1].Content);
        Assert.Equal("Thought: Response 1", secondCallMessages[2].Content); 
        Assert.Equal("Call 2", secondCallMessages[3].Content);
    }

    [Fact]
    public async Task AgentWorkflow_Parse_HandlesBoldMarkersAndThinkTags()
    {
        // Arrange
        var mockLlm = new MockLlmClient(
            "<think>Thinking process</think>\n**Thought**: Let's do it.\n**Action**: Echo({\"msg\": \"Hello\"})",
            "Done."
        );
        var mockTool = new MockTool("Echo", "Echoed: Hello");
        var workflow = new AgentWorkflow(mockLlm, [mockTool]);

        // Act
        var steps = new List<ReasoningStep>();
        await foreach (var step in workflow.RunAsync("Hello"))
        {
            steps.Add(step);
        }

        // Assert
        // 如果解析失败，它会返回 1 个 step，thought 为整个 response
        // 如果成功，它会返回 3 个 step (Action, Observation, Final Thought)
        Assert.True(steps.Count >= 2, $"Should have at least 2 steps, but got {steps.Count}. Parsed thought: {steps[0].Thought}");
        Assert.Equal("Echo", steps[0].Action?.ToolName);
        Assert.Contains("Hello", steps[0].Action?.Arguments);
    }

    [Fact]
    public async Task AgentWorkflow_Approval_Works()
    {
        // Arrange
        var mockLlm = new MockLlmClient(
            "Thought: I need to use the echo tool.\nAction: Echo(Hello)",
            "Thought: User denied it."
        );
        var mockTool = new MockTool("Echo", "Echoed: Hello");
        var workflow = new AgentWorkflow(mockLlm, [mockTool]);

        // Act & Assert (Denied)
        var steps = new List<ReasoningStep>();
        await foreach (var step in workflow.RunAsync("Run echo", action => Task.FromResult(false)))
        {
            steps.Add(step);
        }

        Assert.Equal(3, steps.Count);
        Assert.Equal("Action denied by user.", steps[1].Observation);
        Assert.Contains("denied", steps[2].Thought);
    }
    [Fact]
    public async Task AgentWorkflow_SystemPrompt_IncludesOsInfo()
    {
        // Arrange
        var mockLlm = new MockLlmClient("Done.");
        var workflow = new AgentWorkflow(mockLlm, []);

        // Act
        await foreach (var _ in workflow.RunAsync("Hello")) { }

        // Assert
        var systemMessage = mockLlm.CapturedMessages[0].First();
        Assert.Equal(MessageRole.System, systemMessage.Role);
        Assert.Contains("当前运行环境", systemMessage.Content);
        // 在测试环境下，它应该包含当前 OS 的描述
        Assert.Contains(System.Runtime.InteropServices.RuntimeInformation.OSDescription, systemMessage.Content);
    }
}