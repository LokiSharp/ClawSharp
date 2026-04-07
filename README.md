# ClawSharp 爪锐

![.NET 10 LTS](https://img.shields.io/badge/.NET-10.0_LTS-512BD4.svg?style=flat&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Win%20%7C%20macOS%20%7C%20Linux-lightgrey.svg)
![License](https://img.shields.io/badge/License-MIT%20OR%20Apache--2.0-green.svg)

> 一个基于 C# .NET 构建的跨平台高性能本地智能体 (AI Agent) 网关。

## 🎯 核心愿景

ClawSharp 旨在提供一个拥有极致性能和系统级控制力的本地化 Agent 运行时。它能够在极低内存占用的情况下，作为系统后台守护进程安全、精准地执行底层任务。

## 🏗️ 架构概览

- **`ClawSharp.CLI`**: 基于 Spectre.Console 构建的宿主交互终端，提供流式输出与人机协作审批。
- **`ClawSharp.Core`**: 承载大模型 ReAct 思考循环、多模型路由分配与长期记忆管理的大脑核心。
- **`ClawSharp.Plugins`**: 隔离底层副作用的扩展利爪，包含系统级读写操作、代码解析及 MCP (模型上下文协议, Model Context Protocol) 桥接网关。

## 🚀 快速启动

本项目强制要求 `.NET 10.0+` (LTS) SDK。

```bash
# 恢复依赖
dotnet restore

# 运行终端交互模式
dotnet run --project src/ClawSharp.CLI
```

# 🛡️ 安全沙箱

默认启用 Human-in-the-loop (人机协作) 审批拦截流。任何涉及高危系统命令的执行、文件覆写操作，均必须经过显式终端确认。

## ⚖️ 许可证

本项目采用双重授权协议。您可以根据自身需求，自由选择遵守 [MIT 许可证](LICENSE-MIT) (MIT License) 或 [Apache 2.0 许可证](LICENSE-APACHE) (Apache License, Version 2.0)。

除非您明确声明，否则您为了包含在本项目中而有意提交的任何代码贡献（按照 Apache-2.0 许可证的定义），均应按上述双重许可证授权，不附加任何额外条款或条件。
