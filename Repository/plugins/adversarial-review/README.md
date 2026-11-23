# Adversarial Review & Dual-Agent Team (`adversarial-review`)

对抗式质量门禁与多智能体协同审查套件。

## 包含技能
- **`dual-agent-team`**：主智能体（Main Agent）与审查副智能体（`reviewer-agent`）分三阶段（需求规划、方案设计、执行验证）闭环协作。
- **`turn-audit`**：派生独立红队审查副智能体（`turn-reviewer`），从需求符合度、逻辑闭环、代码级缺陷、极端工况、脆弱性假设、破坏半径六个维度审计上一轮输出。
