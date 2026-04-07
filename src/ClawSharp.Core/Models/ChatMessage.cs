namespace ClawSharp.Core.Models;

public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool
}

public record ChatMessage(MessageRole Role, string Content, string? Name = null)
{
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
}