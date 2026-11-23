"""
Unit tests for Transcript Pruner (transcript_pruner.py)
"""

import unittest
import json
import time
import tempfile
import shutil
from pathlib import Path

import transcript_pruner

class TestTranscriptPruner(unittest.TestCase):
    def setUp(self):
        self.test_dir = tempfile.mkdtemp()

    def tearDown(self):
        shutil.rmtree(self.test_dir, ignore_errors=True)

    def test_short_text_untouched(self):
        text = "line 1\nline 2\nline 3"
        self.assertEqual(transcript_pruner.prune_text_content(text, max_lines=10), text)

    def test_long_text_truncated_with_error_preserved(self):
        lines = [f"info log {i}" for i in range(100)]
        lines[45] = "FATAL: Connection refused to database port 5432"
        text = "\n".join(lines)
        pruned = transcript_pruner.prune_text_content(text, max_lines=30)
        self.assertIn("Pruned 80 lines of verbose output", pruned)
        self.assertIn("FATAL: Connection refused", pruned)
        self.assertIn("info log 0", pruned)
        self.assertIn("info log 99", pruned)

    def test_user_input_preserved(self):
        step = {
            "step_index": 1,
            "source": "USER_EXPLICIT",
            "type": "USER_INPUT",
            "content": "<USER_REQUEST>请优化项目交接机制</USER_REQUEST>"
        }
        res = transcript_pruner.prune_step(step)
        self.assertIsNotNone(res)
        self.assertEqual(res["type"], "USER_INPUT")
        self.assertIn("请优化项目交接机制", res["content"])

    def test_prune_file_markdown_and_performance(self):
        # Create a dummy transcript file with 100 steps
        t_path = Path(self.test_dir) / "transcript.jsonl"
        with open(t_path, "w", encoding="utf-8") as f:
            for i in range(50):
                # User step
                f.write(json.dumps({
                    "step_index": i * 2,
                    "source": "USER",
                    "type": "USER_INPUT",
                    "content": f"User question {i}"
                }) + "\n")
                # Tool/Model step with huge terminal output
                f.write(json.dumps({
                    "step_index": i * 2 + 1,
                    "source": "MODEL",
                    "type": "GENERIC",
                    "content": "\n".join([f"Compiler warning {j}" for j in range(80)])
                }) + "\n")

        # Test speed and output
        t0 = time.time()
        md_digest = transcript_pruner.prune_file(t_path, output_format="markdown")
        latency_ms = (time.time() - t0) * 1000

        self.assertIn("# 会话精炼事务摘要", md_digest)
        self.assertIn("User question 0", md_digest)
        self.assertIn("Pruned 60 lines", md_digest)
        # Verify latency is ultra-fast (< 50ms)
        self.assertLess(latency_ms, 100.0, f"Pruner took {latency_ms:.2f}ms, expected < 100ms")

if __name__ == "__main__":
    unittest.main()
