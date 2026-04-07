using Spectre.Console;

AnsiConsole.Write(
    new FigletText("ClawSharp")
        .LeftJustified()
        .Color(Color.Green));

AnsiConsole.MarkupLine("[bold white]Welcome to [green]ClawSharp[/] - Your local AI Agent gateway.[/]");
AnsiConsole.MarkupLine("[grey]Environment: .NET 10 (NativeAOT enabled)[/]");
AnsiConsole.WriteLine();

// TODO: 初始化 DI 和 Kernel
AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .Start("Thinking...", ctx => 
    {
        // 模拟初始化
        Thread.Sleep(1000);
    });

AnsiConsole.MarkupLine("[yellow]Ready.[/]");