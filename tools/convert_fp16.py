"""
convert_fp16.py — Convert ONNX models from FP32 to FP16 for DirectML (4070 Super / Ada Lovelace)

Usage:
    python convert_fp16.py                          # convert all models/ *.onnx
    python convert_fp16.py path/to/model.onnx       # convert a single file
    python convert_fp16.py models/ --out models/fp16/   # custom output dir

Requirements:
    pip install onnx onnxconverter-common

Why FP16?
    Ada Lovelace (RTX 4070 Super) has 4th-gen tensor cores that run FP16 at
    roughly 2-3x the throughput of FP32. DirectML automatically routes FP16
    ops to tensor cores, so swapping in a converted model requires zero C# changes.

Output:
    Each <name>.onnx → <name>_fp16.onnx (original is untouched).
    The app will pick up *_fp16.onnx automatically if you rename it to replace
    the original, or you can point the model picker at the new file.
"""

import sys
import os
import argparse
import traceback

try:
    import onnx
    from onnxconverter_common import float16
except ImportError:
    print("ERROR: Missing dependencies. Run:  pip install onnx onnxconverter-common")
    sys.exit(1)


def convert_file(src: str, dst: str) -> bool:
    try:
        model = onnx.load(src)
        model_fp16 = float16.convert_float_to_float16(model, keep_io_types=True)
        onnx.save(model_fp16, dst)
        src_mb = os.path.getsize(src) / 1_048_576
        dst_mb = os.path.getsize(dst) / 1_048_576
        print(f"  OK  {os.path.basename(src):60s}  {src_mb:6.1f} MB → {dst_mb:6.1f} MB")
        return True
    except Exception:
        print(f"  FAIL {os.path.basename(src)}")
        traceback.print_exc()
        return False


def collect_models(path: str):
    if os.path.isfile(path):
        return [path]
    results = []
    for entry in sorted(os.scandir(path), key=lambda e: e.name):
        if entry.is_file() and entry.name.endswith(".onnx") and not entry.name.endswith("_fp16.onnx"):
            results.append(entry.path)
    return results


def main():
    parser = argparse.ArgumentParser(description="Convert ONNX FP32 models to FP16 for DirectML")
    parser.add_argument("input", nargs="?", default="models",
                        help="Input file or directory (default: models/)")
    parser.add_argument("--out", default=None,
                        help="Output directory (default: same directory as each source file)")
    args = parser.parse_args()

    models = collect_models(args.input)
    if not models:
        print(f"No .onnx files found in: {args.input}")
        sys.exit(1)

    if args.out:
        os.makedirs(args.out, exist_ok=True)

    print(f"\nConverting {len(models)} model(s) to FP16\n")
    ok = fail = 0
    for src in models:
        if args.out:
            dst = os.path.join(args.out, os.path.splitext(os.path.basename(src))[0] + "_fp16.onnx")
        else:
            dst = os.path.splitext(src)[0] + "_fp16.onnx"
        if convert_file(src, dst):
            ok += 1
        else:
            fail += 1

    print(f"\nDone: {ok} converted, {fail} failed")


if __name__ == "__main__":
    main()
