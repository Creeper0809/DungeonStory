#!/usr/bin/env python3
"""Reproducible QLoRA SFT for the DungeonStory V25 narrative adapter.

This stage learns only from the grounded ``chosen`` projection. Human A/B
decisions are intentionally absent; they are consumed by the later DPO stage.
Use ``--max-steps 2 --sample-limit 64`` for a pipeline smoke test.
"""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import os
import random
from pathlib import Path


TOOL_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOL_ROOT.parents[1]
DEFAULT_DATASET = REPO_ROOT / "Artifacts/Training/V25/trl_sft_train_38000.jsonl.gz"
DEFAULT_OUTPUT = REPO_ROOT / "Artifacts/Training/V25/models/sft-qwen3-1.7b-v1"
FORBIDDEN_NEGATIVE = "전설의 운명이 깨어나 모든 것을 바꾸었다."


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_records(path: Path, limit: int | None):
    from datasets import Dataset

    rows = []
    with gzip.open(path, "rt", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            row = json.loads(line)
            completion = row.get("completion")
            if not isinstance(completion, list) or len(completion) != 1 or completion[0].get("role") != "assistant":
                raise ValueError(f"{path}:{line_number}: invalid conversational completion")
            text = completion[0].get("content", "")
            json.loads(text)
            if FORBIDDEN_NEGATIVE in text:
                raise ValueError(f"{path}:{line_number}: rejected fallback leaked into SFT")
            rows.append(row)
            if limit and len(rows) >= limit:
                break
    if not rows:
        raise ValueError("SFT dataset is empty")
    return Dataset.from_list(rows)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset", type=Path, default=DEFAULT_DATASET)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--model", default="Qwen/Qwen3-1.7B")
    parser.add_argument("--sample-limit", type=int)
    parser.add_argument("--max-steps", type=int, default=-1)
    parser.add_argument("--epochs", type=float, default=2.0)
    parser.add_argument("--resume", nargs="?", const=True, default=False)
    parser.add_argument("--seed", type=int, default=250825)
    args = parser.parse_args()

    os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")

    import torch
    from peft import LoraConfig
    from transformers import AutoModelForCausalLM, AutoTokenizer, BitsAndBytesConfig, set_seed
    from trl import SFTConfig, SFTTrainer

    if not torch.cuda.is_available():
        raise SystemExit("CUDA GPU is required for the V25 QLoRA SFT stage")
    major, _ = torch.cuda.get_device_capability(0)
    if major < 8:
        raise SystemExit("V25 BF16 training requires an Ampere-class or newer NVIDIA GPU")

    dataset_path = args.dataset.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    random.seed(args.seed)
    set_seed(args.seed)
    torch.backends.cuda.matmul.allow_tf32 = True
    torch.backends.cudnn.allow_tf32 = True
    torch.cuda.reset_peak_memory_stats(0)

    train_dataset = load_records(dataset_path, args.sample_limit)
    tokenizer = AutoTokenizer.from_pretrained(args.model, use_fast=True)
    if tokenizer.pad_token_id is None:
        tokenizer.pad_token = tokenizer.eos_token

    quantization = BitsAndBytesConfig(
        load_in_4bit=True,
        bnb_4bit_quant_type="nf4",
        bnb_4bit_compute_dtype=torch.bfloat16,
        bnb_4bit_use_double_quant=True,
    )
    model = AutoModelForCausalLM.from_pretrained(
        args.model,
        quantization_config=quantization,
        device_map={"": 0},
        torch_dtype=torch.bfloat16,
        trust_remote_code=False,
        use_cache=False,
    )
    model.config.use_cache = False

    peft = LoraConfig(
        r=32,
        lora_alpha=64,
        lora_dropout=0.05,
        target_modules="all-linear",
        bias="none",
        task_type="CAUSAL_LM",
    )
    training = SFTConfig(
        output_dir=str(output),
        max_length=2048,
        # Windows release tooling does not depend on FlashAttention2. TRL's
        # packed padding-free path is unsafe without it because examples may
        # attend across boundaries, so correctness wins over packing speed.
        packing=False,
        completion_only_loss=True,
        per_device_train_batch_size=2,
        gradient_accumulation_steps=64,
        learning_rate=1e-4,
        lr_scheduler_type="cosine",
        warmup_ratio=0.03,
        num_train_epochs=args.epochs,
        max_steps=args.max_steps,
        bf16=True,
        tf32=True,
        gradient_checkpointing=True,
        gradient_checkpointing_kwargs={"use_reentrant": False},
        optim="paged_adamw_8bit",
        logging_steps=5,
        save_strategy="steps",
        save_steps=20,
        save_total_limit=3,
        report_to="none",
        seed=args.seed,
        data_seed=args.seed,
        dataset_num_proc=1,
        eos_token="<|im_end|>",
        remove_unused_columns=True,
        label_names=["labels"],
    )
    trainer = SFTTrainer(
        model=model,
        args=training,
        train_dataset=train_dataset,
        processing_class=tokenizer,
        peft_config=peft,
    )
    result = trainer.train(resume_from_checkpoint=args.resume)
    trainer.save_model(str(output / "adapter"))
    tokenizer.save_pretrained(str(output / "adapter"))

    evidence = {
        "stage": "SFT",
        "baseModel": args.model,
        "dataset": str(dataset_path),
        "datasetSha256": sha256(dataset_path),
        "records": len(train_dataset),
        "seed": args.seed,
        "epochsRequested": args.epochs,
        "maxSteps": args.max_steps,
        "globalStep": trainer.state.global_step,
        "trainingLoss": result.training_loss,
        "peakGpuAllocatedBytes": torch.cuda.max_memory_allocated(0),
        "peakGpuReservedBytes": torch.cuda.max_memory_reserved(0),
        "gpu": torch.cuda.get_device_name(0),
        "torch": torch.__version__,
        "cuda": torch.version.cuda,
        "transformers": __import__("transformers").__version__,
        "trl": __import__("trl").__version__,
        "peft": __import__("peft").__version__,
    }
    (output / "training_evidence.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(evidence, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
