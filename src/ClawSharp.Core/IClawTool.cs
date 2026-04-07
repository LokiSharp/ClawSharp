using ClawSharp.Core.Models;

namespace ClawSharp.Core;

/// <summary>
/// 统一定义“利爪”工具的契约
/// </summary>
public interface IClawTool
{
    string Name { get; }
    string Description { get; }
    
    // 输入参数的 JSON Schema (用于 Prompt 注入)
    string ParameterSchema { get; }

    Task<string> ExecuteAsync(string arguments, CancellationToken ct = default);
}