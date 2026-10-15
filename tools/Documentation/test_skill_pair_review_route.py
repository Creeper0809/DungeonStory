"""Read-only skill-pair review package boundary checks."""
import tempfile
import unittest
from pathlib import Path

from serve_pilot_experience_review import (
    NARRATIVE_WORKING,
    ROOT,
    SKILL_PAIR_NAV_LABEL,
    add_skill_pair_navigation,
    byte_hash,
    load_skill_pair_review,
    require_optional_pair,
)


class SkillPairReviewRouteTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(
            prefix="SkillPairReviewTest-", dir=NARRATIVE_WORKING)
        self.package = Path(self.temporary.name)
        self.manifest = b'{"schema":"test-skill-pairs@1"}\n'
        self.html = b"<!doctype html><html><body>skill review</body></html>"
        self.dataset = b'{"pair":1,"trainingEligible":false,"humanApprovalClaimed":false}\n'
        (self.package / "manifest.json").write_bytes(self.manifest)
        (self.package / "index.html").write_bytes(self.html)
        (self.package / "dataset-drafts.jsonl").write_bytes(self.dataset)
        (self.package / "review.json").write_bytes(
            b'{"trainingEligible":false,"humanApprovalClaimed":false}\n')
        self.manifest_hash = byte_hash(self.manifest)

    def tearDown(self):
        self.temporary.cleanup()

    def test_optional_path_and_pin_must_be_supplied_together(self):
        for path, pin in ((self.package, None), (None, self.manifest_hash)):
            with self.subTest(path=path, pin=pin):
                with self.assertRaisesRegex(ValueError, "both required"):
                    require_optional_pair(path, pin, "skill pair")

    def test_package_outside_narrative_working_is_rejected_before_read(self):
        outside = ROOT / "Artifacts/QA/not-a-skill-pair-package"
        with self.assertRaisesRegex(ValueError, "outside NarrativeAI"):
            load_skill_pair_review(outside, self.manifest_hash, lambda _: None)

    def test_bad_manifest_pin_is_rejected_before_verification(self):
        verified = []
        with self.assertRaisesRegex(ValueError, "pin mismatch"):
            load_skill_pair_review(
                self.package, "sha256:" + "0" * 64, verified.append)
        self.assertEqual(verified, [])

    def test_allowed_package_is_verified_then_frozen_for_html_and_jsonl(self):
        verified = []

        def verify(path):
            self.assertTrue((path / "review.json").is_file())
            verified.append(path)

        html, dataset = load_skill_pair_review(
            self.package, self.manifest_hash, verify)
        self.assertEqual(verified, [self.package.resolve()])
        self.assertEqual(html, self.html)
        self.assertEqual(dataset, self.dataset)
        (self.package / "index.html").write_bytes(b"changed after startup")
        self.assertEqual(html, self.html)

    def test_navigation_links_to_read_only_skill_review_without_approval_claim(self):
        original = (NARRATIVE_WORKING / "PlausibleActions-20261011-r1/drafts-r1/index.html").read_bytes()
        result = add_skill_pair_navigation(original)
        self.assertIn(b'href="/skills"', result)
        self.assertIn(SKILL_PAIR_NAV_LABEL.encode("utf-8"), result)
        self.assertNotIn("승인 완료".encode("utf-8"), result)
        self.assertEqual(result.count(b"<article "), original.count(b"<article "))
        with self.assertRaisesRegex(ValueError, "unique main"):
            add_skill_pair_navigation(b"<main></main><main></main>")


if __name__ == "__main__":
    unittest.main()
