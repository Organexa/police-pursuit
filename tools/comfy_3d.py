"""Turn one image into a 3D mesh (.obj) with the local ComfyUI and TripoSR. Free, private, slow on a CPU.

Usage:
    python tools/comfy_3d.py art/generated/20261128-150000-police-car.png
    python tools/comfy_3d.py photo.png --name police-car --resolution 256

Best input: one object, whole, centred, on a plain background (make it with comfy_image.py and say
"isolated on a plain grey background, three-quarter view"); the background is removed automatically (BiRefNet).
The mesh carries vertex colours, no texture. ComfyUI must be running first: double-click C:\\ComfyUI\\run_cpu.bat.
Needs the ComfyUI-Flowty-TripoSR node, models/checkpoints/triposr.ckpt and
models/background_removal/birefnet.safetensors. Meshes go to art/generated/ (git-ignored).
"""
import argparse
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime
from pathlib import Path

from comfy_image import OUT, call, slug

MODEL = "triposr.ckpt"
BG_MODEL = "birefnet.safetensors"


def workflow(image: str, resolution: int, threshold: float) -> dict:
    # BiRefNet cuts the object out first: otherwise TripoSR rebuilds the background as walls stuck to the object.
    return {
        "1": {"class_type": "TripoSRModelLoader", "inputs": {"model": MODEL, "chunk_size": 8192}},
        "2": {"class_type": "LoadImage", "inputs": {"image": image}},
        "5": {"class_type": "LoadBackgroundRemovalModel", "inputs": {"bg_removal_name": BG_MODEL}},
        "6": {"class_type": "RemoveBackground", "inputs": {"bg_removal_model": ["5", 0], "image": ["2", 0]}},
        "3": {"class_type": "TripoSRSampler", "inputs": {
            "model": ["1", 0], "reference_image": ["2", 0], "reference_mask": ["6", 0],
            "geometry_resolution": resolution, "threshold": threshold}},
        "4": {"class_type": "TripoSRViewer", "inputs": {"mesh": ["3", 0]}},  # saves an .obj in ComfyUI's output
    }


def upload(server: str, path: Path) -> str:
    # ComfyUI's /upload/image takes multipart form data; the standard library has no helper for it.
    boundary = uuid.uuid4().hex
    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"{path.name}\"\r\n"
            f"Content-Type: application/octet-stream\r\n\r\n").encode() + path.read_bytes() + \
           f"\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"overwrite\"\r\n\r\ntrue\r\n--{boundary}--\r\n".encode()
    req = urllib.request.Request(f"{server}/upload/image", data=body,
                                 headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read())["name"]


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("image", type=Path)
    p.add_argument("--name", help="file name stem (default: from the image name)")
    p.add_argument("--resolution", type=int, default=256, help="marching-cubes grid, 128-512 (higher: finer, slower)")
    p.add_argument("--threshold", type=float, default=25.0, help="surface density (lower keeps thin parts)")
    p.add_argument("--server", default="http://127.0.0.1:8188")
    p.add_argument("--wait", type=float, default=1800, help="seconds to wait for the mesh")
    a = p.parse_args()

    if not a.image.is_file():
        print(f"No such image: {a.image}", file=sys.stderr)
        return 2
    if not 128 <= a.resolution <= 512:
        print("--resolution must be 128 to 512", file=sys.stderr)
        return 2

    try:
        name = upload(a.server, a.image)
        queued = json.loads(call(f"{a.server}/prompt", {"prompt": workflow(name, a.resolution, a.threshold)}))
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

    print("Queued; this takes minutes on a CPU...", file=sys.stderr)
    deadline = time.time() + a.wait
    meshes = None
    while time.time() < deadline:
        entry = json.loads(call(f"{a.server}/history/{job}")).get(job)
        if entry:
            status = entry.get("status", {})
            if status.get("status_str") == "error":
                print(f"ComfyUI failed: {json.dumps(status.get('messages', []))[:800]}", file=sys.stderr)
                return 1
            meshes = [m for node in entry.get("outputs", {}).values() for m in node.get("mesh", [])]
            if meshes:
                break
        time.sleep(3)
    if not meshes:
        print(f"No mesh after {a.wait:.0f} s.", file=sys.stderr)
        return 1

    OUT.mkdir(parents=True, exist_ok=True)
    stem = f"{datetime.now():%Y%m%d-%H%M%S}-{a.name or slug(a.image.stem)}"
    m = meshes[0]
    query = urllib.parse.urlencode({"filename": m["filename"], "subfolder": m.get("subfolder", ""), "type": m.get("type", "output")})
    path = OUT / f"{stem}.obj"
    path.write_bytes(call(f"{a.server}/view?{query}", timeout=120))
    (OUT / f"{stem}.txt").write_text(
        f"from {a.image}\nmodel={MODEL} resolution={a.resolution} threshold={a.threshold} (ComfyUI TripoSR, CPU)\n",
        encoding="utf-8")
    print(path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
