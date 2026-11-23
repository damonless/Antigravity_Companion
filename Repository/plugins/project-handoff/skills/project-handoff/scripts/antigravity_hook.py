"""
Antigravity Project-Handoff Lifecycle Hook
Optimized for Google Antigravity & Gemini runtime.

Hooks supported:
- PreInvocation: counts logical user/planner turns (filtering GENERIC tool chatter and hook ephemerals),
  injects ephemeral reminder only when genuine long-session thresholds and cooldowns are met.
- Stop: audits model response for handoff proposals and user mute/snooze commands, updates state cleanly.
"""

import sys
import json
import os
import hashlib
from pathlib import Path
from datetime import datetime, timezone

if hasattr(sys.stdin, "reconfigure"):
    sys.stdin.reconfigure(encoding="utf-8")
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

# Logical turn thresholds (measured against PLANNER_RESPONSE / user turns, not raw JSONL lines)
INITIAL_STEP_THRESHOLD = 60
COOLDOWN_STEPS = 40

def get_state_dir() -> Path:
    base = Path.home() / ".gemini" / "antigravity" / "handoff_state"
    base.mkdir(parents=True, exist_ok=True)
    return base

def get_session_hash(conversation_id: str) -> str:
    return hashlib.sha256(conversation_id.encode("utf-8")).hexdigest()[:16]

def load_session_state(conversation_id: str) -> tuple[Path, dict]:
    state_dir = get_state_dir()
    session_hash = get_session_hash(conversation_id)
    state_path = state_dir / f"{session_hash}.json"
    if state_path.is_file():
        try:
            with open(state_path, "r", encoding="utf-8") as f:
                return state_path, json.load(f)
        except Exception:
            pass
    now = datetime.now(timezone.utc).isoformat()
    return state_path, {
        "conversation_id": conversation_id,
        "created_at": now,
        "last_updated_at": now,
        "step_count": 0,
        "last_reminded_step": 0,
        "muted": False,
        "cycle_count": 0
    }

def save_session_state(state_path: Path, state: dict) -> None:
    state["last_updated_at"] = datetime.now(timezone.utc).isoformat()
    tmp_path = state_path.with_suffix(".tmp")
    with open(tmp_path, "w", encoding="utf-8") as f:
        json.dump(state, f, indent=2, ensure_ascii=False)
    os.replace(tmp_path, state_path)

def analyze_transcript(transcript_path: str | None) -> tuple[int, int, int, int]:
    """Return (planner_turns, user_turns, raw_lines, size_bytes) from transcript JSONL."""
    if not transcript_path or not os.path.exists(transcript_path):
        return 0, 0, 0, 0
    try:
        size_bytes = os.path.getsize(transcript_path)
        planner_turns = 0
        user_turns = 0
        raw_lines = 0
        with open(transcript_path, "r", encoding="utf-8", errors="ignore") as f:
            for line in f:
                raw_lines += 1
                stripped = line.strip()
                if not stripped:
                    continue
                try:
                    entry = json.loads(stripped)
                except Exception:
                    continue
                step_type = str(entry.get("type", "")).upper()
                source = str(entry.get("source", "")).upper()
                if step_type == "USER_INPUT" or source == "USER_EXPLICIT":
                    user_turns += 1
                elif step_type == "PLANNER_RESPONSE" or (source == "MODEL" and step_type not in ("GENERIC", "EPHEMERAL_MESSAGE")):
                    planner_turns += 1
        return planner_turns, user_turns, raw_lines, size_bytes
    except Exception:
        return 0, 0, 0, 0

def count_transcript_steps(transcript_path: str | None) -> int:
    planner_turns, _, raw_lines, _ = analyze_transcript(transcript_path)
    return planner_turns if planner_turns > 0 else raw_lines

