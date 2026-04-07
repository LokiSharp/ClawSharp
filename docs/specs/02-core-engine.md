# 02 - 推理核心与流程 (Core Engine & Flow)

## 1. ReAct 思考循环 (ReAct Reasoning Step)

ClawSharp 核心的大脑逻辑基于 ReAct 模式。

### 1.1 循环流程
1. **Input**: 用户通过 CLI 输入指令。
2. **Think (Thought)**: 模型分析任务，生成思考过程。
3. **Act (Action)**: 模型选择合适的插件工具并构造参数。
4. **Approval**: 若涉及写操作或敏感指令，CLI 拦截并弹出提示框。
5. **Observe (Observation)**: 插件执行并返回结果。
6. **Loop**: 重复上述过程直到任务完成。

---

## 2. 记忆管理 (Memory Management)

为了在本地高效管理上下文，ClawSharp 引入了多级记忆机制。

### 2.1 存储分层
- **短期记忆**: 基于会话的上下文窗口管理。
- **长期记忆**: 采用 JSON/SQLite 持久化。
- **语义搜索**: 结合嵌入模型 (Embedding) 进行检索（后期规划）。

### 2.2 压缩与清理
- **摘要策略**: 当 Token 接近阈值时，自动触发模型生成历史摘要。
- **剪裁**: 移除旧的重复 Observation 数据。
