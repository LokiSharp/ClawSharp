using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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
            // 使用 Base64 编码传递命令，避免任何转义/嵌套引号导致的挂起或解析错误
            // PowerShell 的 -EncodedCommand 期望的是 Base64 编码的 UTF-16LE 字符串
            // 显式设置编码为 UTF8 以确保输出不乱码
            string fullCommand = $"$OutputEncoding = [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; {command}";
            var bytes = Encoding.Unicode.GetBytes(fullCommand);
            var base64 = Convert.ToBase64String(bytes);
            
            startInfo.FileName = "powershell.exe";
            // -NoLogo 和 -NonInteractive 有助于在无头环境下更稳定地执行
            startInfo.Arguments = $"-NoProfile -NonInteractive -NoLogo -ExecutionPolicy Bypass -EncodedCommand {base64}";
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

        // 设置输出编码为 UTF8，避免中文乱码
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        using var process = new Process { StartInfo = startInfo };
        
        try 
        {
            process.Start();
            
            // 先并发读取流，再等待进程退出或超时，防止缓冲区死锁
            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);
            var exitTask = process.WaitForExitAsync(ct);

            // 设置 5 分钟的硬超时
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5), ct);
            var completedTask = await Task.WhenAny(exitTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                process.Kill(true);
                return "Error: Command timed out after 5 minutes.";
            }

            // 进程已退出，等待读取完成
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                var combinedError = (error + output).Trim();
                return $"Error (ExitCode {process.ExitCode}): {combinedError}";
            }

            if (string.IsNullOrWhiteSpace(output) && string.IsNullOrWhiteSpace(error))
            {
                return "Command executed successfully (no output).";
            }

            return output;
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
