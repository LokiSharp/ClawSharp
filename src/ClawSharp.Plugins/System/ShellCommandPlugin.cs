using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClawSharp.Core;

namespace ClawSharp.Plugins.System;

/// <summary>
/// 执行系统 Shell 命令的插件。支持 Windows (PowerShell/Cmd) 和 Unix-like (Bash/Sh)。
/// </summary>
public class ShellCommandPlugin : IClawTool
{
    public string Name => "shell_execute";
    public string Description => "在本地系统中执行指定的 Shell 命令。支持跨平台执行。";

    public string ParameterSchema => """
    {
      "type": "object",
      "properties": {
        "command": {
          "type": "string",
          "description": "要执行的命令字符串"
        }
      },
      "required": ["command"]
    }
    """;

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        // 目前 AgentWorkflow 发送的可能是 raw 字符串也可能是 JSON，具体取决于 LLM 输出。
        // 为了健壮性，我们尝试先解析为 JSON，如果解析失败则视为 raw 命令。
        string command;
        try 
        {
            var args = JsonSerializer.Deserialize(arguments, ShellArgsContext.Default.ShellArgs);
            command = args?.Command ?? arguments;
        }
        catch 
        {
            command = arguments;
        }

        if (string.IsNullOrWhiteSpace(command))
            return "Error: Command is empty.";

        var startInfo = new ProcessStartInfo();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            startInfo.FileName = "powershell.exe";
            startInfo.Arguments = $"-NoProfile -Command \"{command.Replace("\"", "\\\"")}\"";
        }
        else
        {
            startInfo.FileName = "/bin/sh";
            startInfo.Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"";
        }

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        using var process = new Process { StartInfo = startInfo };
        
        try 
        {
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);

            await Task.WhenAny(Task.Delay(TimeSpan.FromMinutes(2), ct), process.WaitForExitAsync(ct));

            if (!process.HasExited)
            {
                process.Kill(true);
                return "Error: Command timed out after 2 minutes.";
            }

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                return $"Error (ExitCode {process.ExitCode}): {error}{output}";
            }

            return string.IsNullOrEmpty(output) ? "Command executed successfully (no output)." : output;
        }
        catch (Exception ex)
        {
            return $"Error launching process: {ex.Message}";
        }
    }
}

public record ShellArgs(string Command);

[JsonSerializable(typeof(ShellArgs))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class ShellArgsContext : JsonSerializerContext
{
}
