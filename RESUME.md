# Resume this Unity snapshot

This branch preserves the current local work without updating main or the active local branch.

Clone the private companion repository next to this checkout:

```powershell
gh repo clone Creeper0809/DungeonStoryNarrativeAI
cd DungeonStoryNarrativeAI
python tools/resume/restore_workspace.py --environment
```

Read the companion RESUME.md for the exact R80 default, R85 candidate, source pins and next evaluation steps.
Unity version: 6000.3.8f1. Models and native runtime are restored from the private release.
The R60 compile result is preserved as historical evidence, not a new acceptance of every later source change.
The branch was captured from the local checkout. It is separate from newer main commits and is not a merge.
