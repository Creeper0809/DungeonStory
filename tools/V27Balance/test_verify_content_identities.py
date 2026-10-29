import json
from pathlib import Path
import subprocess
import sys
import tempfile
import types
import unittest
from unittest import mock

import verify_content_identities as verifier


SCRIPT_GUID = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
SECOND_SCRIPT_GUID = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
ASSET_GUID = "11111111111111111111111111111111"
SECOND_ASSET_GUID = "22222222222222222222222222222222"
CHANGED_ASSET_GUID = "33333333333333333333333333333333"
ROOT_ASSET_GUID = "44444444444444444444444444444444"
ITEM_CATALOG_GUID = "55555555555555555555555555555555"
DOMAIN_CATALOG_GUID = "66666666666666666666666666666666"
UNPUBLISHED_ASSET_GUID = "77777777777777777777777777777777"
VENDOR_ASSET_GUID = "88888888888888888888888888888888"
VENDOR_SCRIPT_GUID = "ffffffffffffffffffffffffffffffff"
ROOT_SCRIPT_GUID = "cccccccccccccccccccccccccccccccc"
ITEM_CATALOG_SCRIPT_GUID = "dddddddddddddddddddddddddddddddd"
DOMAIN_CATALOG_SCRIPT_GUID = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"
PRODUCTION_FEATURE_ALPHA_PATH = (
    "@nested:abilityModules.items[featureTags=feature%3Aalpha;"
    "type=BuildingProductionSupportAbility].featureTags"
)
PRODUCTION_FEATURE_BETA_PATH = (
    "@nested:abilityModules.items[featureTags=feature%3Abeta;"
    "type=BuildingProductionSupportAbility].featureTags"
)
COMBAT_ABILITY_ALPHA_PATH = (
    "@nested:combatAbilities.abilities[id=ability%3Aalpha].id"
)
COMBAT_ABILITY_BETA_PATH = (
    "@nested:combatAbilities.abilities[id=ability%3Abeta].id"
)
AMMUNITION_ARROW_PATH = (
    "@nested:features[ammunitionKindId=ammo%3Aarrow;"
    "type=AmmunitionItemFeature].ammunitionKindId"
)
SUBSTANCE_TONIC_PATH = (
    "@nested:features[substanceId=substance%3Atonic;"
    "type=SubstanceItemFeature].substanceId"
)


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

    def write_building_provider_asset(self, feature_tags: tuple[str, ...]) -> None:
        source = self.root / "Assets/Scripts/TestSO.cs"
        source.write_text("public sealed class BuildingSO {}\n", encoding="utf-8")
        feature_lines = (
            ("      featureTags:",)
            + tuple(f"      - {tag}" for tag in feature_tags)
            if feature_tags
            else ("      featureTags: []",)
        )
        self.write_asset(
            fields=(
                "abilityModules:",
                "  items:",
                "  - rid: 10",
                "references:",
                "  RefIds:",
                "  - rid: 10",
                "    type: {class: BuildingProductionSupportAbility, "
                "ns: '', asm: Assembly-CSharp}",
                "    data:",
            )
            + feature_lines,
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

    @staticmethod
    def registration_field(member: str, guid: str) -> tuple[str, str]:
        return (f"@registration:{member}:{guid}", guid)

    def write_registry_fixture(self) -> None:
        self.write_script("GameContentCatalogSO", ROOT_SCRIPT_GUID)
        self.write_script("ItemDefinitionCatalogSO", ITEM_CATALOG_SCRIPT_GUID)
        self.write_script("GameDomainContentCatalogSO", DOMAIN_CATALOG_SCRIPT_GUID)
        self.write_asset(
            "Assets/Data/Second.asset",
            asset_guid=SECOND_ASSET_GUID,
            fields=("identity: item:beta",),
        )
        self.write_asset(
            "Assets/Data/Third.asset",
            asset_guid=CHANGED_ASSET_GUID,
            fields=("identity: item:gamma",),
        )
        self.write_asset(
            verifier.ROOT_ASSET_PATH,
            asset_guid=ROOT_ASSET_GUID,
            script_guid=ROOT_SCRIPT_GUID,
            fields=(
                "itemDefinitions: {fileID: 11400000, guid: "
                f"{ITEM_CATALOG_GUID}, type: 2}}",
                "worldPresentation: {fileID: 11400000, guid: "
                f"{ASSET_GUID}, type: 2}}",
                "characterSkillSettings: {fileID: 11400000, guid: "
                f"{SECOND_ASSET_GUID}, type: 2}}",
                "media: {fileID: 11400000, guid: "
                f"{CHANGED_ASSET_GUID}, type: 2}}",
                "domainCatalogs:",
                "- {fileID: 11400000, guid: "
                f"{DOMAIN_CATALOG_GUID}, type: 2}}",
            ),
        )
        self.write_asset(
            "Assets/Data/ItemDefinitionCatalog.asset",
            asset_guid=ITEM_CATALOG_GUID,
            script_guid=ITEM_CATALOG_SCRIPT_GUID,
            fields=(
                "definitions:",
                f"- {{fileID: 11400000, guid: {ASSET_GUID}, type: 2}}",
                f"- {{fileID: 11400000, guid: {SECOND_ASSET_GUID}, type: 2}}",
            ),
        )
        self.write_asset(
            "Assets/Data/GameDomainContentCatalog.asset",
            asset_guid=DOMAIN_CATALOG_GUID,
            script_guid=DOMAIN_CATALOG_SCRIPT_GUID,
            fields=(
                "definitions:",
                f"- {{fileID: 11400000, guid: {SECOND_ASSET_GUID}, type: 2}}",
                f"- {{fileID: 11400000, guid: {CHANGED_ASSET_GUID}, type: 2}}",
                "metaUpgrades: []",
                "runVariables: []",
                "ownerDoctrines: []",
                "invasionPatterns: []",
                "characterNeeds: []",
            ),
        )

    def registry_rows(self) -> list[dict[str, object]]:
        return [
            self.asset_row(
                ROOT_ASSET_GUID,
                (
                    ("@resource-path", verifier.ROOT_ASSET_PATH),
                    self.registration_field("itemDefinitions", ITEM_CATALOG_GUID),
                    self.registration_field("worldPresentation", ASSET_GUID),
                    self.registration_field("characterSkillSettings", SECOND_ASSET_GUID),
                    self.registration_field("media", CHANGED_ASSET_GUID),
                    self.registration_field("domainCatalogs", DOMAIN_CATALOG_GUID),
                ),
                "GameContentCatalogSO",
                ROOT_SCRIPT_GUID,
            ),
            self.asset_row(
                ITEM_CATALOG_GUID,
                (
                    self.registration_field("definitions", ASSET_GUID),
                    self.registration_field("definitions", SECOND_ASSET_GUID),
                ),
                "ItemDefinitionCatalogSO",
                ITEM_CATALOG_SCRIPT_GUID,
            ),
            self.asset_row(
                DOMAIN_CATALOG_GUID,
                (
                    self.registration_field("definitions", SECOND_ASSET_GUID),
                    self.registration_field("definitions", CHANGED_ASSET_GUID),
                ),
                "GameDomainContentCatalogSO",
                DOMAIN_CATALOG_SCRIPT_GUID,
            ),
            self.asset_row(),
            self.asset_row(
                SECOND_ASSET_GUID,
                (("identity", "item:beta"),),
            ),
            self.asset_row(
                CHANGED_ASSET_GUID,
                (("identity", "item:gamma"),),
            ),
        ]

    def append_domain_registration(self, guid: str) -> None:
        catalog = self.root / "Assets/Data/GameDomainContentCatalog.asset"
        marker = "  metaUpgrades: []\n"
        text = catalog.read_text(encoding="utf-8")
        self.assertIn(marker, text)
        catalog.write_text(
            text.replace(
                marker,
                f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n" + marker,
                1,
            ),
            encoding="utf-8",
        )

    def registry_rows_with_domain_registration(self, guid: str) -> list[dict[str, object]]:
        rows = self.registry_rows()
        rows[2]["fields"].append(
            {
                "path": f"@registration:definitions:{guid}",
                "value": guid,
            }
        )
        return rows

    def publish_registry_fixture(self) -> str:
        self.write_registry_fixture()
        self.write_baseline(self.registry_rows())
        return self.commit("publish catalog registration identities")


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

    def test_new_blank_field_on_published_asset_is_rejected(self):
        base = self.publish_default()
        self.write_asset(fields=("identity: item:alpha", "newId:"))
        self.write_baseline([
            self.asset_row(fields=(("identity", "item:alpha"), ("newId", "")))
        ])
        head = self.commit("append blank published identity")

        with self.assertRaisesRegex(
            verifier.VerificationError, "new published identity field is blank"
        ):
            verifier.verify_revisions(base, head, self.root)

    def test_new_nonblank_field_and_existing_blank_field_are_allowed(self):
        self.write_asset(fields=("identity: item:alpha", "legacyId:"))
        self.write_baseline([
            self.asset_row(fields=(("identity", "item:alpha"), ("legacyId", "")))
        ])
        base = self.commit("publish legacy blank identity")
        self.write_asset(
            fields=("identity: item:alpha", "legacyId:", "newId: item:new")
        )
        self.write_baseline([
            self.asset_row(
                fields=(
                    ("identity", "item:alpha"),
                    ("legacyId", ""),
                    ("newId", "item:new"),
                )
            )
        ])
        head = self.commit("append nonblank published identity")

        result = verifier.verify_revisions(base, head, self.root)

        self.assertEqual(3, result["fieldsChecked"])

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

    def test_catalog_virtual_fields_match_current_yaml_and_ignore_array_order(self):
        base = self.publish_registry_fixture()
        item_catalog = self.root / "Assets/Data/ItemDefinitionCatalog.asset"
        text = item_catalog.read_text(encoding="utf-8")
        first = f"  - {{fileID: 11400000, guid: {ASSET_GUID}, type: 2}}"
        second = f"  - {{fileID: 11400000, guid: {SECOND_ASSET_GUID}, type: 2}}"
        item_catalog.write_text(
            text.replace(first + "\n" + second, second + "\n" + first),
            encoding="utf-8",
        )
        head = self.commit("reorder catalog definitions")

        result = verifier.verify_revisions(base, head, self.root)

        self.assertEqual(6, result["assetsChecked"])
        self.assertEqual(13, result["fieldsChecked"])

    def test_registered_target_move_with_preserved_meta_guid_is_allowed(self):
        base = self.publish_registry_fixture()
        destination = self.root / "Assets/Moved/GameDomainContentCatalog.asset"
        destination.parent.mkdir(parents=True)
        (self.root / "Assets/Data/GameDomainContentCatalog.asset").rename(destination)
        (self.root / "Assets/Data/GameDomainContentCatalog.asset.meta").rename(
            Path(str(destination) + ".meta")
        )
        head = self.commit("move registered target with its meta")

        result = verifier.verify_revisions(base, head, self.root)

        self.assertEqual(6, result["assetsChecked"])

    def test_registered_game_asset_requires_its_own_baseline_row(self):
        base = self.publish_registry_fixture()
        self.write_asset(
            "Assets/Data/Unpublished.asset",
            asset_guid=UNPUBLISHED_ASSET_GUID,
            fields=("identity: item:unpublished",),
        )
        self.append_domain_registration(UNPUBLISHED_ASSET_GUID)
        self.write_baseline(
            self.registry_rows_with_domain_registration(UNPUBLISHED_ASSET_GUID)
        )
        head = self.commit("register game asset without publishing its identity")

        with self.assertRaisesRegex(
            verifier.VerificationError,
            "registered eligible game ScriptableObject is missing from identity baseline",
        ):
            verifier.verify_revisions(base, head, self.root)

    def test_registered_vendor_asset_does_not_require_a_baseline_row(self):
        base = self.publish_registry_fixture()
        vendor_script = self.root / "Assets/Plugins/Demigiant/DOTween.dll"
        vendor_script.parent.mkdir(parents=True, exist_ok=True)
        vendor_script.write_bytes(b"vendor fixture")
        Path(str(vendor_script) + ".meta").write_text(
            f"fileFormatVersion: 2\nguid: {VENDOR_SCRIPT_GUID}\n",
            encoding="utf-8",
        )
        self.write_asset(
            "Assets/Data/VendorSettings.asset",
            asset_guid=VENDOR_ASSET_GUID,
            script_guid=VENDOR_SCRIPT_GUID,
            fields=("setting: enabled",),
        )
        self.append_domain_registration(VENDOR_ASSET_GUID)
        self.write_baseline(
            self.registry_rows_with_domain_registration(VENDOR_ASSET_GUID)
        )
        head = self.commit("register vendor settings without publishing an identity")

        result = verifier.verify_revisions(base, head, self.root)

        self.assertEqual(6, result["assetsChecked"])
        self.assertEqual(14, result["fieldsChecked"])

    def test_registered_target_script_guid_must_resolve(self):
        base = self.publish_registry_fixture()
        self.write_asset(
            "Assets/Data/BrokenSettings.asset",
            asset_guid=UNPUBLISHED_ASSET_GUID,
            script_guid=VENDOR_SCRIPT_GUID,
            fields=("setting: enabled",),
        )
        self.append_domain_registration(UNPUBLISHED_ASSET_GUID)
        self.write_baseline(
            self.registry_rows_with_domain_registration(UNPUBLISHED_ASSET_GUID)
        )
        head = self.commit("register target with an unresolved script guid")

        with self.assertRaisesRegex(
            verifier.VerificationError,
            "registered target m_Script GUID does not resolve",
        ):
            verifier.verify_revisions(base, head, self.root)

    def test_registered_target_script_binding_must_be_unambiguous(self):
        base = self.publish_registry_fixture()
        self.write_asset(
            "Assets/Data/AmbiguousSettings.asset",
            asset_guid=UNPUBLISHED_ASSET_GUID,
            fields=(
                "m_Script: {fileID: 11500000, guid: "
                f"{SECOND_SCRIPT_GUID}, type: 3}}",
                "setting: enabled",
            ),
        )
        self.append_domain_registration(UNPUBLISHED_ASSET_GUID)
        self.write_baseline(
            self.registry_rows_with_domain_registration(UNPUBLISHED_ASSET_GUID)
        )
        head = self.commit("register target with ambiguous script bindings")

        with self.assertRaisesRegex(
            verifier.VerificationError,
            "registered target m_Script binding is ambiguous",
        ):
            verifier.verify_revisions(base, head, self.root)

    def test_startup_catalog_move_is_rejected_even_when_guid_is_preserved(self):
        base = self.publish_registry_fixture()
        source = self.root / verifier.ROOT_ASSET_PATH
        destination = source.with_name("MovedGameContentCatalog.asset")
        source.rename(destination)
        Path(str(source) + ".meta").rename(Path(str(destination) + ".meta"))
        head = self.commit("move startup catalog with its meta")

        with self.assertRaisesRegex(
            verifier.VerificationError, "startup content catalog must remain"
        ):
            verifier.verify_revisions(base, head, self.root)

    def test_domain_registration_removal_is_rejected_while_target_remains(self):
        base = self.publish_registry_fixture()
        catalog = self.root / "Assets/Data/GameDomainContentCatalog.asset"
        removed = (
            f"  - {{fileID: 11400000, guid: {CHANGED_ASSET_GUID}, type: 2}}\n"
        )
        catalog.write_text(
            catalog.read_text(encoding="utf-8").replace(removed, ""),
            encoding="utf-8",
        )
        head = self.commit("remove domain registration but retain target")

        with self.assertRaisesRegex(
            verifier.VerificationError, "catalog registrations do not match"
        ):
            verifier.verify_revisions(base, head, self.root)

    def test_item_registration_removal_is_rejected_while_target_remains(self):
        base = self.publish_registry_fixture()
        catalog = self.root / "Assets/Data/ItemDefinitionCatalog.asset"
        removed = (
            f"  - {{fileID: 11400000, guid: {SECOND_ASSET_GUID}, type: 2}}\n"
        )
        catalog.write_text(
            catalog.read_text(encoding="utf-8").replace(removed, ""),
            encoding="utf-8",
        )
        head = self.commit("remove item registration but retain target")

        with self.assertRaisesRegex(
            verifier.VerificationError, "catalog registrations do not match"
        ):
            verifier.verify_revisions(base, head, self.root)

    def test_malformed_registration_reference_is_rejected(self):
        base = self.publish_registry_fixture()
        catalog = self.root / "Assets/Data/ItemDefinitionCatalog.asset"
        catalog.write_text(
            catalog.read_text(encoding="utf-8").replace(
                f"guid: {SECOND_ASSET_GUID}, type: 2",
                f"guid: {SECOND_ASSET_GUID}, type: 3",
            ),
            encoding="utf-8",
        )
        head = self.commit("malform item registration reference")

        with self.assertRaisesRegex(
            verifier.VerificationError, "registration array has invalid element"
        ):
            verifier.verify_revisions(base, head, self.root)

    def test_virtual_registration_path_syntax_is_strict(self):
        self.write_registry_fixture()
        rows = self.registry_rows()
        rows[1]["fields"][0]["path"] = "@registration:definitions:not-a-guid"
        self.write_baseline(rows)
        head = self.commit("write malformed virtual registration path")

        with self.assertRaisesRegex(verifier.VerificationError, "invalid virtual identity path"):
            verifier.verify_revisions(self.initial, head, self.root)

    def test_virtual_registration_member_is_restricted_by_catalog_type(self):
        self.write_registry_fixture()
        rows = self.registry_rows()
        rows[1]["fields"][0] = {
            "path": f"@registration:domainCatalogs:{ASSET_GUID}",
            "value": ASSET_GUID,
        }
        self.write_baseline(rows)
        head = self.commit("write wrong-kind virtual registration")

        with self.assertRaisesRegex(verifier.VerificationError, "is invalid for"):
            verifier.verify_revisions(self.initial, head, self.root)

    def test_nested_identity_reorder_is_allowed_but_rename_is_rejected(self):
        source = self.root / "Assets/Scripts/TestSO.cs"
        source.write_text("public sealed class AnatomyProfileSO {}\n", encoding="utf-8")
        self.write_asset(fields=("nodes:", "- nodeId: left-arm", "- nodeId: right-arm"))
        fields = (
            ("@nested:nodes[nodeId=left-arm].nodeId", "left-arm"),
            ("@nested:nodes[nodeId=right-arm].nodeId", "right-arm"),
            ("@nested:nodes[nodeId=left-arm].pairedGroupId", "0:"),
            ("@nested:nodes[nodeId=right-arm].pairedGroupId", "0:"),
        )
        self.write_baseline([self.asset_row(fields=fields, type_name="AnatomyProfileSO")])
        base = self.commit("publish nested anatomy identities")

        self.write_asset(fields=("nodes:", "- nodeId: right-arm", "- nodeId: left-arm"))
        reordered = self.commit("reorder anatomy nodes")
        result = verifier.verify_revisions(base, reordered, self.root)
        self.assertEqual(4, result["fieldsChecked"])

        self.write_asset(fields=("nodes:", "- nodeId: right-arm", "- nodeId: left-hand"))
        renamed = self.commit("rename anatomy node")
        with self.assertRaisesRegex(verifier.VerificationError, "nested identities do not match"):
            verifier.verify_revisions(base, renamed, self.root)

    def test_trait_display_rename_is_allowed_but_legacy_alias_removal_is_rejected(self):
        source = self.root / "Assets/Scripts/TestSO.cs"
        source.write_text("public sealed class CharacterTraitSO {}\n", encoding="utf-8")
        self.write_asset(fields=("traitName: Old Name", "combatAbilities:", "  abilities: []",
                                 "identityRules: []", "legacyPolicyNames:", "- Old Name"))
        fields = (("@legacy-trait-name:Old%20Name", "Old Name"),)
        self.write_baseline([self.asset_row(fields=fields, type_name="CharacterTraitSO")])
        base = self.commit("publish frozen trait policy alias")

        self.write_asset(fields=("traitName: New Name", "combatAbilities:", "  abilities: []",
                                 "identityRules: []", "legacyPolicyNames:", "- Old Name"))
        renamed = self.commit("rename trait display name")
        result = verifier.verify_revisions(base, renamed, self.root)
        self.assertEqual(1, result["fieldsChecked"])

        self.write_asset(fields=("traitName: New Name", "combatAbilities:", "  abilities: []",
                                 "identityRules: []", "legacyPolicyNames: []"))
        removed = self.commit("remove frozen trait policy alias")
        with self.assertRaisesRegex(verifier.VerificationError, "nested identities do not match"):
            verifier.verify_revisions(base, removed, self.root)

    def test_trait_saved_rule_rename_and_omitted_list_are_rejected(self):
        (self.root / "Assets/Scripts/TestSO.cs").write_text(
            "public sealed class CharacterTraitSO {}\n", encoding="utf-8")
        def rules(key):
            return ("combatAbilities:", "  abilities: []",
                    "identityRules:", "- rid: 10", "references:", "  RefIds:",
                    "  - rid: 10", "    type: {class: LastStandRule, ns: '', asm: Assembly-CSharp}",
                    "    data:", "      ruleId: " + key)
        self.write_asset(fields=rules("extreme:last-stand"))
        self.write_baseline([self.asset_row(
            fields=(("@nested:identityRules[ruleId=extreme%3Alast-stand].ruleId", "extreme:last-stand"),),
            type_name="CharacterTraitSO")])
        base = self.commit("publish saved trait rule")
        verifier.verify_revisions(base, base, self.root)
        for fields in (rules("extreme:renamed"),
                       ("traitName: Legacy Empty", "combatAbilities:", "  abilities: []")):
            self.write_asset(fields=fields)
            changed = self.commit("change published trait rules")
            with self.assertRaisesRegex(verifier.VerificationError, "nested identities do not match"):
                verifier.verify_revisions(base, changed, self.root)

    def test_published_building_provider_rename_and_delete_are_rejected(self):
        self.write_building_provider_asset(("feature:alpha",))
        self.write_baseline([self.asset_row(
            fields=((PRODUCTION_FEATURE_ALPHA_PATH, "feature:alpha"),),
            type_name="BuildingSO",
        )])
        base = self.commit("publish building provider identity")

        self.write_building_provider_asset(("feature:renamed",))
        renamed = self.commit("rename building provider identity")
        with self.assertRaisesRegex(
            verifier.VerificationError, "nested identities do not match"
        ):
            verifier.verify_revisions(base, renamed, self.root)

        self.write_building_provider_asset(())
        deleted = self.commit("delete building provider identity")
        with self.assertRaisesRegex(
            verifier.VerificationError, "nested identities do not match"
        ):
            verifier.verify_revisions(base, deleted, self.root)

    def test_explicit_new_building_provider_append_is_allowed(self):
        self.write_building_provider_asset(("feature:alpha",))
        self.write_baseline([self.asset_row(
            fields=((PRODUCTION_FEATURE_ALPHA_PATH, "feature:alpha"),),
            type_name="BuildingSO",
        )])
        base = self.commit("publish one building provider identity")

        self.write_building_provider_asset(("feature:alpha", "feature:beta"))
        self.write_baseline([self.asset_row(
            fields=(
                (PRODUCTION_FEATURE_ALPHA_PATH, "feature:alpha"),
                (PRODUCTION_FEATURE_BETA_PATH, "feature:beta"),
            ),
            type_name="BuildingSO",
        )])
        head = self.commit("append building provider identity")

        result = verifier.verify_revisions(base, head, self.root)

        self.assertEqual(2, result["fieldsChecked"])


class NestedIdentityExtractionTests(unittest.TestCase):
    def test_scripted_yaml_accepts_lf_and_crlf_document_markers(self):
        body = (
            "%YAML 1.1\n"
            "%TAG !u! tag:unity3d.com,2011:\n"
            + self.document(
                "  combatAbilities:\n"
                "    abilities:\n"
                "    - rid: 10\n"
                "  references:\n"
                "    RefIds:\n"
                "    - rid: 10\n"
                "      type: {class: CharacterCombatAbilityDefinition, ns: '', "
                "asm: DungeonStory.Combat}\n"
                "      data: {id: ability:alpha}\n"
            )
        )
        for newline in ("\n", "\r\n"):
            with self.subTest(newline=repr(newline)):
                raw = body.replace("\n", newline).encode("utf-8")
                document, script_guid = verifier._scripted_document(
                    raw,
                    "fixture.asset",
                    SCRIPT_GUID,
                )
                self.assertEqual(SCRIPT_GUID, script_guid)
                self.assertEqual(
                    {COMBAT_ABILITY_ALPHA_PATH: "ability:alpha"},
                    verifier._actual_nested_fields(
                        document,
                        self.record("CharacterSpeciesSO"),
                        "fixture.asset",
                    ),
                )

    def test_scripted_yaml_still_rejects_malformed_document_markers(self):
        payload = (
            "MonoBehaviour:\n"
            "  m_Script: {fileID: 11500000, guid: " + SCRIPT_GUID + ", type: 3}\n"
        )
        for marker in (
            "--- !u!114 &11400000 ",
            "--- !u!114 &not-a-file-id",
            "--- !u!114 &11400000\r\r",
        ):
            with self.subTest(marker=repr(marker)), self.assertRaisesRegex(
                verifier.VerificationError,
                "document marker is malformed",
            ):
                verifier._load_scripted_yaml(marker + "\n" + payload, "fixture.asset")

    def test_legacy_trait_without_rule_list_has_no_saved_rule_identity(self):
        self.assertEqual(
            {},
            self.extract(
                "CharacterTraitSO",
                "  traitName: Legacy Empty\n"
                "  combatAbilities:\n"
                "    abilities: []\n",
            ),
        )

    def test_trait_saved_rule_identity_is_captured(self):
        actual = self.extract("CharacterTraitSO",
            "  combatAbilities:\n    abilities: []\n"
            "  identityRules:\n  - rid: 10\n  references:\n    RefIds:\n"
            "    - rid: 10\n      type: {class: LastStandRule, ns: '', asm: Assembly-CSharp}\n"
            "      data:\n        ruleId: extreme:last-stand\n")
        self.assertEqual({"@nested:identityRules[ruleId=extreme%3Alast-stand].ruleId": "extreme:last-stand"}, actual)

    def test_combat_ability_id_is_captured_for_trait_and_species(self):
        body = (
            "  combatAbilities:\n"
            "    abilities:\n"
            "    - rid: 10\n"
            "  references:\n"
            "    RefIds:\n"
            "    - rid: 10\n"
            "      type: {class: CharacterCombatAbilityDefinition, ns: '', asm: DungeonStory.Combat}\n"
            "      data: {id: ability:alpha}\n"
        )
        for type_name in ("CharacterTraitSO", "CharacterSpeciesSO"):
            with self.subTest(type_name=type_name):
                self.assertEqual(
                    {COMBAT_ABILITY_ALPHA_PATH: "ability:alpha"},
                    self.extract(type_name, body),
                )

    def test_item_provider_ids_are_captured_only_for_matching_managed_types(self):
        body = (
            "  features:\n"
            "  - rid: 10\n"
            "  - rid: 20\n"
            "  - rid: 30\n"
            "  references:\n"
            "    RefIds:\n"
            "    - rid: 10\n"
            "      type: {class: AmmunitionItemFeature, ns: '', asm: DungeonStory.Economy}\n"
            "      data: {ammunitionKindId: ammo:arrow}\n"
            "    - rid: 20\n"
            "      type: {class: SubstanceItemFeature, ns: '', asm: DungeonStory.Economy}\n"
            "      data: {substanceId: substance:tonic}\n"
            "    - rid: 30\n"
            "      type: {class: ProductionItemFeature, ns: '', asm: DungeonStory.Economy}\n"
            "      data: {ammunitionKindId: ignored:ammo, substanceId: ignored:substance}\n"
        )
        expected = {
            AMMUNITION_ARROW_PATH: "ammo:arrow",
            SUBSTANCE_TONIC_PATH: "substance:tonic",
        }
        for type_name in ("ResourceItemDefinitionSO", "GenericItemDefinitionSO"):
            with self.subTest(type_name=type_name):
                self.assertEqual(expected, self.extract(type_name, body))
                for path in expected:
                    verifier._parse_nested_identity_path(
                        type_name,
                        path,
                        "baseline",
                    )

    def test_campaign_region_id_and_faction_provider_are_captured(self):
        actual = self.extract(
            "OffenseCampaignCatalogSO",
            "  targets: []\n"
            "  regions:\n"
            "  - id: region:north\n"
            "    factionId: faction:surface:north\n",
        )
        self.assertEqual(
            {
                "@nested:regions[id=region%3Anorth].id": "region:north",
                "@nested:regions[id=region%3Anorth].factionId":
                    "faction:surface:north",
            },
            actual,
        )

    def test_new_provider_duplicate_and_blank_ids_are_rejected(self):
        cases = (
            (
                "CharacterSpeciesSO",
                "  combatAbilities:\n"
                "    abilities:\n"
                "    - rid: 10\n"
                "    - rid: 20\n"
                "  references:\n"
                "    RefIds:\n"
                "    - rid: 10\n"
                "      type: {class: CharacterCombatAbilityDefinition, ns: '', asm: DungeonStory.Combat}\n"
                "      data: {id: ability:shared}\n"
                "    - rid: 20\n"
                "      type: {class: CharacterCombatAbilityDefinition, ns: '', asm: DungeonStory.Combat}\n"
                "      data: {id: ability:shared}\n",
                "duplicate nested identity selector",
            ),
            (
                "GenericItemDefinitionSO",
                "  features:\n"
                "  - rid: 10\n"
                "  references:\n"
                "    RefIds:\n"
                "    - rid: 10\n"
                "      type: {class: AmmunitionItemFeature, ns: '', asm: DungeonStory.Economy}\n"
                "      data: {ammunitionKindId: ' '}\n",
                "blank or not trimmed",
            ),
            (
                "OffenseCampaignCatalogSO",
                "  targets: []\n"
                "  regions:\n"
                "  - id: region:north\n"
                "    factionId: ' '\n",
                "blank or not trimmed",
            ),
        )
        for type_name, body, message in cases:
            with self.subTest(type_name=type_name), self.assertRaisesRegex(
                verifier.VerificationError,
                message,
            ):
                self.extract(type_name, body)

    def test_item_provider_baseline_path_requires_matching_managed_type(self):
        for malformed in (
            "@nested:features[ammunitionKindId=ammo%3Aarrow].ammunitionKindId",
            "@nested:features[ammunitionKindId=ammo%3Aarrow;"
            "type=ProductionItemFeature].ammunitionKindId",
        ):
            with self.subTest(path=malformed), self.assertRaises(
                verifier.VerificationError
            ):
                verifier._parse_nested_identity_path(
                    "GenericItemDefinitionSO",
                    malformed,
                    "baseline",
                )

    def test_nested_provider_rename_delete_and_append_contract(self):
        base_asset = verifier.PublishedAsset(
            ASSET_GUID,
            "CharacterSpeciesSO",
            SCRIPT_GUID,
            (verifier.IdentityField(COMBAT_ABILITY_ALPHA_PATH, "ability:alpha"),),
        )
        base = verifier.IdentityBaseline((base_asset,))
        renamed = verifier.IdentityBaseline((verifier.PublishedAsset(
            ASSET_GUID,
            "CharacterSpeciesSO",
            SCRIPT_GUID,
            (verifier.IdentityField(COMBAT_ABILITY_BETA_PATH, "ability:beta"),),
        ),))
        deleted = verifier.IdentityBaseline((verifier.PublishedAsset(
            ASSET_GUID,
            "CharacterSpeciesSO",
            SCRIPT_GUID,
            (),
        ),))
        for candidate in (renamed, deleted):
            with self.assertRaises(verifier.VerificationError):
                verifier.compare_baselines(base, candidate)

        appended = verifier.IdentityBaseline((verifier.PublishedAsset(
            ASSET_GUID,
            "CharacterSpeciesSO",
            SCRIPT_GUID,
            (
                verifier.IdentityField(COMBAT_ABILITY_ALPHA_PATH, "ability:alpha"),
                verifier.IdentityField(COMBAT_ABILITY_BETA_PATH, "ability:beta"),
            ),
        ),))
        verifier.compare_baselines(base, appended)
        self.assertEqual(
            "ability:alpha",
            appended.assets[0].fields_by_path()[COMBAT_ABILITY_ALPHA_PATH].value,
        )

    def test_campaign_region_faction_leaf_change_is_rejected(self):
        region_id_path = "@nested:regions[id=region%3Anorth].id"
        faction_id_path = "@nested:regions[id=region%3Anorth].factionId"
        base = verifier.IdentityBaseline((verifier.PublishedAsset(
            ASSET_GUID,
            "OffenseCampaignCatalogSO",
            SCRIPT_GUID,
            (
                verifier.IdentityField(region_id_path, "region:north"),
                verifier.IdentityField(faction_id_path, "faction:surface:north"),
            ),
        ),))
        changed = verifier.IdentityBaseline((verifier.PublishedAsset(
            ASSET_GUID,
            "OffenseCampaignCatalogSO",
            SCRIPT_GUID,
            (
                verifier.IdentityField(region_id_path, "region:north"),
                verifier.IdentityField(faction_id_path, "faction:surface:south"),
            ),
        ),))

        with self.assertRaisesRegex(
            verifier.VerificationError,
            "published identity changed",
        ):
            verifier.compare_baselines(base, changed)

    def test_facility_capability_identity_is_captured(self):
        actual = self.extract("FacilityEvolutionRecipeSO",
            "  formulaCapabilities:\n  - capabilityId: facility:alpha\n    evolutionModuleId: facility:research\n")
        self.assertEqual({"@nested:formulaCapabilities[capabilityId=facility%3Aalpha].capabilityId": "facility:alpha"}, actual)

    def test_building_provider_arrays_scalars_and_domain_need_are_captured(self):
        actual = self.extract(
            "BuildingSO",
            "  abilityModules:\n"
            "    items:\n"
            "    - rid: 10\n"
            "    - rid: 20\n"
            "    - rid: 30\n"
            "    - rid: 40\n"
            "    - rid: 50\n"
            "    - rid: 60\n"
            "  references:\n"
            "    RefIds:\n"
            "    - rid: 10\n"
            "      type: {class: BuildingSemanticTagsAbility, ns: '', asm: Assembly-CSharp}\n"
            "      data: {tags: [room:quiet, room:warm]}\n"
            "    - rid: 20\n"
            "      type: {class: BuildingProductionSupportAbility, ns: '', asm: Assembly-CSharp}\n"
            "      data: {featureTags: [production:heat]}\n"
            "    - rid: 30\n"
            "      type: {class: BuildingServiceSupportAbility, ns: '', asm: Assembly-CSharp}\n"
            "      data: {featureTags: [service:privacy]}\n"
            "    - rid: 40\n"
            "      type: {class: BuildingEvolutionAbility, ns: '', asm: Assembly-CSharp}\n"
            "      data:\n"
            "        settings:\n"
            "          tags: [evolution:rest]\n"
            "          scores: [{key: evolution:comfort, value: 2}]\n"
            "          metrics: [{key: evolution:traffic, value: 1}]\n"
            "    - rid: 50\n"
            "      type: {class: BuildingProductionWorkstationAbility, ns: '', asm: Assembly-CSharp}\n"
            "      data: {workstationTag: workstation:forge}\n"
            "    - rid: 60\n"
            "      type: {class: BuildingServiceHubAbility, ns: '', asm: Assembly-CSharp}\n"
            "      data: {serviceHubTag: service:lodging}\n",
        )

        expected = {
            "@nested:abilityModules.items[tags=room%3Aquiet;"
            "type=BuildingSemanticTagsAbility].tags": "room:quiet",
            "@nested:abilityModules.items[tags=room%3Awarm;"
            "type=BuildingSemanticTagsAbility].tags": "room:warm",
            "@nested:abilityModules.items[featureTags=production%3Aheat;"
            "type=BuildingProductionSupportAbility].featureTags": "production:heat",
            "@nested:abilityModules.items[featureTags=service%3Aprivacy;"
            "type=BuildingServiceSupportAbility].featureTags": "service:privacy",
            "@nested:abilityModules.items[settings.tags=evolution%3Arest;"
            "type=BuildingEvolutionAbility].settings.tags": "evolution:rest",
            "@nested:abilityModules.items[settings.scores.key=evolution%3Acomfort;"
            "type=BuildingEvolutionAbility].settings.scores.key": "evolution:comfort",
            "@nested:abilityModules.items[settings.metrics.key=evolution%3Atraffic;"
            "type=BuildingEvolutionAbility].settings.metrics.key": "evolution:traffic",
            "@nested:abilityModules.items[workstationTag=workstation%3Aforge;"
            "type=BuildingProductionWorkstationAbility].workstationTag":
                "workstation:forge",
            "@nested:abilityModules.items[serviceHubTag=service%3Alodging;"
            "type=BuildingServiceHubAbility].serviceHubTag": "service:lodging",
        }
        self.assertEqual(expected, actual)

        domain = self.extract(
            "GameDomainContentCatalogSO",
            "  metaUpgrades: []\n"
            "  runVariables: []\n"
            "  ownerDoctrines: []\n"
            "  invasionPatterns: []\n"
            "  characterNeeds:\n"
            "  - id: need:sleep\n",
        )
        self.assertEqual(
            {"@nested:characterNeeds[id=need%3Asleep].id": "need:sleep"},
            domain,
        )

    def test_building_provider_same_key_is_allowed_across_managed_types(self):
        actual = self.extract(
            "BuildingSO",
            "  abilityModules:\n"
            "    items:\n"
            "    - rid: 10\n"
            "    - rid: 20\n"
            "  references:\n"
            "    RefIds:\n"
            "    - rid: 10\n"
            "      type: {class: BuildingProductionSupportAbility, ns: '', asm: Assembly-CSharp}\n"
            "      data: {featureTags: [feature:shared]}\n"
            "    - rid: 20\n"
            "      type: {class: BuildingServiceSupportAbility, ns: '', asm: Assembly-CSharp}\n"
            "      data: {featureTags: [feature:shared]}\n",
        )

        self.assertEqual(2, len(actual))
        self.assertEqual({"feature:shared"}, set(actual.values()))
        self.assertTrue(any("type=BuildingProductionSupportAbility" in key for key in actual))
        self.assertTrue(any("type=BuildingServiceSupportAbility" in key for key in actual))

    def test_duplicate_building_provider_key_in_same_type_is_rejected(self):
        body = (
            "  abilityModules:\n"
            "    items:\n"
            "    - rid: 10\n"
            "  references:\n"
            "    RefIds:\n"
            "    - rid: 10\n"
            "      type: {class: BuildingProductionSupportAbility, ns: '', asm: Assembly-CSharp}\n"
            "      data: {featureTags: [feature:shared, feature:shared]}\n"
        )

        with self.assertRaisesRegex(
            verifier.VerificationError, "duplicate nested identity selector"
        ):
            self.extract("BuildingSO", body)

    @staticmethod
    def record(type_name: str) -> verifier.PublishedAsset:
        return verifier.PublishedAsset(ASSET_GUID, type_name, SCRIPT_GUID, ())

    @staticmethod
    def document(body: str) -> str:
        return (
            "--- !u!114 &11400000\n"
            "MonoBehaviour:\n"
            "  m_Script: {fileID: 11500000, guid: " + SCRIPT_GUID + ", type: 3}\n"
            + body
        )

    def extract(self, type_name: str, body: str) -> dict[str, str]:
        return verifier._actual_nested_fields(
            self.document(body), self.record(type_name), "fixture.asset"
        )

    def test_old_schema_one_root_row_without_virtual_fields_still_loads(self):
        document = {
            "schemaVersion": 1,
            "assets": [
                {
                    "guid": ROOT_ASSET_GUID,
                    "typeName": "GameContentCatalogSO",
                    "scriptGuid": ROOT_SCRIPT_GUID,
                    "fields": [{"path": "catalogId", "value": "root"}],
                }
            ],
        }
        loaded = verifier.load_baseline(
            (json.dumps(document) + "\n").encode("utf-8"), "old-schema-one"
        )
        self.assertEqual(1, len(loaded.assets))

    def test_managed_reference_routes_resolve_rids_and_preserve_parent_scope(self):
        body = (
            "  modules:\n"
            "  - rid: 20\n"
            "  - rid: 10\n"
            "  drawbackCapabilities: []\n"
            "  references:\n"
            "    version: 2\n"
            "    RefIds:\n"
            "    - rid: 10\n"
            "      type: {class: SkillRule, ns: '', asm: Assembly-CSharp}\n"
            "      data:\n"
            "        id: attack\n"
            "        capabilityId: attack-capability\n"
            "        variants:\n"
            "        - id: small\n"
            "    - rid: 20\n"
            "      type: {class: SkillRule, ns: '', asm: Assembly-CSharp}\n"
            "      data:\n"
            "        id: defense\n"
            "        capabilityId: defense-capability\n"
            "        variants: []\n"
        )

        actual = self.extract("CharacterSkillSystemSettingsSO", body)

        self.assertEqual("attack", actual["@nested:modules[id=attack].id"])
        self.assertEqual(
            "attack-capability",
            actual["@nested:modules[id=attack].capabilityId"],
        )
        self.assertEqual(
            "small",
            actual["@nested:modules[id=attack].variants[id=small].id"],
        )
        self.assertEqual("defense", actual["@nested:modules[id=defense].id"])

    def test_duplicate_blank_and_missing_nested_selectors_are_rejected(self):
        duplicate = "  nodes:\n  - nodeId: arm\n  - nodeId: arm\n"
        blank = "  nodes:\n  - nodeId: ' '\n"
        missing = "  nodes:\n  - displayName: arm\n"
        cases = (
            (duplicate, "duplicate nested identity selector"),
            (blank, "blank or not trimmed"),
            (missing, "nested Unity field is missing"),
        )
        for body, message in cases:
            with self.subTest(message=message), self.assertRaisesRegex(
                verifier.VerificationError, message
            ):
                self.extract("AnatomyProfileSO", body)

    def test_ambiguous_managed_rid_is_rejected(self):
        body = (
            "  modules:\n"
            "  - rid: 10\n"
            "  drawbackCapabilities: []\n"
            "  references:\n"
            "    RefIds:\n"
            "    - rid: 10\n"
            "      type: {class: SkillRule, ns: '', asm: Assembly-CSharp}\n"
            "      data: {id: attack, capabilityId: attack, variants: []}\n"
            "    - rid: 10\n"
            "      type: {class: SkillRule, ns: '', asm: Assembly-CSharp}\n"
            "      data: {id: attack, capabilityId: attack, variants: []}\n"
        )
        with self.assertRaisesRegex(verifier.VerificationError, "rid is ambiguous"):
            self.extract("CharacterSkillSystemSettingsSO", body)

    def test_managed_type_filter_is_part_of_building_identity(self):
        body = (
            "  abilityModules:\n"
            "    items:\n"
            "    - rid: 10\n"
            "    - rid: 20\n"
            "  references:\n"
            "    RefIds:\n"
            "    - rid: 10\n"
            "      type: {class: BuildingProductionAbility, ns: '', asm: Assembly-CSharp}\n"
            "      data: {abilityId: BuildingProductionAbility}\n"
            "    - rid: 20\n"
            "      type: {class: BuildingCookingAbility, ns: '', asm: Assembly-CSharp}\n"
            "      data: {abilityId: BuildingCookingAbility}\n"
        )
        actual = self.extract("BuildingSO", body)
        self.assertEqual(
            {
                "@nested:abilityModules.items[abilityId=BuildingProductionAbility;"
                "type=BuildingProductionAbility].abilityId": "BuildingProductionAbility"
            },
            actual,
        )

    def test_child_move_changes_the_scoped_identity_path(self):
        before = self.extract(
            "CharacterAcquiredTraitSettingsSO",
            "  drawbackCapabilities:\n"
            "  - drawbackId: first\n"
            "    capabilityId: first-capability\n"
            "    effects:\n"
            "    - bindingId: moved\n"
            "  - drawbackId: second\n"
            "    capabilityId: second-capability\n"
            "    effects: []\n",
        )
        after = self.extract(
            "CharacterAcquiredTraitSettingsSO",
            "  drawbackCapabilities:\n"
            "  - drawbackId: first\n"
            "    capabilityId: first-capability\n"
            "    effects: []\n"
            "  - drawbackId: second\n"
            "    capabilityId: second-capability\n"
            "    effects:\n"
            "    - bindingId: moved\n",
        )
        self.assertIn(
            "@nested:drawbackCapabilities[drawbackId=first].effects[bindingId=moved].bindingId",
            before,
        )
        self.assertNotEqual(before, after)

    def test_nested_selector_escape_must_be_canonical(self):
        with self.assertRaisesRegex(verifier.VerificationError, "canonically escaped"):
            verifier._parse_nested_identity_path(
                "AnatomyProfileSO",
                "@nested:nodes[nodeId=left%2farm].nodeId",
                "baseline",
            )
        verifier._parse_nested_identity_path(
            "AnatomyProfileSO",
            "@nested:nodes[nodeId=left%2Farm].nodeId",
            "baseline",
        )

    def test_yaml_duplicate_keys_aliases_and_duplicate_legacy_names_are_rejected(self):
        duplicate_key = "  nodes:\n  - nodeId: arm\n    nodeId: leg\n"
        alias = "  nodes: &nodes\n  - nodeId: arm\n  copied: *nodes\n"
        for body, message in (
            (duplicate_key, "duplicate Unity YAML mapping key"),
            (alias, "aliases and anchors are unsupported"),
        ):
            with self.subTest(message=message), self.assertRaisesRegex(
                verifier.VerificationError, message
            ):
                self.extract("AnatomyProfileSO", body)

        record = self.record("CharacterTraitSO")
        document = self.document("  legacyPolicyNames:\n  - Old Name\n  - Old Name\n")
        with self.assertRaisesRegex(verifier.VerificationError, "duplicate legacy trait name"):
            verifier._actual_legacy_trait_fields(document, record, "fixture.asset")


class GameAssetEligibilityTests(unittest.TestCase):
    def test_path_rules_match_editor_case_semantics(self):
        self.assertTrue(
            verifier._is_eligible_game_asset_path(
                "Assets/Data/Game.asset", "Assets/Scripts/Models/GameSO.cs"
            )
        )
        self.assertFalse(
            verifier._is_eligible_game_asset_path(
                "assets/Data/Game.asset", "Assets/Scripts/Models/GameSO.cs"
            )
        )
        self.assertFalse(
            verifier._is_eligible_game_asset_path(
                "Assets/Data/Game.asset", "Assets/scripts/Models/GameSO.cs"
            )
        )
        for excluded in ("Editor", "vEnDoR", "THIRDPARTY", "pLuGiNs"):
            with self.subTest(excluded=excluded):
                self.assertFalse(
                    verifier._is_eligible_game_asset_path(
                        "Assets/Data/Game.asset",
                        f"Assets/Scripts/{excluded}/GameSO.cs",
                    )
                )


class ScalarDecodingTests(unittest.TestCase):
    def test_work_type_protocol_membership_and_uniqueness(self):
        source = b'''public static readonly WorkTypeId Craft = new WorkTypeId("work:craft");
public static readonly WorkTypeId Haul = new WorkTypeId("work:haul");
public static readonly IReadOnlyList<WorkTypeId> All = new[] { Craft, Haul };'''
        self.assertEqual(
            {"Craft": "work:craft", "Haul": "work:haul"},
            verifier.work_type_protocol_ids(source),
        )
        for broken in (source.replace(b"Craft, Haul", b"Craft"),
                       source.replace(b"work:haul", b"work:craft"),
                       source.replace(b"new[]", b"CreateUnknown()")):
            with self.assertRaises(verifier.VerificationError):
                verifier.work_type_protocol_ids(broken)

    def test_work_type_protocol_rename_or_source_removal_is_rejected(self):
        raw = b'''public static readonly WorkTypeId Craft = new WorkTypeId("work:craft");
public static readonly IReadOnlyList<WorkTypeId> All = new[] { Craft };'''
        class Snapshot:
            def __init__(self, content): self.content = content
            def read_blob(self, path):
                self.path = path
                return self.content
        before = Snapshot(raw)
        verifier.verify_work_type_protocols(before, Snapshot(raw))
        for after in (raw.replace(b"work:craft", b"work:craft-new"), None):
            with self.assertRaises(verifier.VerificationError):
                verifier.verify_work_type_protocols(before, Snapshot(after))
        added = raw.replace(b"public static readonly IReadOnlyList", b'public static readonly WorkTypeId Haul = new WorkTypeId("work:haul");\npublic static readonly IReadOnlyList')
        added = added.replace(b"{ Craft }", b"{ Craft, Haul }")
        verifier.verify_work_type_protocols(before, Snapshot(added))

    def test_work_type_protocol_symbol_value_swap_is_rejected(self):
        raw = b'''public static readonly WorkTypeId Craft = new WorkTypeId("work:craft");
public static readonly WorkTypeId Cook = new WorkTypeId("work:cook");
public static readonly IReadOnlyList<WorkTypeId> All = new[] { Craft, Cook };'''
        swapped = raw.replace(b'"work:craft"', b'"work:temporary"')
        swapped = swapped.replace(b'"work:cook"', b'"work:craft"')
        swapped = swapped.replace(b'"work:temporary"', b'"work:cook"')

        class Snapshot:
            def __init__(self, content): self.content = content
            def read_blob(self, path): return self.content

        with self.assertRaisesRegex(
            verifier.VerificationError, "WorkType protocol binding changed"
        ):
            verifier.verify_work_type_protocols(Snapshot(raw), Snapshot(swapped))

    def test_work_type_symbol_rename_requires_compatible_alias(self):
        raw = b'''public static readonly WorkTypeId Craft = new WorkTypeId("work:craft");
public static readonly IReadOnlyList<WorkTypeId> All = new[] { Craft };'''
        renamed = b'''public static readonly WorkTypeId Fabricate = new WorkTypeId("work:craft");
public static readonly IReadOnlyList<WorkTypeId> All = new[] { Fabricate };'''
        aliased = b'''public static readonly WorkTypeId Fabricate = new WorkTypeId("work:craft");
public static readonly WorkTypeId Craft = Fabricate;
public static readonly IReadOnlyList<WorkTypeId> All = new[] { Fabricate };'''

        class Snapshot:
            def __init__(self, content): self.content = content
            def read_blob(self, path): return self.content

        with self.assertRaisesRegex(
            verifier.VerificationError, "WorkType protocol symbol was removed"
        ):
            verifier.verify_work_type_protocols(Snapshot(raw), Snapshot(renamed))
        verifier.verify_work_type_protocols(Snapshot(raw), Snapshot(aliased))

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


class PersistentBuildingIdentityTests(GitIdentityFixture):
    contracts = (
        "public interface IBuildingPersistentAbilityDefinition {}\n"
        "public abstract class BuildingAbility {}\n"
        "public abstract class SavedBase : BuildingAbility, IBuildingPersistentAbilityDefinition {}\n"
        "public sealed class NewSavedAbility : SavedBase {}\n"
        "public sealed class UnsavedAbility : BuildingAbility {}\n"
        "// public class CommentAbility : SavedBase {}\n"
        'public class TextHolder { string text = "class TextAbility : SavedBase {}"; }\n'
    )

    def publish_building(self):
        (self.root / "Assets/Scripts/TestSO.cs").write_text("public class BuildingSO {}\n")
        (self.root / "Assets/Scripts/Abilities.cs").write_text(self.contracts)
        self.write_building("NewSavedAbility", "saved:alpha")
        self.write_baseline([self.asset_row(type_name="BuildingSO", fields=(
            (self.identity_path("NewSavedAbility", "saved:alpha"), "saved:alpha"),))])
        return self.commit("publish declared persistent ability")

    @staticmethod
    def identity_path(kind, identity):
        return ("@nested:abilityModules.items[abilityId="
                + verifier.quote(identity, safe="-._~") + f";type={kind}].abilityId")

    def write_building(self, kind, identity):
        self.write_asset(fields=("abilityModules:", "  items:", "  - rid: 10", "  - rid: 20",
            "references:", "  RefIds:", "  - rid: 10",
            f"    type: {{class: {kind}, ns: '', asm: Assembly-CSharp}}",
            f"    data: {{abilityId: {identity}}}", "  - rid: 20",
            "    type: {class: UnsavedAbility, ns: '', asm: Assembly-CSharp}",
            "    data: {abilityId: unsaved:ignored}"))

    def test_inherited_new_type_uses_committed_declaration_without_gate_registration(self):
        base = self.publish_building()
        extra = self.root / "Assets/Scripts/AdditionalAbility.cs"
        extra.write_text("public sealed class AnotherSavedAbility : SavedBase {}\n")
        head = self.commit("add independently declared persistent ability")
        snapshot = verifier.GitSnapshot(self.root, head)
        routes = verifier.nested_routes_for_snapshot(snapshot)
        self.assertEqual(("AnotherSavedAbility", "NewSavedAbility"), routes["BuildingSO"][0].managed_types)
        # An omitted published key is rejected as soon as the new type is used in an asset.
        self.write_building("AnotherSavedAbility", "saved:beta")
        changed = self.commit("use new persistent ability without publication")
        with self.assertRaisesRegex(verifier.VerificationError, "nested identities do not match"):
            verifier.verify_revisions(base, changed, self.root)
        self.write_building("NewSavedAbility", "saved:alpha")
        restored = self.commit("restore published asset")
        extra.write_text("public class WorkingTreeOnlyAbility : SavedBase {}\n")
        self.assertEqual(1, verifier.verify_revisions(base, restored, self.root)["fieldsChecked"])

    def test_published_persistent_id_mutation_is_rejected_with_or_without_republication(self):
        base = self.publish_building()
        self.write_building("NewSavedAbility", "saved:renamed")
        head = self.commit("mutate saved address")
        with self.assertRaises(verifier.VerificationError):
            verifier.verify_revisions(base, head, self.root)
        self.write_baseline([self.asset_row(type_name="BuildingSO", fields=(
            (self.identity_path("NewSavedAbility", "saved:renamed"), "saved:renamed"),))])
        head = self.commit("try to republish renamed address")
        with self.assertRaises(verifier.VerificationError):
            verifier.verify_revisions(base, head, self.root)

    def test_removing_marker_cannot_silently_remove_identity_protection(self):
        base = self.publish_building()
        source = self.root / "Assets/Scripts/Abilities.cs"
        source.write_text(self.contracts.replace(", IBuildingPersistentAbilityDefinition", ""))
        head = self.commit("remove persistent membership")
        with self.assertRaisesRegex(verifier.VerificationError, "no concrete definitions"):
            verifier.verify_revisions(base, head, self.root)
        source.write_text(self.contracts.replace(", IBuildingPersistentAbilityDefinition", "")
                          .replace("public interface IBuildingPersistentAbilityDefinition {}\n", ""))
        head = self.commit("remove marker authority")
        with self.assertRaisesRegex(verifier.VerificationError, "authority was removed"):
            verifier.verify_revisions(base, head, self.root)

    def test_ambiguous_persistent_type_names_fail_closed(self):
        base = self.publish_building()
        (self.root / "Assets/Scripts/Duplicate.cs").write_text("public class NewSavedAbility : SavedBase {}\n")
        head = self.commit("introduce ambiguous managed class name")
        with self.assertRaisesRegex(verifier.VerificationError, "declaration is ambiguous"):
            verifier.verify_revisions(base, head, self.root)

    def test_formula_hash_needs_current_native_execution_and_cannot_be_omitted(self):
        (self.root / "Assets/Scripts/TestSO.cs").write_text("public class FacilityEvolutionRecipeSO {}\n")
        self.write_asset(fields=("formulaCapabilities: []",))
        self.write_baseline([self.asset_row(type_name="FacilityEvolutionRecipeSO",
            fields=((verifier.FORMULA_CONTENT_FIELD, "a" * 64),))])
        head = self.commit("publish formula without native validation")
        with self.assertRaisesRegex(verifier.VerificationError, "native evidence"):
            verifier.verify_revisions(self.initial, head, self.root)
        self.write_baseline([self.asset_row(type_name="FacilityEvolutionRecipeSO", fields=())])
        head = self.commit("omit formula protection")
        with self.assertRaisesRegex(verifier.VerificationError, "formula contract is missing"):
            verifier.verify_revisions(self.initial, head, self.root)

    def test_compound_fingerprint_cannot_pass_by_matching_hash_shape_alone(self):
        self.write_asset(fields=("policy:", "  amount: 7"))
        self.write_baseline([self.asset_row(fields=(("policy", "a" * 64),))])
        head = self.commit("publish structured identity without native proof")
        with self.assertRaisesRegex(verifier.VerificationError, "native evidence"):
            verifier.verify_revisions(self.initial, head, self.root)


class R26IdentityEncodingTests(unittest.TestCase):
    def test_pair_groups_preserve_empty_and_utf16_encoded_identity(self):
        probe = NestedIdentityExtractionTests()
        actual = probe.extract("AnatomyProfileSO", "  nodes:\n  - nodeId: torso\n"
                               "  - nodeId: arm\n    pairedGroupId: 'pair:🦾'\n")
        self.assertEqual("0:", actual["@nested:nodes[nodeId=torso].pairedGroupId"])
        self.assertEqual("7:pair:🦾", actual["@nested:nodes[nodeId=arm].pairedGroupId"])
        for malformed in ("[]", "' noncanonical '"):
            with self.assertRaises(verifier.VerificationError):
                probe.extract("AnatomyProfileSO", "  nodes:\n  - nodeId: arm\n    pairedGroupId: " + malformed + "\n")

    def test_equipment_provider_identity_is_checked_against_managed_asset(self):
        probe = NestedIdentityExtractionTests()
        body = ("  features:\n  - rid: 10\n  references:\n    RefIds:\n    - rid: 10\n"
                "      type: {class: EquipmentItemFeature, ns: '', asm: Assembly-CSharp}\n"
                "      data: {equipmentDefinitionId: weapon:test}\n")
        path = "@nested:features[equipmentDefinitionId=weapon%3Atest;type=EquipmentItemFeature].equipmentDefinitionId"
        self.assertEqual({path: "weapon:test"}, probe.extract("GenericItemDefinitionSO", body))
        with self.assertRaises(verifier.VerificationError):
            verifier._parse_nested_identity_path("GenericItemDefinitionSO", path.replace("EquipmentItemFeature", "AmmunitionItemFeature"), "test")

    def test_formula_identity_rejects_wrong_type_hash_and_republication(self):
        def load(kind, digest):
            row = GitIdentityFixture.asset_row(type_name=kind, fields=((verifier.FORMULA_CONTENT_FIELD, digest),))
            return verifier.load_baseline(json.dumps({"schemaVersion": 1, "assets": [row]}).encode(), "test")
        for kind, digest in (("BuildingSO", "a" * 64), ("FacilityEvolutionRecipeSO", "bad")):
            with self.assertRaises(verifier.VerificationError):
                load(kind, digest)
        with self.assertRaisesRegex(verifier.VerificationError, "published identity changed"):
            verifier.compare_baselines(load("FacilityEvolutionRecipeSO", "a" * 64),
                                       load("FacilityEvolutionRecipeSO", "b" * 64))


class FormulaExecutionEvidenceTests(unittest.TestCase):
    published_count = 7

    def marker(self, count: int | None = None) -> str:
        return (
            "CONTENT_REFERENCE_INTEGRITY=PASS; runtimeSoTypes=12; "
            f"publishedAssets={self.published_count if count is None else count}; complete\n"
        )

    def run_formula_evidence(
        self,
        effects: dict[str, object],
        logs: dict[str, str],
        *,
        missing_committed_path: str | None = None,
    ) -> list[str]:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            for relative, text in logs.items():
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(text, encoding="utf-8")

            gate = types.ModuleType("verify_change_scope")
            gate.ROOT = root
            gate.EVIDENCE = "Artifacts/QA/ImpactCI/verification.json"
            gate.verify_receipt = mock.Mock()
            gate.ledger = types.SimpleNamespace(
                load_json_strict=mock.Mock(return_value={"checks": [effects]})
            )
            cat_file_paths: list[str] = []

            def run_git(_: Path, command: list[str], input_bytes=None) -> bytes:
                if command[:3] == ["git", "cat-file", "-e"]:
                    path = command[3].split(":", 1)[1]
                    cat_file_paths.append(path)
                    if path == missing_committed_path:
                        raise verifier.VerificationError("committed nested evidence is missing")
                return b""

            self.gate = gate
            snapshot = types.SimpleNamespace(root=root, commit="head")
            with mock.patch.dict(sys.modules, {"verify_change_scope": gate}):
                with mock.patch.object(verifier, "_resolve_commit", return_value="head"):
                    with mock.patch.object(verifier, "_run_git", side_effect=run_git):
                        verifier.verify_formula_execution(snapshot, self.published_count)
            return cat_file_paths

    def assert_effect_gate_called(self) -> None:
        self.gate.verify_receipt.assert_called_once_with({"effects"})

    def test_formula_execution_accepts_the_legacy_single_effect_log(self):
        log = "Artifacts/QA/ImpactCI/r30-effects.log"
        summary = "Artifacts/QA/ImpactCI/r30-effects-summary.json"
        cat_paths = self.run_formula_evidence(
            {"group": "effects", "log": log, "summary": summary},
            {log: self.marker()},
        )
        self.assert_effect_gate_called()
        self.assertEqual(
            ["Artifacts/QA/ImpactCI/verification.json", log, summary],
            cat_paths,
        )

    def test_formula_execution_accepts_three_effect_parts_with_one_marker_total(self):
        parts = []
        logs = {}
        expected = ["Artifacts/QA/ImpactCI/verification.json"]
        for name, log_text in (
            ("effects-definitions", self.marker()),
            ("effects-recovery", "recovery completed\n"),
            ("effects-traits", "traits completed\n"),
        ):
            log = f"Artifacts/QA/ImpactCI/r30-{name}.log"
            summary = f"Artifacts/QA/ImpactCI/r30-{name}-summary.json"
            parts.append({"group": name, "log": log, "summary": summary})
            logs[log] = log_text
            expected.extend((log, summary))
        cat_paths = self.run_formula_evidence(
            {"group": "effects", "parts": parts}, logs)
        self.assert_effect_gate_called()
        self.assertEqual(expected, cat_paths)

    def test_formula_execution_rejects_missing_committed_effect_part_evidence(self):
        parts = []
        logs = {}
        for name in ("effects-definitions", "effects-recovery", "effects-traits"):
            log = f"Artifacts/QA/ImpactCI/r30-{name}.log"
            summary = f"Artifacts/QA/ImpactCI/r30-{name}-summary.json"
            parts.append({"group": name, "log": log, "summary": summary})
            logs[log] = self.marker() if name == "effects-definitions" else "part completed\n"
        with self.assertRaisesRegex(verifier.VerificationError, "committed nested evidence is missing"):
            self.run_formula_evidence(
                {"group": "effects", "parts": parts},
                logs,
                missing_committed_path=parts[1]["summary"],
            )
        self.assert_effect_gate_called()

    def test_formula_execution_rejects_missing_duplicate_or_wrong_count_marker(self):
        cases = ("missing", "duplicate", "wrong-count")
        for fixture in ("single", "parts"):
            for name in cases:
                with self.subTest(fixture=fixture, name=name):
                    if fixture == "single":
                        log = "Artifacts/QA/ImpactCI/r30-effects.log"
                        summary = "Artifacts/QA/ImpactCI/r30-effects-summary.json"
                        effects = {"group": "effects", "log": log, "summary": summary}
                        single_logs = {
                            "missing": "no content marker\n",
                            "duplicate": self.marker() + self.marker(),
                            "wrong-count": self.marker(self.published_count + 1),
                        }
                        logs = {log: single_logs[name]}
                    else:
                        parts = []
                        logs = {}
                        for part_name in ("effects-definitions", "effects-recovery", "effects-traits"):
                            log = f"Artifacts/QA/ImpactCI/r30-{part_name}.log"
                            summary = f"Artifacts/QA/ImpactCI/r30-{part_name}-summary.json"
                            parts.append({"group": part_name, "log": log, "summary": summary})
                            logs[log] = "part completed\n"
                        if name == "duplicate":
                            logs[parts[0]["log"]] = self.marker()
                            logs[parts[1]["log"]] = self.marker()
                        elif name == "wrong-count":
                            logs[parts[0]["log"]] = self.marker(self.published_count + 1)
                        effects = {"group": "effects", "parts": parts}
                    with self.assertRaisesRegex(
                        verifier.VerificationError,
                        "lacks the complete published identity validation",
                    ):
                        self.run_formula_evidence(effects, logs)
                    self.assert_effect_gate_called()


if __name__ == "__main__":
    unittest.main()
