"""Make an image with OpenAI's image API (the model behind ChatGPT's images) and save it as a PNG.

Usage:
    python tools/gen_image.py "A photorealistic police car on a rainy neon street at night" --name police-car
    python tools/gen_image.py "..." --size 1536x1024 --quality high --n 2

Needs an OpenAI API key in the OPENAI_API_KEY environment variable (platform.openai.com; this is billed
separately from a ChatGPT subscription). The key is read from the environment only and is never printed.
Images go to art/generated/ (git-ignored) with the prompt saved next to each one.
"""
import argparse
import base64
import json
import os
import re
import sys
import urllib.error
import urllib.request
from datetime import datetime
from pathlib import Path

API = "https://api.openai.com/v1/images/generations"
OUT = Path(__file__).resolve().parent.parent / "art" / "generated"


def slug(text: str) -> str:
    s = re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")
    return s[:40] or "image"


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("prompt")
    p.add_argument("--name", help="file name stem (default: from the prompt)")
    p.add_argument("--size", default="1024x1024", help="1024x1024, 1536x1024 (wide) or 1024x1536 (tall)")
    p.add_argument("--quality", default="medium", choices=["low", "medium", "high"])
    p.add_argument("--n", type=int, default=1, help="how many variants")
    p.add_argument("--model", default="gpt-image-1")
    a = p.parse_args()

    key = os.environ.get("OPENAI_API_KEY", "").strip()
    if not key:
        print("OPENAI_API_KEY is not set. Create a key at platform.openai.com, then set it for your user, e.g. in PowerShell:\n"
              "  [Environment]::SetEnvironmentVariable('OPENAI_API_KEY', '<your key>', 'User')\n"
              "and open a new terminal.", file=sys.stderr)
        return 2
    if not 1 <= a.n <= 4:
        print("--n must be 1 to 4", file=sys.stderr)
        return 2

    body = json.dumps({"model": a.model, "prompt": a.prompt, "size": a.size, "quality": a.quality, "n": a.n}).encode()
    req = urllib.request.Request(API, data=body, headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=180) as r:
            data = json.load(r)
    except urllib.error.HTTPError as e:
        # The error body says why (bad size, content policy, no billing); it never contains the key.
        print(f"OpenAI refused ({e.code}): {e.read().decode(errors='replace')[:600]}", file=sys.stderr)
        return 1
    except urllib.error.URLError as e:
        print(f"Could not reach OpenAI: {e.reason}", file=sys.stderr)
        return 1

    OUT.mkdir(parents=True, exist_ok=True)
    stem = f"{datetime.now():%Y%m%d-%H%M%S}-{a.name or slug(a.prompt)}"
    images = data.get("data") or []
    if not images:
        print("No image came back.", file=sys.stderr)
        return 1
    for i, item in enumerate(images):
        b64 = item.get("b64_json")
        if not b64:
            print("Unexpected answer: no image data.", file=sys.stderr)
            return 1
        path = OUT / (f"{stem}.png" if len(images) == 1 else f"{stem}-{i + 1}.png")
        path.write_bytes(base64.b64decode(b64))
        print(path)
    (OUT / f"{stem}.txt").write_text(f"{a.prompt}\n\nmodel={a.model} size={a.size} quality={a.quality} n={a.n}\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
