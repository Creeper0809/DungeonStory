# DungeonStory V25 narrative model pipeline

This directory treats game rules and prose as separate authorities. Training
examples contain only rule-produced facts and legal choices. The model learns
how to name and narrate them; it never learns to invent gameplay effects.

## Mount the current untrained base model

Training is not required to exercise the offline runtime integration. On
Windows, the following command downloads the official CPU-only llama.cpp build
and the official Qwen3-1.7B Q4_K_M GGUF, verifies the GGUF header and size, and
mounts both under `Assets/StreamingAssets/DungeonStoryLlm`:

```powershell
tools\v25_narrative_training\mount_base_model.cmd
```

The generated manifest explicitly records `trainingState: base-untrained` and
`releaseCertified: false`. This path never reads `checkpoint-*` or an adapter,
does not start SFT, and does not bypass `package_release.py` for an actual
release. The Unity runtime starts the model with `--gpu-layers 0`, structured
JSON output, loopback authentication, and deterministic rule fallback.

## Corpus build and review

```powershell
python tools/v25_narrative_training/build_dataset.py
python tools/v25_narrative_training/verify_dataset.py
```

The builder reads the authoritative Unity YAML assets, creates 50,000 paired
scenario-family records, selects a balanced 40,000-record pool, reserves 2,000
family-isolated held-out records, and writes 8,000 blank human-review rows. It
also emits a TRL conversational prompt/completion projection. Internet sources
listed in `sources.json` define only taxonomy, tooling, and license boundaries;
no source sentence or modern-fiction passage is copied.

Review instructions are in `REVIEW_GUIDE.md`. Human approval is never inferred
or pre-filled; completed rows are merged only by `apply_human_review.py`.

The dependency-free local review workbench keeps the source CSV chunks
immutable and autosaves explicit reviewer actions separately:

```powershell
python tools/v25_narrative_training/reviewer/server.py --open
python tools/v25_narrative_training/apply_human_review.py --review-csv Artifacts/Review/V25/reviewer_export.csv
```

It binds to loopback only, requires a per-launch token for every API request,
loads no third-party resources, and supports keyboard review, deterministic
warning highlights, similarity/filter views, bounded confirmed bulk actions,
undo, resume, and merge-compatible export.

Candidate JSON is parsed into profile-aware cards (skill name/reason, equipment
history, facility identity, individual viewpoints, dialogue, and so on). Raw
JSON is available only under an advanced disclosure. Direct edits start from A
or B and expose prose fields while rule-owned values remain read-only and are
validated again by the server. On desktop, facts and both candidates scroll in
their own panes so the decision controls stay visible.

1. Export 50,000 deterministic rule scenarios as JSONL matching
   `dataset.schema.json`. Keep stable event/viewpoint splits together to prevent
   the same event leaking across train and held-out data.
2. Apply structural, visibility, fact-reference, duplicate, and profanity
   filters. Create the pinned Python 3.11 environment with
   `setup_sft_environment.cmd`, then run a two-step smoke test with
   `start_sft.cmd --sample-limit 64 --max-steps 2 --output Artifacts/Training/V25/models/sft-smoke`.
   Run the full grounded QLoRA SFT with `start_sft.cmd` only after the smoke
   evidence passes. The trainer consumes only `trl_sft_train_38000.jsonl.gz`;
   rejected candidates and synthetic preferences cannot enter this stage.
   Preserve every SFT
   checkpoint considered for release.
3. Human reviewers approve, rewrite, or drop the 8,000 paired samples after an
   SFT candidate exists. Of these, 6,000 can supply DPO preferences and 2,000
   remain permanently held out. Synthetic `systemPreferred` metadata is never
   accepted as a human label.
4. Train three optional one-epoch DPO candidates at beta 0.03, 0.05, and 0.10
   from the completed human review only.
   Never overwrite the SFT candidate.
5. Generate the same held-out contexts in non-thinking mode and run
   `evaluate_release.py`. DPO is rejected if grounding does not improve enough
   or vocabulary entropy, Distinct-2/3, name roots, Self-BLEU, cultural lexical
   spread, or near-duplicate rates cross their regression budgets.
6. If every DPO candidate fails, release the passing SFT model. Convert only the
   chosen checkpoint to GGUF Q4_K_M.
7. Build and certify both native hosts, then run `package_release.py`. It refuses
   to create a StreamingAssets manifest unless held-out evaluation passed, the
   model is at most 1.5 GB, and both host binaries exist. The emitted SHA-256 is
   checked again by the game before launch.

No trained weight, human approval, or evaluation result is fabricated by these
tools. Those become authoritative only after the corresponding work and gates
actually pass.
