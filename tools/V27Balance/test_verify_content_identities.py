import json
from pathlib import Path
import subprocess
import tempfile
import unittest

import verify_content_identities as verifier


SCRIPT_GUID = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
SECOND_SCRIPT_GUID = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
ASSET_GUID = "11111111111111111111111111111111"
SECOND_ASSET_GUID = "22222222222222222222222222222222"
CHANGED_ASSET_GUID = "33333333333333333333333333333333"


class GitIdentityFixture(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.git("init", "-q")
        self.git("config", "user.email", "identity-gate@example.invalid")
        self.git("config", "user.name", "Identity Gate Tests")
        self.write_script()
        self.write_asset()
        self.initial = self.commit("content without published baseline")

    def git(self, *arguments: str) -> str:
        result = subprocess.run(
            ["git", *arguments],
            cwd=self.root,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        if result.returncode != 0:
            self.fail(
                f"git {' '.join(arguments)} failed ({result.returncode}): {result.stderr}"
            )
        return result.stdout.strip()

    def commit(self, message: str) -> str:
        self.git("add", "-A")
        self.git("commit", "-q", "-m", message)
        return self.git("rev-parse", "HEAD")

    def write_script(self, name: str = "TestSO", script_guid: str = SCRIPT_GUID) -> None:
        script = self.root / f"Assets/Scripts/{name}.cs"
        script.parent.mkdir(parents=True, exist_ok=True)
        script.write_text(f"public sealed class {name} {{}}\n", encoding="utf-8")
        script.with_suffix(".cs.meta").write_text(
            f"fileFormatVersion: 2\nguid: {script_guid}\n",
            encoding="utf-8",
        )

    def write_asset(
        self,
        relative_path: str = "Assets/Data/Test.asset",
        *,
        asset_guid: str = ASSET_GUID,
        script_guid: str = SCRIPT_GUID,
        fields: tuple[str, ...] = ("identity: item:alpha", "numericId: 7"),
    ) -> None:
        asset = self.root / relative_path
        asset.parent.mkdir(parents=True, exist_ok=True)
        serialized_fields = "\n".join(f"  {field}" for field in fields)
        asset.write_text(
            "%YAML 1.1\n"
            "%TAG !u! tag:unity3d.com,2011:\n"
            "--- !u!114 &11400000\n"
            "MonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: Test\n"
            f"{serialized_fields}\n",
            encoding="utf-8",
        )
        Path(str(asset) + ".meta").write_text(
            f"fileFormatVersion: 2\nguid: {asset_guid}\n",
            encoding="utf-8",
        )

    @staticmethod
    def asset_row(
        guid: str = ASSET_GUID,
        fields: tuple[tuple[str, str], ...] = (("identity", "item:alpha"),),
        type_name: str = "TestSO",
        script_guid: str = SCRIPT_GUID,
    ) -> dict[str, object]:
        return {
            "guid": guid,
            "typeName": type_name,
            "scriptGuid": script_guid,
            "fields": [{"path": path, "value": value} for path, value in fields],
        }

    def write_baseline(self, assets: list[dict[str, object]]) -> None:
        path = self.root / verifier.BASELINE_PATH
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            json.dumps(
                {"schemaVersion": 1, "assets": assets},
                ensure_ascii=False,
                indent=2,
            )
            + "\n",
            encoding="utf-8",
        )

    def publish_default(self) -> str:
        self.write_baseline([self.asset_row()])
        return self.commit("publish identity")


class RevisionVerificationTests(GitIdentityFixture):
    def test_first_baseline_introduction_is_allowed_and_all_rows_are_checked(self):
        head = self.publish_default()

        result = verifier.verify_revisions(self.initial, head, self.root)

        self.assertFalse(result["baseBaselinePresent"])
        self.assertEqual(1, result["assetsChecked"])
        self.assertEqual(1, result["fieldsChecked"])

    def test_new_fields_and_assets_are_allowed(self):
        base = self.publish_default()
        self.write_asset(
            "Assets/Data/Second.asset",
            asset_guid=SECOND_ASSET_GUID,
            fields=("identity: item:beta", "numericId: 8"),
        )
        self.write_baseline([
            self.asset_row(fields=(("identity", "item:alpha"), ("numericId", "7"))),
            self.asset_row(
                SECOND_ASSET_GUID,
                (("identity", "item:beta"), ("numericId", "8")),
            ),
        ])
        head = self.commit("add published identities")

        result = verifier.verify_revisions(base, head, self.root)

        self.assertEqual(2, result["assetsChecked"])
        self.assertEqual(4, result["fieldsChecked"])

    def test_changed_asset_identity_is_rejected_even_when_baseline_is_unchanged(self):
        base = self.publish_default()
        self.write_asset(fields=("identity: item:changed", "numericId: 7"))
        head = self.commit("mutate serialized identity")

        with self.assertRaisesRegex(
            verifier.VerificationError, "published identity does not match"
        ):
            verifier.verify_revisions(base, head, self.root)

    def test_deleted_zero_field_asset_is_rejected(self):
        self.write_baseline([self.asset_row(fields=())])
        base = self.commit("publish guid-only identity")
        (self.root / "Assets/Data/Test.asset").unlink()
        head = self.commit("delete asset but retain meta")

        with self.assertRaisesRegex(verifier.VerificationError, "asset is missing"):
            verifier.verify_revisions(base, head, self.root)

    def test_changed_guid_of_zero_field_asset_is_rejected(self):
        self.write_baseline([self.asset_row(fields=())])
        base = self.commit("publish guid-only identity")
        meta = self.root / "Assets/Data/Test.asset.meta"
        meta.write_text(
            f"fileFormatVersion: 2\nguid: {CHANGED_ASSET_GUID}\n",
            encoding="utf-8",
        )
        self.write_baseline([self.asset_row(CHANGED_ASSET_GUID, fields=())])
        head = self.commit("replace published asset guid")

        with self.assertRaisesRegex(verifier.VerificationError, "removed or GUID changed"):
            verifier.verify_revisions(base, head, self.root)

    def test_zero_field_asset_is_valid_and_still_checks_binding(self):
        self.write_baseline([self.asset_row(fields=())])
        head = self.commit("publish guid-only identity")

        result = verifier.verify_revisions(self.initial, head, self.root)

        self.assertEqual(1, result["assetsChecked"])
        self.assertEqual(0, result["fieldsChecked"])

    def test_embedded_non_script_subassets_do_not_duplicate_identity_fields(self):
        asset = self.root / "Assets/Data/Test.asset"
        header, mono_behaviour = asset.read_text(encoding="utf-8").split(
            "--- !u!114 &11400000\n", 1
        )
        asset.write_text(
            header
            + "--- !u!28 &-42\n"
            + "Texture2D:\n"
            + "  m_ObjectHideFlags: 0\n"
            + "  m_Name: EmbeddedTexture\n"
            + "--- !u!114 &11400000\n"
            + mono_behaviour
            + "--- !u!213 &43\n"
            + "Sprite:\n"
            + "  m_ObjectHideFlags: 0\n"
            + "  m_Name: EmbeddedSprite\n",
            encoding="utf-8",
        )
        self.write_baseline([self.asset_row()])
        head = self.commit("publish identity with embedded native subassets")

        result = verifier.verify_revisions(self.initial, head, self.root)

        self.assertEqual(1, result["assetsChecked"])

    def test_changed_script_guid_is_rejected_when_head_baseline_follows_it(self):
        base = self.publish_default()
        self.write_script("ReplacementSO", SECOND_SCRIPT_GUID)
        self.write_asset(script_guid=SECOND_SCRIPT_GUID)
        self.write_baseline([
            self.asset_row(type_name="ReplacementSO", script_guid=SECOND_SCRIPT_GUID)
        ])
        head = self.commit("replace published script binding")

        with self.assertRaisesRegex(
            verifier.VerificationError, "type changed|script GUID changed"
        ):
            verifier.verify_revisions(base, head, self.root)

    def test_asset_script_binding_must_match_head_baseline(self):
        base = self.publish_default()
        self.write_script("ReplacementSO", SECOND_SCRIPT_GUID)
        self.write_asset(script_guid=SECOND_SCRIPT_GUID)
        head = self.commit("mutate script binding without baseline")

        with self.assertRaisesRegex(verifier.VerificationError, "script binding does not match"):
            verifier.verify_revisions(base, head, self.root)

    def test_removed_baseline_row_is_rejected(self):
        self.write_asset(
            "Assets/Data/Second.asset",
            asset_guid=SECOND_ASSET_GUID,
            fields=("identity: item:beta",),
        )
        first = self.asset_row()
        second = self.asset_row(SECOND_ASSET_GUID, (("identity", "item:beta"),))
        self.write_baseline([first, second])
        base = self.commit("publish two identities")
        self.write_baseline([second])
        head = self.commit("remove one published row")

        with self.assertRaisesRegex(verifier.VerificationError, "published asset removed"):
            verifier.verify_revisions(base, head, self.root)

    def test_duplicate_identity_field_paths_are_rejected(self):
        row = self.asset_row(
            fields=(("identity", "item:alpha"), ("identity", "item:alpha"))
        )
        self.write_baseline([row])
        head = self.commit("duplicate baseline fields")

        with self.assertRaisesRegex(verifier.VerificationError, "duplicate identity field"):
            verifier.verify_revisions(self.initial, head, self.root)

    def test_asset_rename_retaining_guid_is_allowed(self):
        base = self.publish_default()
        destination = self.root / "Assets/Renamed/이름.asset"
        destination.parent.mkdir(parents=True)
        (self.root / "Assets/Data/Test.asset").rename(destination)
        (self.root / "Assets/Data/Test.asset.meta").rename(
            Path(str(destination) + ".meta")
        )
        head = self.commit("rename published asset")

        result = verifier.verify_revisions(base, head, self.root)

        self.assertEqual(1, result["assetsChecked"])

    def test_script_filename_may_differ_when_exact_partial_class_is_declared(self):
        base = self.publish_default()
        old_source = self.root / "Assets/Scripts/TestSO.cs"
        old_meta = self.root / "Assets/Scripts/TestSO.cs.meta"
        new_source = self.root / "Assets/Scripts/TestDefinition.cs"
        old_source.rename(new_source)
        old_meta.rename(new_source.with_suffix(".cs.meta"))
        new_source.write_text(
            "public sealed partial class TestSO {}\n",
            encoding="utf-8",
        )
        head = self.commit("move partial type to differently named source")

        result = verifier.verify_revisions(base, head, self.root)

        self.assertEqual(1, result["assetsChecked"])

    def test_bound_script_must_declare_exact_baseline_class(self):
        base = self.publish_default()
        (self.root / "Assets/Scripts/TestSO.cs").write_text(
            "// class TestSO must not satisfy the gate\n"
            'internal static class Note { const string Text = "class TestSO"; }\n'
            "public sealed partial class OtherSO {}\n",
            encoding="utf-8",
        )
        head = self.commit("remove published class declaration")

        with self.assertRaisesRegex(verifier.VerificationError, "no exact class declaration"):
            verifier.verify_revisions(base, head, self.root)

    def test_json_string_numeric_id_matches_unquoted_yaml_integer_scalar(self):
        self.write_asset(fields=("numericZero: 0", "numericId: 123"))
        self.write_baseline([
            self.asset_row(fields=(("numericZero", "0"), ("numericId", "123")))
        ])
        head = self.commit("publish numeric identities as strings")

        result = verifier.verify_revisions(self.initial, head, self.root)

        self.assertEqual(2, result["fieldsChecked"])

    def test_omitted_uninitialized_unity_string_matches_empty_identity(self):
        self.write_asset(fields=("numericId: 7",))
        self.write_baseline([self.asset_row(fields=(("archetypeId", ""),))])
        head = self.commit("publish omitted Unity null string")

        result = verifier.verify_revisions(self.initial, head, self.root)

        self.assertEqual(1, result["fieldsChecked"])

    def test_omitted_nonempty_identity_is_rejected(self):
        self.write_asset(fields=("numericId: 7",))
        self.write_baseline([self.asset_row(fields=(("archetypeId", "required"),))])
        head = self.commit("publish missing nonempty identity")

        with self.assertRaisesRegex(verifier.VerificationError, "identity field is missing"):
            verifier.verify_revisions(self.initial, head, self.root)

    def test_committed_head_is_used_instead_of_working_tree(self):
        base = self.publish_default()
        self.write_asset(fields=("identity: item:changed-only-in-worktree", "numericId: 7"))

        result = verifier.verify_revisions(self.initial, base, self.root)

        self.assertEqual(1, result["assetsChecked"])


class ScalarDecodingTests(unittest.TestCase):
    def test_unicode_quotes_empty_and_whitespace_are_decoded_exactly(self):
        cases = (
            ('"한글\\u0020\\U0001F600"', "한글 😀"),
            ('"emoji \\uD83D\\uDE00"', "emoji 😀"),
            ("'it''s exact'", "it's exact"),
            ("", ""),
            ("~", ""),
            ("null", ""),
            ("  keep both  ", "  keep both  "),
            ("0", "0"),
            ("123", "123"),
            ("-17", "-17"),
        )
        for raw, expected in cases:
            with self.subTest(raw=raw):
                self.assertEqual(
                    expected,
                    verifier.decode_identity_scalar(raw, "test.asset.identity"),
                )


if __name__ == "__main__":
    unittest.main()
