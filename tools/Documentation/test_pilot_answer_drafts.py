"""Checks at the historical-alias, allowed-module, and original-wire boundaries."""
import copy
import unittest
from build_pilot_answer_drafts import ROOT, read, validate_answer, validate_authored_text
from serve_pilot_experience_review import load_store, attach_answers, byte_hash

class AnswerDraftTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.base = ROOT / "Artifacts/QA/Pilot100-20261011-b133-r1"
        cls.inputs = read(cls.base / "authoring-inputs.json")["rows"]
        cls.bundle_path = cls.base / "review-drafts-r1/answers.json"
        cls.answers = read(cls.bundle_path)["rows"]

    def test_historical_alias_is_resolved_from_public_input(self):
        cards = validate_answer(self.inputs[50], self.answers[50])
        self.assertTrue(all(e["id"].startswith("public-fact:sha256:") for e in cards[0]["evidence"]))
        self.assertTrue(any("시설 훈련" in e["text"] for e in cards[0]["evidence"]))

    def test_offered_but_wrong_target_module_rejected(self):
        answer = copy.deepcopy(self.answers[0])
        answer["response"]["candidates"][0]["positiveModuleIds"] = ["damage"]
        with self.assertRaises(ValueError):
            validate_answer(self.inputs[0], answer)

    def test_unknown_evidence_rejected_even_in_old_permissive_schema(self):
        answer = copy.deepcopy(self.answers[50])
        answer["response"]["evidenceFactIds"] = ["public-fact:sha256:" + "0" * 64]
        with self.assertRaisesRegex(ValueError, "evidence"):
            validate_answer(self.inputs[50], answer)

    def test_wrong_original_wire_rejected(self):
        answer = copy.deepcopy(self.answers[0])
        answer["sourceWireSha256"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(ValueError, "source wire"):
            validate_answer(self.inputs[0], answer)

    def test_all_old_skill_candidates_fail_missing_identity(self):
        checked = 0
        for context, answer in zip(self.inputs, self.answers):
            if context["profile"] != "CharacterSkillModuleSelection":
                continue
            for candidate in answer["response"]["candidates"]:
                with self.assertRaisesRegex(ValueError, "exact character name"):
                    validate_authored_text(context, candidate)
                checked += 1
        self.assertEqual(checked, 117)

    def test_exact_numeric_proper_name_is_not_an_effect_number(self):
        context = dict(profile="CharacterSkillModuleSelection", wire=dict(messages=[dict(content="character=Ayun2")]))
        candidate = dict(displayName="익숙한 타격", narrativeFlavor="Ayun2는 훈련의 파문으로 제 타격을 다듬는다.")
        validate_authored_text(context, candidate)
        for text in ("Ayun2는 훈련으로 공격을 2배 높인다.", "Ayun23는 훈련의 파문을 배운다.",
                     "Ayun2는 힘을 낸다. 다시 힘을 낸다.", "이름 없는 수련자는 타격을 다듬는다."):
            with self.assertRaises(ValueError):
                validate_authored_text(context, dict(candidate, narrativeFlavor=text))

    def test_review_overlay_has100_answers_without_changing_input_or_approval(self):
        base = ROOT / "Artifacts/QA/Pilot100-20261011-b132-r1"
        policy = base / "unity-policy-r2/current-experience-review.json"
        store = load_store(base / "input-review-r1/manifest.json", policy, byte_hash(policy.read_bytes()),
                           byte_hash((base / "input-review-r1/captured-importance.json").read_bytes()))
        original = copy.deepcopy(store.rows)
        with self.assertRaises(ValueError):
            attach_answers(store, self.bundle_path, "sha256:" + "0" * 64)
        attach_answers(store, self.bundle_path, byte_hash(self.bundle_path.read_bytes()))
        self.assertEqual(sum(len(r["authoredAnswer"]["displayCandidates"]) for r in store.rows), 178)
        for before, after in zip(original, store.rows):
            self.assertEqual(before["wire"], after["wire"])
            self.assertIsNone(after["response"])
        self.assertEqual(store.read()["decisions"], {})
        self.assertFalse(store.read()["humanApprovalClaimed"])
        self.assertFalse(store.read()["trainingEligible"])

if __name__ == "__main__":
    unittest.main()
