# 03 - 安全规范与沙箱 (Security & Sandbox)

## 1. 权限分级 (Permission Levels)

所有“利爪”插件必须声明其风险等级：

- **Read-Only (低风险)**: 读取文件、获取系统基本信息。无感执行。
- **Side-Effect (中风险)**: 修改非系统关键文件、执行一般的 Shell 命令。
- **High-Risk (高风险)**: 删除操作、写入敏感目录、执行带有管理员权限的指令。

---

## 2. 人机协作审批 (Human-in-the-loop)

针对中高风险操作，ClawSharp 强制进入审批流。

### 2.1 交互流程
1. 插件触发 `Action`。
2. `AgentWorkflow` 检测到 Side-Effect。
3. 调用 `ClawSharp.CLI` 的 UI 组件弹出确认框。
4. 用户可选择 `Approve` (运行一次), `Approve All` (本次会话全部允许) 或 `Deny` (拒绝执行)。

---

## 3. 安全沙箱 (Sandbox)

- **路径限制**: 文件插件默认限制在项目工作目录内，除非显式配置白名单。
- **命令审计**: 记录所有执行的命令及其输出到 `.logs/audit.log`。
- **超时保护**: 单次工具执行不可超过预设的秒数（防止 AI 进入死循环）。
