"""Small read-only regression checks on the actual100-input review bundle."""
import unittest
from pathlib import Path
from serve_pilot_experience_review import ROOT, load_store, byte_hash
from pilot_input_intake import canonical

class ActualPilotReviewTests(unittest.TestCase):
    def setUp(self):
        self.base = ROOT / "Artifacts/QA/Pilot100-20261011-b132-r1"
        self.manifest = self.base / "input-review-r1/manifest.json"
        self.policy = self.base / "unity-policy-r2/current-experience-review.json"
        self.policy_hash = byte_hash(self.policy.read_bytes())
        self.captured_hash = byte_hash((self.base / "input-review-r1/captured-importance.json").read_bytes())

    def test_all100_parse_and_keep_original_wire(self):
        store = load_store(self.manifest, self.policy, self.policy_hash, self.captured_hash)
        self.assertEqual(len(store.rows), 100)
        self.assertEqual(sum(r["presentation"]["parsed"] for r in store.rows), 100)
        self.assertEqual(store.review_context["completedAnswerPairs"], 0)
        self.assertFalse(store.read()["humanApprovalClaimed"])
        self.assertFalse(store.read()["trainingEligible"])
        from pilot_input_intake import loads
        original = {r["requestKey"]: r for r in (loads(line) for line in
            (self.base / "profile-100-r1/public-requests.jsonl").read_bytes().splitlines())}
        for row in store.rows:
            self.assertEqual(canonical(row["wire"]), canonical(original[row["requestKey"]]["wire"]))

    def test_raw_high_salience_is_not_a_strong_current_experience(self):
        store = load_store(self.manifest, self.policy, self.policy_hash, self.captured_hash)
        self.assertEqual(sum(r["importanceSummary"]["coreFactCount"] for r in store.rows), 0)
        self.assertTrue(any(f["capturedImportance"]["salience"] is not None
            and f["capturedImportance"]["salience"] >= .8
            for r in store.rows for f in r["presentation"]["facts"]))

    def test_character_context_is_not_a_missing_experience_score(self):
        store = load_store(self.manifest, self.policy, self.policy_hash, self.captured_hash)
        contexts = [f for r in store.rows for f in r["presentation"]["characterContext"]]
        self.assertEqual(len(contexts), 449)
        self.assertTrue(any(f["text"] == "Name: Roma" for f in contexts))
        self.assertTrue(any(f["text"] == "Species: Slime" for f in contexts))
        for row in store.rows:
            view = row["presentation"]
            self.assertEqual(len(view["facts"]), sum(len(view[k]) for k in
                ("events", "characterContext", "unknownFacts")))
            expected = sum(f["importance"]["salience"] is None for f in view["events"])
            self.assertEqual(row["importanceSummary"]["unknownSalienceCount"], expected + len(view["unknownFacts"]))

    def test_policy_and_captured_hash_mismatch_are_rejected(self):
        bad = "sha256:" + "0" * 64
        for policy_hash, captured_hash in ((bad, self.captured_hash), (self.policy_hash, bad)):
            with self.assertRaises(ValueError):
                load_store(self.manifest, self.policy, policy_hash, captured_hash)

if __name__ == "__main__":
    unittest.main()
