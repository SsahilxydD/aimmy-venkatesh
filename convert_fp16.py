"""Convert an ONNX model from float32 to float16, keeping IO types as float32.

keep_io_types=True wraps the model with cast nodes so the model still accepts
and returns float32 tensors — no changes needed in the C# inference code.
The interior compute runs in fp16, which is ~2x faster on Ada (RTX 40-series)
tensor cores via DirectML.
"""

import argparse
import os
import sys

import onnx
from onnxconverter_common import float16


def convert(src: str, dst: str) -> None:
    print(f"loading: {src}")
    model = onnx.load(src)

    src_size = os.path.getsize(src)
    print(f"source size: {src_size / (1024 * 1024):.2f} MB")

    print("converting fp32 -> fp16 (keep_io_types=True, disable_shape_infer=False)...")
    fp16_model = float16.convert_float_to_float16(
        model,
        keep_io_types=True,
        disable_shape_infer=False,
    )

    print(f"saving: {dst}")
    onnx.save(fp16_model, dst)

    dst_size = os.path.getsize(dst)
    print(f"output size: {dst_size / (1024 * 1024):.2f} MB ({dst_size / src_size:.1%} of original)")

    print("verifying output is a valid ONNX model...")
    onnx.checker.check_model(dst)
    print("ok.")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("src", help="path to fp32 .onnx model")
    parser.add_argument("dst", help="path to write fp16 .onnx model")
    args = parser.parse_args()
    convert(args.src, args.dst)
    return 0


if __name__ == "__main__":
    sys.exit(main())
