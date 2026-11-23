"""
Unit tests for Antigravity project-handoff hook.
"""

import unittest
import json
import tempfile
import shutil
from pathlib import Path

# Import module under test
import antigravity_hook

class TestAntigravityHook(unittest.TestCase):
    def setUp(self):
        self.test_dir = tempfile.mkdtemp()
        self.orig_get_state_dir = antigravity_hook.get_state_dir
        antigravity_hook.get_state_dir = lambda: Path(self.test_dir)

    def tearDown(self):
        antigravity_hook.get_state_dir = self.orig_get_state_dir
        shutil.rmtree(self.test_dir, ignore_errors=True)

    def test_pre_invocation_under_threshold(self):
        payload = {
            "conversationId": "test_under_thresh",
            "initialNumSteps": 40
        }
        res = antigravity_hook.handle_pre_invocation(payload)
        self.assertEqual(res, {"injectSteps": []})

    def test_pre_invocation_reaches_threshold(self):
        payload = {
            "conversationId": "test_reach_thresh",
            "initialNumSteps": 62
        }
        res = antigravity_hook.handle_pre_invocation(payload)
        self.assertEqual(len(res.get("injectSteps", [])), 1)
        msg = res["injectSteps"][0]["ephemeralMessage"]
        self.assertIn("[project-handoff 检查点]", msg)
        self.assertIn("62 步", msg)

    def test_transcript_filters_tool_and_ephemeral_noise(self):
        transcript = Path(self.test_dir) / "noisy_transcript.jsonl"
        entries = [{"source": "USER_EXPLICIT", "type": "USER_INPUT", "content": "Run full audit"}]
        for i in range(35):
            entries.append({"source": "MODEL", "type": "PLANNER_RESPONSE", "content": f"step {i}"})
            entries.append({"source": "MODEL", "type": "GENERIC", "content": "tool result line"})
            entries.append({"source": "SYSTEM_SDK", "type": "EPHEMERAL_MESSAGE", "content": "old hook nag"})
        with open(transcript, "w", encoding="utf-8") as f:
            for e in entries:
                f.write(json.dumps(e) + "\n")

        # Even though raw lines = 106 and initialNumSteps = 106, only 35 PLANNER_RESPONSE turns occurred (< 60)
        res = antigravity_hook.handle_pre_invocation({
            "conversationId": "test_noisy_transcript",
            "transcriptPath": str(transcript),
            "initialNumSteps": 106,
        })
        self.assertEqual(res, {"injectSteps": []})

        # Add 25 more PLANNER_RESPONSE turns -> 60 planner turns total -> triggers once
        with open(transcript, "a", encoding="utf-8") as f:
            for i in range(25):
                f.write(json.dumps({"source": "MODEL", "type": "PLANNER_RESPONSE", "content": f"more {i}"}) + "\n")
        res2 = antigravity_hook.handle_pre_invocation({
            "conversationId": "test_noisy_transcript",
            "transcriptPath": str(transcript),
            "initialNumSteps": 200,
        })
        self.assertEqual(len(res2.get("injectSteps", [])), 1)
        self.assertIn("60 轮模型规划", res2["injectSteps"][0]["ephemeralMessage"])

    def test_cooldown_behavior(self):
        cid = "test_cooldown"
        # 1. Trigger threshold at step 60
        res = antigravity_hook.handle_pre_invocation({"conversationId": cid, "initialNumSteps": 60})
        self.assertEqual(len(res.get("injectSteps", [])), 1)

        # 2. Simulate Stop hook recording handoff proposal
        dummy_transcript = Path(self.test_dir) / "transcript.jsonl"
        with open(dummy_transcript, "w", encoding="utf-8") as f:
            f.write(json.dumps({
                "source": "MODEL",
                "type": "PLANNER_RESPONSE",
                "content": "交接建议：当前第一阶段已收拢，进入下一阶段前建议换对话。"
            }) + "\n")

        antigravity_hook.handle_stop({
            "conversationId": cid,
            "transcriptPath": str(dummy_transcript)
        })

        # 3. Next pre-invocation at step 85 (should be cooled down, 85 - 60 < 40)
        res2 = antigravity_hook.handle_pre_invocation({"conversationId": cid, "initialNumSteps": 85})
        self.assertEqual(res2, {"injectSteps": []})

        # 4. Step 101 (101 - 60 >= 40, cooldown finished)
        res3 = antigravity_hook.handle_pre_invocation({"conversationId": cid, "initialNumSteps": 101})
        self.assertEqual(len(res3.get("injectSteps", [])), 1)

    def test_pre_invocation_cooldown_without_stop_proposal(self):
        cid = "test_no_proposal_cooldown"
        # 1. Trigger threshold at step 60
        res1 = antigravity_hook.handle_pre_invocation({"conversationId": cid, "initialNumSteps": 60})
        self.assertEqual(len(res1.get("injectSteps", [])), 1)

        # 2. Immediate next steps (61, 75, 99) without any Stop proposal must NOT spam
        for step in (61, 75, 99):
            res_mid = antigravity_hook.handle_pre_invocation({"conversationId": cid, "initialNumSteps": step})
            self.assertEqual(res_mid, {"injectSteps": []}, f"Expected no reminder at step {step}")

        # 3. Once cooldown (40 steps) has elapsed at step 100, reminder fires again
        res_after = antigravity_hook.handle_pre_invocation({"conversationId": cid, "initialNumSteps": 100})
        self.assertEqual(len(res_after.get("injectSteps", [])), 1)

    def test_user_mute(self):
        cid = "test_mute"
        dummy_transcript = Path(self.test_dir) / "transcript.jsonl"
        with open(dummy_transcript, "w", encoding="utf-8") as f:
            f.write(json.dumps({
                "source": "USER",
                "type": "USER_INPUT",
                "content": "好的，本任务不提醒了，继续执行"
            }) + "\n")
            for i in range(40):
                f.write(json.dumps({"source": "MODEL", "type": "GENERIC", "content": f"tool {i}"}) + "\n")

        antigravity_hook.handle_stop({
            "conversationId": cid,
            "transcriptPath": str(dummy_transcript)
        })

        # Even at high steps, must stay muted
        res = antigravity_hook.handle_pre_invocation({"conversationId": cid, "initialNumSteps": 120})
        self.assertEqual(res, {"injectSteps": []})

if __name__ == "__main__":
    unittest.main()
