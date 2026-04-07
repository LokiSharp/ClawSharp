# 01 - 架构与项目概览 (Architecture & Overview)

## 1. 项目概述
ClawSharp 是一个基于 .NET 10 构建的跨平台、高性能本地智能体 (AI Agent) 网关。它旨在利用 C# 的强类型安全性和 NativeAOT 的极致性能，为开发者提供一个可控、安全且低延迟的 AI 执行环境。

### 核心目标
- **极致性能**：通过 NativeAOT 编译，实现毫秒级启动和极低内存占用。
- **系统级控制**：直接调用底层系统 API 或 Shell，实现对操作系统的深度控制。
- **安全沙箱**：引入人机协作 (Human-in-the-loop) 机制，确保高危操作必须经过审批。
- **标准化适配**：支持 MCP (Model Context Protocol) 协议，无缝对接生态插件。

---

## 2. 架构设计

项目采用分层架构，确保核心逻辑、用户界面与外部副作用之间的隔离。

### 2.1 模块分解
- **ClawSharp.CLI (宿主交互层)**
    - 职责：负责用户输入采集、流式文本渲染、审批界面弹出。
    - 技术：基于 `Spectre.Console` 实现，适配 ANSI 终端。
- **ClawSharp.Core (推理核心层)**
    - 职责：管理 AI 的生命周期。包含 ReAct 推理引擎、模型路由 (Model Router)、长期记忆 (Long-term Memory) 管理。
    - 技术：基于 C# 14 原生开发的轻量级推理引擎，100% 适配 NativeAOT。
- **ClawSharp.Plugins (副作用执行层)**
    - 职责：执行具体的原子操作（利爪）。
    - 子模块：
        - `System`: 跨平台 Shell 指令执行。
        - `FileOps`: 安全受限的文件读写。
        - `Mcp`: 兼容模型上下文协议的第三方插件桥接。

---

## 3. 技术栈
- **运行时**: .NET 10.0 (LTS)
- **语言**: C# 14
- **编译模型**: NativeAOT (强制要求)
- **UI 框架**: Spectre.Console
- **内核框架**: 自定义轻量级 ReAct 引擎 (AOT 优先)
- **配置与注入**: Microsoft.Extensions (Configuration/DependencyInjection/Logging)

---

## 4. 路线图 (Roadmap)
- [ ] Phase 1: 基础 CLI 骨架与自定义 ReAct 引擎跑通。
- [ ] Phase 2: 实现 System/File 基础插件包。
- [ ] Phase 3: 完善 ReAct 循环流式展示。
- [ ] Phase 4: 支持 MCP 协议桥接。
- [ ] Phase 5: 优化 AOT 发布包体积与内存指纹。
