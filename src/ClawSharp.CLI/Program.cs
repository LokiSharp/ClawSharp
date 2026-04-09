using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using ClawSharp.Core;
using ClawSharp.Plugins.FileOps;
using ClawSharp.Plugins.System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables("CLAW_")
    .Build();

var baseUrl = config["BASE_URL"];
var apiKey = config["API_KEY"];
var model = config["MODEL"];

if (string.IsNullOrEmpty(baseUrl))
{
    AnsiConsole.MarkupLine("[red]Error: Base URL is missing. Set CLAW_BASE_URL environment variable.[/]");
    AnsiConsole.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return;
}

if (string.IsNullOrEmpty(apiKey))
{
    AnsiConsole.MarkupLine("[red]Error: API Key is missing. Set CLAW_API_KEY environment variable.[/]");
    AnsiConsole.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return;
}

if (string.IsNullOrEmpty(model))
{
    AnsiConsole.MarkupLine("[red]Error: Model name is missing. Set CLAW_MODEL environment variable.[/]");
    AnsiConsole.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return;
}

var services = new ServiceCollection();
services.AddHttpClient("OpenAi", client =>
{
    client.BaseAddress = new Uri(baseUrl!);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    client.Timeout = TimeSpan.FromMinutes(10);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()); // AOT 友好

services.AddSingleton<ILlmClient>(sp => 
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = factory.CreateClient("OpenAi");
    return new OpenAiLlmClient(httpClient, model!);
});

services.AddSingleton<IClawTool, ShellCommandPlugin>();
services.AddSingleton<IClawTool, FileOpsPlugin>();

services.AddSingleton<IAgentWorkflow>(sp => 
    new AgentWorkflow(sp.GetRequiredService<ILlmClient>(), sp.GetServices<IClawTool>()));

var serviceProvider = services.BuildServiceProvider();

AnsiConsole.Write(
    new FigletText("ClawSharp")
        .LeftJustified()
        .Color(Color.Green));

AnsiConsole.MarkupLine("[bold white]Welcome to [green]ClawSharp[/] - Your local AI Agent gateway.[/]");
AnsiConsole.MarkupLine($"[grey]Environment: .NET 10 (NativeAOT: {(RuntimeFeature.IsDynamicCodeSupported ? "Disabled" : "Enabled")})[/]");
AnsiConsole.WriteLine();

var workflow = serviceProvider.GetRequiredService<IAgentWorkflow>();

while (true)
{
    var prompt = AnsiConsole.Ask<string>("[bold blue]>[/] ");
    if (string.IsNullOrWhiteSpace(prompt) || prompt == "exit") break;

    AnsiConsole.MarkupLine("[grey]Agent is processing your request...[/]");
    await foreach (var step in workflow.RunAsync(prompt, async action => 
    {
        return AnsiConsole.Confirm($"[yellow]Approval Required:[/] Allow execution of [bold]{Markup.Escape(action.ToolName)}[/]?");
    }))
    {
        if (!string.IsNullOrEmpty(step.Thought))
        {
            AnsiConsole.MarkupLine($"[grey]Thought: {Markup.Escape(step.Thought)}[/]");
        }
        if (step.Action != null)
        {
            AnsiConsole.MarkupLine($"[yellow]Action: {Markup.Escape(step.Action.ToolName)}({Markup.Escape(step.Action.Arguments)})[/]");
        }
        if (!string.IsNullOrEmpty(step.Observation))
        {
            AnsiConsole.MarkupLine($"[green]Observation: {Markup.Escape(step.Observation)}[/]");
        }
    }
}