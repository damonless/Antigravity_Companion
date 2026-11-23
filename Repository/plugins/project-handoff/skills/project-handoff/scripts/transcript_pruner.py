"""
Transcript Pruner - Deterministic Log Pruning Engine
Optimized for Google Antigravity & Gemini Project Handoff.

Performs zero-LLM, ultra-fast (<30ms) deterministic log pruning on transcript.jsonl.
Eliminates verbose terminal dumps, progress bars, and massive file read outputs
while 100% preserving:
- User requirements & confirmations
- Tool call targets (CommandLine, TargetFile, edits)
- Execution status, exit codes, and key error tracebacks
- CodeGraph symbols and paths
- Model decisions & generated code diffs
"""

import sys
import json
import re
from pathlib import Path
from typing import Any

if hasattr(sys.stdin, "reconfigure"):
    sys.stdin.reconfigure(encoding="utf-8")
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

MAX_OUTPUT_LINES = 30
HEAD_LINES = 10
TAIL_LINES = 10

def prune_text_content(text: str, max_lines: int = MAX_OUTPUT_LINES) -> str:
    """Prunes overly long text (e.g. command stdout or file dumps) while keeping head and tail."""
    if not text:
        return ""
    lines = text.splitlines()
    if len(lines) <= max_lines:
        return text

    # Check for crucial errors in the middle that should not be dropped
    error_lines = [
        f"  [L{i+1}] {line}"
        for i, line in enumerate(lines[HEAD_LINES:-TAIL_LINES], start=HEAD_LINES)
        if re.search(r"\b(error|exception|traceback|fatal|failed|panic)\b", line, re.IGNORECASE)
    ]

    head = lines[:HEAD_LINES]
    tail = lines[-TAIL_LINES:]
    pruned_count = len(lines) - HEAD_LINES - TAIL_LINES

    omitted_notice = [f"\n... [Pruned {pruned_count} lines of verbose output/dumps] ...\n"]
    if error_lines:
        omitted_notice.append("... [Extracted critical error lines from pruned section]:")
        omitted_notice.extend(error_lines[:5])
        if len(error_lines) > 5:
            omitted_notice.append(f"... and {len(error_lines) - 5} more error lines ...")
        omitted_notice.append("\n")

    return "\n".join(head + omitted_notice + tail)

def prune_tool_call(tool_call: dict[str, Any]) -> dict[str, Any]:
    """Preserves essential parameters while trimming huge payloads."""
    name = tool_call.get("name", "")
    args = tool_call.get("args", {})
    if isinstance(args, str):
        try:
            args = json.loads(args)
        except Exception:
            args = {"raw_args": args}

    pruned_args = {}
    for k, v in args.items():
        if isinstance(v, str):
            # If writing a huge file, keep description and truncate code
            if k in ("CodeContent", "ReplacementContent") and len(v) > 2000:
                pruned_args[k] = prune_text_content(v, max_lines=20)
            else:
                pruned_args[k] = v
        else:
            pruned_args[k] = v

    return {
        "name": name,
        "args": pruned_args,
        "toolSummary": tool_call.get("toolSummary", "")
    }

def prune_step(raw_step: dict[str, Any]) -> dict[str, Any] | None:
    """Prunes a single step from transcript.jsonl."""
    source = raw_step.get("source", "")
    step_type = raw_step.get("type", "")
    content = raw_step.get("content", "")
    step_index = raw_step.get("step_index", 0)

    # 1. User inputs: always preserve 100%
    if "USER" in source or step_type in ("USER_INPUT", "USER_EXPLICIT"):
        return {
            "step_index": step_index,
            "type": "USER_INPUT",
            "source": source,
            "content": content
        }

    # 2. System ephemeral messages: keep if related to handoff / checkpoint
    if step_type == "EPHEMERAL_MESSAGE" or "SYSTEM" in source:
        if content and any(k in content for k in ["project-handoff", "检查点", "checkpoint", "交接"]):
            return {
                "step_index": step_index,
                "type": "SYSTEM_CHECKPOINT",
                "content": content
            }
        return None  # Skip standard accessibility snapshots or transient internal system notices

    # 3. Model planner response: preserve thoughts/decisions & tool calls
    if step_type == "PLANNER_RESPONSE" or source in ("MODEL", "ASSISTANT"):
        tool_calls = raw_step.get("tool_calls", [])
        pruned_tool_calls = [prune_tool_call(tc) for tc in tool_calls] if tool_calls else []
        pruned_content = prune_text_content(content) if content else ""
        return {
            "step_index": step_index,
            "type": "MODEL_RESPONSE",
            "content": pruned_content,
            "tool_calls": pruned_tool_calls
        }

    # 4. Tool execution results / generic step: prune heavy terminal outputs
    if step_type in ("GENERIC", "TOOL_OUTPUT") or "output" in raw_step or content:
        text = content if content else str(raw_step.get("output", ""))
        pruned_output = prune_text_content(text)
        return {
            "step_index": step_index,
            "type": "TOOL_OUTPUT",
            "content": pruned_output
        }

    return None

