using System.Text.Json;
using System.Text.Json.Serialization;
using ClawSharp.Core;

namespace ClawSharp.Plugins.FileOps;

/// <summary>
/// 提供基本文件系统操作的插件。
/// </summary>
public class FileOpsPlugin : IClawTool
{
    public string Name => "file_operations";
    public string Description => "执行文件操作。支持的操作：read (读取)、write (写入)、list (列出目录内容)。";

    public string ParameterSchema => """
    {
      "type": "object",
      "properties": {
        "operation": {
          "type": "string",
          "enum": ["read", "write", "list"],
          "description": "要执行的操作类型"
        },
        "path": {
          "type": "string",
          "description": "文件或目录的路径"
        },
        "content": {
          "type": "string",
          "description": "写入操作时使用的内容（仅限 write 操作）"
        }
      },
      "required": ["operation", "path"]
    }
    """;

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        FileArgs? args;
        try 
        {
            args = JsonSerializer.Deserialize(arguments, FileArgsContext.Default.FileArgs);
        }
        catch (JsonException)
        {
            return "Error: Invalid JSON arguments. Please provide arguments in JSON format.";
        }

        if (args == null) return "Error: Arguments are null.";

        try 
        {
            return args.Operation.ToLowerInvariant() switch
            {
                "read" => await ReadFileAsync(args.Path, ct),
                "write" => await WriteFileAsync(args.Path, args.Content, ct),
                "list" => ListDirectory(args.Path),
                _ => $"Error: Unsupported operation '{args.Operation}'."
            };
        }
        catch (Exception ex)
        {
            return $"Error performing file operation: {ex.Message}";
        }
    }

    private async Task<string> ReadFileAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return $"Error: File '{path}' not found.";
        return await File.ReadAllTextAsync(path, ct);
    }

    private async Task<string> WriteFileAsync(string path, string? content, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(path, content ?? string.Empty, ct);
        return $"Successfully wrote to file '{path}'.";
    }

    private string ListDirectory(string path)
    {
        if (!Directory.Exists(path)) return $"Error: Directory '{path}' not found.";
        var entries = Directory.GetFileSystemEntries(path);
        return string.Join(Environment.NewLine, entries.Select(Path.GetFileName));
    }
}

public record FileArgs(string Operation, string Path, string? Content = null);

[JsonSerializable(typeof(FileArgs))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class FileArgsContext : JsonSerializerContext
{
}