def handle_pre_invocation(payload: dict) -> dict:
    conversation_id = payload.get("conversationId", "default")
    transcript_path = payload.get("transcriptPath")
    initial_num_steps = int(payload.get("initialNumSteps", 0) or 0)

    state_path, state = load_session_state(conversation_id)
    has_transcript = bool(transcript_path and os.path.exists(transcript_path))
    if has_transcript:
        planner_turns, user_turns, raw_lines, _ = analyze_transcript(transcript_path)
        # Never allow inflated raw step indices (tool/ephemeral lines) to override logical planner turns
        current_steps = planner_turns if (planner_turns > 0 or user_turns > 0) else min(initial_num_steps, raw_lines)
    else:
        planner_turns, user_turns, raw_lines = 0, 0, 0
        current_steps = initial_num_steps

    state["step_count"] = current_steps
    save_session_state(state_path, state)

    if state.get("muted", False):
        return {"injectSteps": []}

    last_reminded = state.get("last_reminded_step", 0)
    should_remind = False

    if last_reminded == 0:
        if current_steps >= INITIAL_STEP_THRESHOLD:
            should_remind = True
    else:
        if current_steps - last_reminded >= COOLDOWN_STEPS:
            should_remind = True

    if should_remind:
        state["last_reminded_step"] = current_steps
        save_session_state(state_path, state)
        if has_transcript and (planner_turns > 0 or user_turns > 0):
            metric_label = f"累计 {user_turns} 轮用户输入 / {planner_turns} 轮模型规划"
        else:
            metric_label = f"累计 {current_steps} 步"
        msg = (
            f"[project-handoff 检查点] 当前会话达到长程评估阈值（{metric_label}）。"
            "若当前工作已处于安全收拢位置且具备明确下一步与切换收益，"
            "请依据 project-handoff 规范在回复第一段以「交接建议：...」格式向用户提出交接提议"
            "（说明切换原因、接续第一步及确认后将保存的动作）；若当前不处于安全收拢位置或属于纯问答/同阶段调优，请暂缓建议。"
        )
        return {
            "injectSteps": [
                {
                    "ephemeralMessage": msg
                }
            ]
        }
    return {"injectSteps": []}

def handle_stop(payload: dict) -> dict:
    conversation_id = payload.get("conversationId", "default")
    transcript_path = payload.get("transcriptPath")
    state_path, state = load_session_state(conversation_id)

    if not transcript_path or not os.path.exists(transcript_path):
        return {}

    try:
        # Read last 80 lines so high-density tool outputs don't hide user mute/snooze commands
        lines = []
        with open(transcript_path, "r", encoding="utf-8", errors="ignore") as f:
            for line in f:
                line = line.strip()
                if line:
                    lines.append(line)
        recent_lines = lines[-80:]

        has_proposal = False
        user_muted = False
        user_snoozed = False

        for l in reversed(recent_lines):
            try:
                entry = json.loads(l)
                content = entry.get("content", "") or ""
                source = entry.get("source", "") or ""
                step_type = entry.get("type", "") or ""

                # Check if assistant proposed handoff
                if ("PLANNER_RESPONSE" in step_type or source in ("MODEL", "ASSISTANT")) and not has_proposal:
                    if "交接建议：" in content or "交接建议:" in content:
                        has_proposal = True

                # Check user explicit control
                if "USER" in source or step_type == "USER_INPUT":
                    if any(k in content for k in ["本任务不提醒", "不再提醒", "关闭交接提醒"]):
                        user_muted = True
                    elif any(k in content for k in ["先不交接", "继续做", "暂不交接"]):
                        user_snoozed = True
            except Exception:
                continue

        current_steps = state.get("step_count", 0)
        if user_muted:
            state["muted"] = True
        elif user_snoozed:
            state["last_reminded_step"] = current_steps

        if has_proposal:
            state["last_reminded_step"] = current_steps
            state["cycle_count"] = state.get("cycle_count", 0) + 1

        save_session_state(state_path, state)
    except Exception:
        pass

    return {}

def main():
    if len(sys.argv) < 2:
        print(json.dumps({}))
        sys.exit(0)

    cmd = sys.argv[1].lower()
    try:
        input_data = sys.stdin.read()
        payload = json.loads(input_data) if input_data.strip() else {}
    except Exception:
        payload = {}

    if cmd == "pre-invocation":
        res = handle_pre_invocation(payload)
        print(json.dumps(res, ensure_ascii=False))
    elif cmd == "stop":
        res = handle_stop(payload)
        print(json.dumps(res, ensure_ascii=False))
    else:
        print(json.dumps({}))

if __name__ == "__main__":
    main()
