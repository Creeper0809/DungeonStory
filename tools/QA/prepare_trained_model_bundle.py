"""Create-only local V10 test bundle; never changes the shipped model or training artifacts."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import zipfile


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve(strict=True)
    qa = Path(__file__).resolve().parents[2] / 'Artifacts' / 'QA'
    require(root.is_relative_to(qa), 'Bundle must remain inside this repository QA root')
    sources = {
        'downloads/Qwen3-8B-Q4_K_M-r2.gguf': 'd98cdcbd03e17ce47681435b5150e34c1417f50b5c0019dd560e4882c5745785',
        'downloads/llama-b10331-bin-win-cuda-12.4-x64-r2.zip': '77eaf749ea4af9ff72ce989e56e9df9ca173f36189ea25d9633b33998f4f1e21',
        'downloads/cudart-llama-bin-win-cuda-12.4-x64.zip': '8c79a9b226de4b3cacfd1f83d24f962d0773be79f1e7b75c6af4ded7e32ae1d6',
        'adapter/adapter_model.safetensors': 'cd5e700fbcc1da79d5767258d409bf097e9766d5c0d00246e2fe30ae2f54956f',
        'adapter/adapter_config.json': 'cfd44605dce041632bd200aaa352052c6c2a63469d2221dd9c4be564487d0c31',
    }
    for name, expected in sources.items():
        require(sha(root / name) == expected, 'Source SHA-256 mismatch: ' + name)
    adapter = root / 'DungeonStory-Qwen3-8B-V10-LoRA-F32.gguf'
    require(adapter.is_file() and adapter.stat().st_size > 0, 'Converted trained adapter missing')
    require(not (root / 'runtime').exists(), 'Create-only runtime already exists')
    bundle = root / 'runtime' / 'DungeonStoryLlm'
    bundle.mkdir(parents=True)
    for archive in (name for name in sources if name.endswith('.zip')):
        with zipfile.ZipFile(root / archive) as z:
            for entry in z.infolist():
                if entry.is_dir():
                    continue
                relative = Path(entry.filename)
                require(not relative.is_absolute() and '..' not in relative.parts, 'Unsafe archive path')
                # Runtime binaries only; keep examples/docs in the downloaded archive.
                if relative.suffix.lower() not in ('.exe', '.dll'):
                    continue
                if relative.suffix.lower() == '.exe' and relative.name != 'llama-server.exe':
                    continue
                target = bundle / relative.name
                require(not target.exists(), 'Duplicate runtime binary: ' + target.name)
                with z.open(entry) as source, target.open('xb') as output:
                    shutil.copyfileobj(source, output)
    model_name = 'Qwen3-8B-Q4_K_M.gguf'
    os.link(root / 'downloads/Qwen3-8B-Q4_K_M-r2.gguf', bundle / model_name)
    os.link(adapter, bundle / adapter.name)
    server = bundle / 'llama-server.exe'
    require(server.is_file() and (bundle / 'ggml-cuda.dll').is_file(), 'Official CUDA server missing')
    manifest = dict(protocolVersion=25, hostKind='LlamaCppServer', hostWindows=server.name,
        hostWindowsSha256=sha(server),
        supportFiles=[dict(file=p.name, sha256=sha(p)) for p in sorted(bundle.glob('*.dll'))],
        modelFile=model_name, modelSha256=sources['downloads/Qwen3-8B-Q4_K_M-r2.gguf'],
        maximumModelBytes=5027783488, modelAlias='DungeonStory-Qwen3-8B-V10-Q4_K_M-LoRA',
        adapterFile=adapter.name, adapterSha256=sha(adapter), gpuLayers=99, contextSize=8192, parallelSlots=1,
        modelVersion='Qwen3-8B-SFT-V10/Q4_K_M+F32-LoRA@llama.cpp-b10331',
        releaseCertified=False, trainingState='sft-v10-local-4bit-test')
    with (bundle / 'manifest.json').open('x', encoding='utf-8') as output:
        json.dump(manifest, output, ensure_ascii=False, indent=2)
    receipt = dict(status='PREPARED_NOT_INFERENCE_PASS', sourcePins=sources,
        adapterGgufSha256=manifest['adapterSha256'], modelWeightBytes=5027783488,
        adapterWeightBytes=adapter.stat().st_size,
        deploymentBytes=sum(p.stat().st_size for p in bundle.iterdir() if p.is_file()),
        baseRevision='b968826d9c46dd6066d109eabc6255188de91218',
        quantizedBaseRevision='7c41481f57cb95916b40956ab2f0b139b296d974',
        manifestSha256=sha(bundle / 'manifest.json'), trainingEligible=False, releaseEligible=False)
    with (root / 'bundle-receipt.json').open('x', encoding='utf-8') as output:
        json.dump(receipt, output, ensure_ascii=False, indent=2)
    print(json.dumps(receipt, ensure_ascii=False))


if __name__ == '__main__':
    main()
