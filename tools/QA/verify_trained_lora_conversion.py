"""Check every converted F32 LoRA tensor against the trained safetensors; no inference."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import numpy as np
from safetensors import safe_open


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve(strict=True)
    sys.path.insert(0, str(root / 'llama.cpp' / 'gguf-py'))
    import gguf

    config = json.loads((root / 'base-config/config.json').read_text())
    mapping = gguf.get_tensor_name_map(gguf.MODEL_ARCH.QWEN3, config['num_hidden_layers'])
    converted = root / 'DungeonStory-Qwen3-8B-V10-LoRA-F32.gguf'
    reader = gguf.GGUFReader(converted)
    tensors = {tensor.name: tensor for tensor in reader.tensors}
    checked = set()
    with safe_open(str(root / 'adapter/adapter_model.safetensors'), framework='numpy') as original:
        for name in original.keys():
            base = name.replace('base_model.model.', '').replace('.lora_A.weight', '.weight').replace('.lora_B.weight', '.weight')
            target = mapping.get_name(base, try_suffixes=('.weight',))
            if target is None:
                raise ValueError('Unmapped source tensor: ' + name)
            mapped = target + ('.lora_a' if '.lora_A.' in name else '.lora_b')
            source = original.get_tensor(name)
            if mapped in checked or mapped not in tensors:
                raise ValueError('Missing or duplicate converted tensor: ' + mapped)
            value = tensors[mapped].data
            if source.dtype != np.float32 or value.dtype != np.float32 or value.shape != source.shape or not np.array_equal(value, source):
                raise ValueError('Conversion changed an F32 tensor: ' + mapped)
            checked.add(mapped)
    if len(checked) != len(tensors) or len(checked) != 504:
        raise ValueError('Unexpected or unbound tensors')
    with converted.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    result = dict(status='PASS', bitExactFloat32LoraTensors=len(checked),
        layers=config['num_hidden_layers'], convertedSha256=digest,
        modelInferenceRun=False, unityRun=False, quantizedModelQualityAccepted=False)
    with (root / 'lora-conversion-verification.json').open('x', encoding='utf-8') as output:
        json.dump(result, output, indent=2)
    print(json.dumps(result))


if __name__ == '__main__':
    main()
