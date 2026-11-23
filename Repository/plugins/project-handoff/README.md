# Project Handoff Plugin (`project-handoff`)

长会话防退化监控与项目交接单一事实载体（SSOT）生成套件。

## 包含组件
- **Skill (`project-handoff`)**：提供长任务阶段交接、日志降噪（`transcript_pruner.py`）与四段式 `PROJECT_STATE.md` 整理恢复工作流。
- **Lifecycle Hook (`project-handoff-monitor`)**：通过 `PreInvocation` 与 `Stop` 钩子自动追踪会话步数，达到阈值时触发轻量检查点评估并支持冷却与静默控制。
