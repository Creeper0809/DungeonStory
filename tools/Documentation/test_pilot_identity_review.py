import copy
import json
import unittest
from build_pilot_identity_review import ROOT, identities, actor_display
from serve_pilot_experience_review import load_store, attach_answers, attach_identity, byte_hash

class NameRoleReviewTests(unittest.TestCase):
    def test_name_replacement_requires_the_bound_actor_prefix(self):
        text = "슬라임 사장은 허수아비를 이용했다."
        self.assertEqual(actor_display(text, "슬라임 사장", "Leon"), "Leon: 허수아비를 이용했다.")
        self.assertEqual(actor_display(text, "다른 인물", "Leon"), text)
        self.assertEqual(actor_display("사장이 Leon에게 말했다.", "Leon", "Ruka"), "사장이 Leon에게 말했다.")

    def test_conflicting_saved_personal_name_is_rejected(self):
        actor = dict(persistentId="owner", displayName="Leon", growth=dict(displayName="Dion"),
            role=1, characterType=0, isOwner=True)
        with self.assertRaisesRegex(ValueError, "personal name"):
            identities(dict(sections=[dict(payloadJson=json.dumps(dict(actors=[actor])))]))

    def test_actual100_overlay_preserves_wire_and_separates_name_role(self):
        base = ROOT / "Artifacts/QA/Pilot100-20261011-b132-r1"
        policy = base / "unity-policy-r2/current-experience-review.json"
        store = load_store(base / "input-review-r1/manifest.json", policy, byte_hash(policy.read_bytes()),
            byte_hash((base / "input-review-r1/captured-importance.json").read_bytes()))
        answers = ROOT / "Artifacts/QA/Pilot100-20261011-b133-r1/review-drafts-r1/answers.json"
        attach_answers(store, answers, byte_hash(answers.read_bytes()))
        before = copy.deepcopy([r["wire"] for r in store.rows])
        source = ROOT / "Artifacts/QA/Pilot100-20261011-b134-r1/identity-r2/identity-review.json"
        attach_identity(store, source, byte_hash(source.read_bytes()))
        self.assertEqual(before, [r["wire"] for r in store.rows])
        row = store.rows[79]
        self.assertEqual(row["presentation"]["facts"][0]["displayText"], "Leon: 훈련허수아비 이용을 마쳤다.")
        person = row["identityReview"]["characters"][0]
        self.assertEqual((person["name"], person["roleLabel"]), ("Leon", "사장"))
        self.assertEqual(row["authoredAnswer"]["displayCandidates"][1]["evidence"][0]["text"],
            "Leon: 훈련허수아비 이용을 마쳤다.")
        for item in store.rows:
            self.assertEqual(item["identityReview"]["preparedWire"]["messages"][:-1], item["wire"]["messages"])
        self.assertFalse(store.read()["humanApprovalClaimed"])

if __name__ == "__main__":
    unittest.main()