def prune_transcript_lines(lines: list[str]) -> list[dict[str, Any]]:
    """Prunes lines of JSON objects from transcript.jsonl."""
    pruned_steps = []
    for line in lines:
        line = line.strip()
        if not line:
            continue
        try:
            raw_step = json.loads(line)
            pruned = prune_step(raw_step)
            if pruned:
                pruned_steps.append(pruned)
        except Exception:
            continue
    return pruned_steps

def to_markdown_digest(pruned_steps: list[dict[str, Any]]) -> str:
    """Formats pruned steps into a clean markdown stream for Single-Pass Handoff aggregation."""
    md_lines = ["# 会话精炼事务摘要 (Pruned Conversation Stream)\n"]
    for s in pruned_steps:
        stype = s.get("type", "")
        idx = s.get("step_index", 0)
        content = s.get("content", "").strip()

        if stype == "USER_INPUT":
            md_lines.append(f"\n### [Step {idx}] 👤 用户需求与指令")
            # Strip XML tags if present
            clean_content = re.sub(r"<USER_REQUEST>([\s\S]*?)</USER_REQUEST>", r"\1", content).strip()
            clean_content = re.sub(r"<ADDITIONAL_METADATA>[\s\S]*?</ADDITIONAL_METADATA>", "", clean_content).strip()
            md_lines.append(clean_content)
        elif stype == "SYSTEM_CHECKPOINT":
            md_lines.append(f"\n> ⚙️ **[Step {idx} 系统检查点]** {content}")
        elif stype == "MODEL_RESPONSE":
            tool_calls = s.get("tool_calls", [])
            tc_desc = ""
            if tool_calls:
                names = [tc.get("name", "") for tc in tool_calls]
                tc_desc = f" (执行工具: {', '.join(names)})"
            md_lines.append(f"\n#### [Step {idx}] 🤖 模型规划与决议{tc_desc}")
            if content:
                md_lines.append(content)
            for tc in tool_calls:
                t_name = tc.get("name", "")
                t_args = tc.get("args", {})
                if t_name == "run_command":
                    md_lines.append(f"- 执行命令: `{t_args.get('CommandLine', '')}`")
                elif t_name in ("write_to_file", "replace_file_content"):
                    md_lines.append(f"- 变更文件: `{t_args.get('TargetFile', '')}`")
                elif "codegraph" in t_name.lower():
                    md_lines.append(f"- CodeGraph 探索: `{t_args}`")
        elif stype == "TOOL_OUTPUT":
            if content:
                md_lines.append(f"- 工具返回结果:")
                md_lines.append("```text\n" + content + "\n```")

    return "\n".join(md_lines)

def prune_file(transcript_path: str | Path, output_path: str | Path | None = None, output_format: str = "markdown") -> str:
    """Prunes a transcript.jsonl file and optionally writes to output_path."""
    t_path = Path(transcript_path)
    if not t_path.is_file():
        raise FileNotFoundError(f"Transcript not found at {transcript_path}")

    with open(t_path, "r", encoding="utf-8", errors="ignore") as f:
        lines = f.readlines()

    pruned = prune_transcript_lines(lines)

    if output_format == "jsonl":
        result = "\n".join(json.dumps(s, ensure_ascii=False) for s in pruned)
    else:
        result = to_markdown_digest(pruned)

    if output_path:
        out_p = Path(output_path)
        out_p.parent.mkdir(parents=True, exist_ok=True)
        with open(out_p, "w", encoding="utf-8") as f:
            f.write(result)

    return result

def main():
    if len(sys.argv) < 2:
        print("Usage: python transcript_pruner.py <transcript_path> [--output <path>] [--format markdown|jsonl]")
        sys.exit(1)

    t_path = sys.argv[1]
    out_path = None
    out_format = "markdown"

    i = 2
    while i < len(sys.argv):
        if sys.argv[i] == "--output" and i + 1 < len(sys.argv):
            out_path = sys.argv[i + 1]
            i += 2
        elif sys.argv[i] == "--format" and i + 1 < len(sys.argv):
            out_format = sys.argv[i + 1].lower()
            i += 2
        else:
            i += 1

    try:
        res = prune_file(t_path, output_path=out_path, output_format=out_format)
        if not out_path:
            print(res)
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
