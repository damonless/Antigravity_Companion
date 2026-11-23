EvoKernel Meta-Rules:
1. Search before act: Call evo_search(query, workspace) before reading/editing unfamiliar files or debugging errors.
2. Progressive inspect: On rule/skill/memory hit, call evo_inspect(id, "L2", workspace) to load full SOP/anchors before proceeding.
3. Mandatory crystallization: After verifying a bug fix or receiving user correction, call evo_propose(kind, ...) to crystallize lessons into .evo/.
4. Think in code: Use evo_run(tool_id, args, workspace) for multi-file checks instead of dumping raw file content.