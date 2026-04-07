# 04 - NativeAOT 适配要求 (NativeAOT Compatibility)

## 1. 编译模型

ClawSharp 强制要求全量 NativeAOT 编译。

### 1.1 带来的约束
- **禁用反射**: 严禁在热路径中使用 `System.Reflection`。
- **源码生成器 (Source Generators)**: 必须优先使用源码生成器实现代码逻辑（如 JSON 序列化、依赖注入等）。

---

## 2. 核心适配策略

针对 AI 开发中的动态特性，采取以下方案：

### 2.1 依赖管理
- 所有引入的 NuGet 包必须通过 AOT 兼容性检查。
- 核心引擎采用自定义的、基于 Source Generators 的反射替代方案。

### 2.2 插件加载
- 针对 MCP (Model Context Protocol) 插件，由于无法在 AOT 下动态加载 DLL，统一采用 **进程间通信 (IPC)** 模式调用外部独立进程。

---

## 3. 性能优化建议

- **裁剪指纹**: 定期检查二进制文件体积，通过配置 `TrimMode` 和 `OptimizationPreference` 进行调优。
- **静态链接**: 对核心 C 库（如 SQLite 或 OnnxRuntime）使用静态链接。
