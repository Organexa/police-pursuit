"""Make an image with the local ComfyUI (SDXL Turbo) and save it as a PNG. Free, private, slow on a CPU.

Usage:
    python tools/comfy_image.py "A photorealistic police car on a rainy neon street at night" --name police-car
    python tools/comfy_image.py "..." --size 768x512 --steps 2 --seed 42

ComfyUI must be running first: double-click C:\\ComfyUI\\run_cpu.bat (it serves http://127.0.0.1:8188).
SDXL Turbo is made for 512x512 and 1-4 steps; bigger sizes or more steps are slower and not always better.
Images go to art/generated/ (git-ignored) with the prompt saved next to each one.
"""
import argparse
import json
import random
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "art" / "generated"
MODEL = "sd_xl_turbo_1.0_fp16.safetensors"


def slug(text: str) -> str:
    s = re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")
    return s[:40] or "image"


def workflow(prompt: str, negative: str, width: int, height: int, steps: int, seed: int) -> dict:
    # The smallest SDXL Turbo graph: no guidance (cfg 1), euler_ancestral, a few steps.
    return {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": MODEL}},
        "2": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["1", 1]}},
        "3": {"class_type": "CLIPTextEncode", "inputs": {"text": negative, "clip": ["1", 1]}},
        "4": {"class_type": "EmptyLatentImage", "inputs": {"width": width, "height": height, "batch_size": 1}},
        "5": {"class_type": "KSampler", "inputs": {
            "model": ["1", 0], "positive": ["2", 0], "negative": ["3", 0], "latent_image": ["4", 0],
            "seed": seed, "steps": steps, "cfg": 1.0, "sampler_name": "euler_ancestral", "scheduler": "normal",
            "denoise": 1.0}},
        "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
        "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": "police-pursuit"}},
    }


def call(url: str, data: dict | None = None, timeout: float = 30):
    body = json.dumps(data).encode() if data is not None else None
    req = urllib.request.Request(url, data=body, headers={"Content-Type": "application/json"} if body else {})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("prompt")
    p.add_argument("--name", help="file name stem (default: from the prompt)")
    p.add_argument("--negative", default="blurry, distorted, watermark, text, logo")
    p.add_argument("--size", default="512x512", help="WIDTHxHEIGHT, multiples of 64")
    p.add_argument("--steps", type=int, default=1, help="1-4 for SDXL Turbo")
    p.add_argument("--seed", type=int, help="repeatable result (default: random)")
    p.add_argument("--server", default="http://127.0.0.1:8188")
    p.add_argument("--wait", type=float, default=600, help="seconds to wait for the image")
    a = p.parse_args()

    try:
        width, height = (int(v) for v in a.size.lower().split("x"))
    except ValueError:
        print("--size must look like 512x512", file=sys.stderr)
        return 2
    if width % 64 or height % 64 or not (256 <= width <= 1536 and 256 <= height <= 1536):
        print("--size: width and height must be multiples of 64 between 256 and 1536", file=sys.stderr)
        return 2
    if not 1 <= a.steps <= 8:
        print("--steps must be 1 to 8", file=sys.stderr)
        return 2
    seed = a.seed if a.seed is not None else random.randrange(2**32)

    try:
        queued = json.loads(call(f"{a.server}/prompt", {"prompt": workflow(a.prompt, a.negative, width, height, a.steps, seed)}))
    except urllib.error.HTTPError as e:  # before URLError, which it is a kind of
        print(f"ComfyUI refused the job ({e.code}): {e.read().decode(errors='replace')[:600]}", file=sys.stderr)
        return 1
    except urllib.error.URLError as e:
        print(f"ComfyUI is not answering at {a.server} ({e.reason}). Start C:\\ComfyUI\\run_cpu.bat first.", file=sys.stderr)
        return 1
    job = queued.get("prompt_id")
    if not job:
        print(f"ComfyUI did not queue it: {queued}", file=sys.stderr)
        return 1

    print(f"Queued (seed {seed}); this takes a while on a CPU...", file=sys.stderr)
    deadline = time.time() + a.wait
    images = None
    while time.time() < deadline:
        history = json.loads(call(f"{a.server}/history/{job}"))
        entry = history.get(job)
        if entry:
            status = entry.get("status", {})
            if status.get("status_str") == "error":
                print(f"ComfyUI failed: {json.dumps(status.get('messages', []))[:600]}", file=sys.stderr)
                return 1
            outputs = entry.get("outputs", {})
            images = [img for node in outputs.values() for img in node.get("images", [])]
            if images:
                break
        time.sleep(2)
    if not images:
        print(f"No image after {a.wait:.0f} s.", file=sys.stderr)
        return 1

    OUT.mkdir(parents=True, exist_ok=True)
    stem = f"{datetime.now():%Y%m%d-%H%M%S}-{a.name or slug(a.prompt)}"
    for i, img in enumerate(images):
        query = urllib.parse.urlencode({"filename": img["filename"], "subfolder": img.get("subfolder", ""), "type": img.get("type", "output")})
        path = OUT / (f"{stem}.png" if len(images) == 1 else f"{stem}-{i + 1}.png")
        path.write_bytes(call(f"{a.server}/view?{query}", timeout=60))
        print(path)
    (OUT / f"{stem}.txt").write_text(
        f"{a.prompt}\n\nnegative={a.negative}\nmodel={MODEL} size={width}x{height} steps={a.steps} seed={seed} (ComfyUI, CPU)\n",
        encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
