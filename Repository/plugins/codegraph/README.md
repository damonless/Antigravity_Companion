# CodeGraph AST Explorer Plugin (`codegraph`)

基于 AST 的代码符号拓扑与调用链精准探索插件。

## 包含组件
- **MCP Server (`codegraph`)**：启动 `codegraph serve --mcp`，提供 `codegraph_explore` 工具。
- **Rule (`rules/AGENTS.md`)**：定义代码图谱检索分级矩阵、多分支 `codegraph.cmd sync` 同步规约及防盲目 `grep` 熔断机制（仅在含 `.codegraph/` 索引的工程中激活）。
