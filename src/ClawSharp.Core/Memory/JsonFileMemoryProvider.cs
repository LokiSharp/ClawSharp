using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using ClawSharp.Core.Models;

namespace ClawSharp.Core.Memory;

/// <summary>
/// 基于 JSON 文件的记忆提供者实现。
/// </summary>
public class JsonFileMemoryProvider(string filePath) : IMemoryProvider
{
    private static readonly JsonSerializerOptions _options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true, // 让 JSON 文件更易读
        Converters = { new JsonStringEnumConverter<MessageRole>(JsonNamingPolicy.CamelCase) }
    };

    private static readonly MemorySourceGenerationContext _context = new(_options);

    public async Task SaveHistoryAsync(IEnumerable<ChatMessage> history, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, history, _context.IEnumerableChatMessage, ct);
    }

    public async Task<List<ChatMessage>> LoadHistoryAsync(CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        using var stream = File.OpenRead(filePath);
        try
        {
            var history = await JsonSerializer.DeserializeAsync(stream, _context.ListChatMessage, ct);
            return history ?? [];
        }
        catch
        {
            return [];
        }
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }
}

[JsonSerializable(typeof(List<ChatMessage>))]
[JsonSerializable(typeof(IEnumerable<ChatMessage>))]
internal partial class MemorySourceGenerationContext : JsonSerializerContext
{
}
