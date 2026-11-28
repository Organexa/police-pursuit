"""Make a TripoSR mesh game-ready for Unity: fewer triangles, real size, Y up, front +Z, colours baked to a texture.

Usage (run with the ComfyUI Python, which has trimesh, xatlas and fast-simplification):
    C:\\ComfyUI\\venv\\Scripts\\python.exe tools/game_mesh.py art/generated/20261128-151719-police-car.obj --length 4.6
    ... --yaw 180 --triangles 6000 --texture 512

Writes <name>-game/<name>.obj + .mtl + .png next to the input (art/generated/ is git-ignored).
Unity's OBJ importer ignores vertex colours, so they are baked into a texture on a fresh UV unwrap.
--yaw turns the model about Y (degrees) when its front does not face +Z after the automatic length-along-Z turn.
"""
import argparse
import sys
from pathlib import Path

import fast_simplification
import numpy as np
import trimesh
import xatlas
from PIL import Image
from scipy import ndimage
from scipy.spatial import cKDTree


def orient(v: np.ndarray, yaw: float, length: float, width: float | None, height: float | None) -> np.ndarray:
    # TripoSR's export is already Y up; turn the longer floor axis onto Z, then the user's yaw.
    ext = v.max(0) - v.min(0)
    if ext[0] > ext[2]:
        v = v[:, [2, 1, 0]] * np.array([1, 1, -1])  # 90 degrees about Y, keeps handedness
    a = np.radians(yaw)
    rot = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])
    v = v @ rot.T
    lo, hi = v.min(0), v.max(0)
    v = (v - np.array([(lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2])) * (length / (hi[2] - lo[2]))
    # TripoSR guesses depth from one picture and often makes things too fat: squash to the real size if given.
    ext = v.max(0) - v.min(0)
    if width:
        v[:, 0] *= width / ext[0]
    if height:
        v[:, 1] *= height / ext[1]
    return v  # sits on y = 0, centred on x and z


def bake(verts, faces, uvs, src_points, src_colors, size: int) -> Image.Image:
    # Rasterise every triangle in UV space; each texel takes the colour of the nearest source vertex.
    px = uvs * (size - 1)
    pts, at = [], []
    for tri in faces:
        p = px[tri]
        x0, y0 = np.floor(p.min(0)).astype(int)
        x1, y1 = np.ceil(p.max(0)).astype(int)
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        q = np.stack([xs.ravel(), ys.ravel()], 1).astype(float)
        d = (p[1, 1] - p[2, 1]) * (p[0, 0] - p[2, 0]) + (p[2, 0] - p[1, 0]) * (p[0, 1] - p[2, 1])
        if abs(d) < 1e-12:
            continue
        w0 = ((p[1, 1] - p[2, 1]) * (q[:, 0] - p[2, 0]) + (p[2, 0] - p[1, 0]) * (q[:, 1] - p[2, 1])) / d
        w1 = ((p[2, 1] - p[0, 1]) * (q[:, 0] - p[2, 0]) + (p[0, 0] - p[2, 0]) * (q[:, 1] - p[2, 1])) / d
        w = np.stack([w0, w1, 1 - w0 - w1], 1)
        inside = (w >= -0.02).all(1)  # a hair over the edge so seams have no gaps
        if not inside.any():
            continue
        pts.append(w[inside] @ verts[tri])
        at.append(q[inside].astype(int))
    pts, at = np.concatenate(pts), np.concatenate(at)
    _, nearest = cKDTree(src_points).query(pts)
    img = np.zeros((size, size, 3), np.uint8)
    filled = np.zeros((size, size), bool)
    img[at[:, 1], at[:, 0]] = src_colors[nearest]
    filled[at[:, 1], at[:, 0]] = True
    # Pad: every empty texel copies its nearest filled one, so mipmaps do not bleed black into the seams.
    _, (iy, ix) = ndimage.distance_transform_edt(~filled, return_indices=True)
    img = img[iy, ix]
    return Image.fromarray(img[::-1])  # row 0 of the image is the top (v = 1)


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("mesh", type=Path)
    p.add_argument("--length", type=float, required=True, help="real length front to back, metres")
    p.add_argument("--width", type=float, help="real width, metres (default: keep proportions)")
    p.add_argument("--height", type=float, help="real height, metres (default: keep proportions)")
    p.add_argument("--yaw", type=float, default=0, help="extra turn about Y, degrees")
    p.add_argument("--triangles", type=int, default=8000)
    p.add_argument("--texture", type=int, default=1024, help="texture size in pixels")
    a = p.parse_args()

    src = trimesh.load(a.mesh, force="mesh", process=False)
    if src.visual.kind != "vertex":
        print("The mesh has no vertex colours (expected a TripoSR .obj).", file=sys.stderr)
        return 2
    colors = np.asarray(src.visual.vertex_colors)[:, :3]
    verts = orient(np.asarray(src.vertices, float), a.yaw, a.length, a.width, a.height)

    keep = min(1.0, a.triangles / len(src.faces))
    v, f = fast_simplification.simplify(verts.astype(np.float32), np.asarray(src.faces, np.int32), 1 - keep)
    vmap, idx, uvs = xatlas.parametrize(v, f)
    v, f = v[vmap], idx

    tex = bake(v, f, uvs, verts, colors, a.texture)
    out_dir = a.mesh.parent / f"{a.mesh.stem}-game"
    out_dir.mkdir(exist_ok=True)
    # White diffuse: Unity multiplies the texture by Kd, and trimesh's default 0.4 grey would darken it.
    material = trimesh.visual.material.SimpleMaterial(image=tex, name=a.mesh.stem, diffuse=(255, 255, 255, 255),
                                                      ambient=(255, 255, 255, 255), specular=(0, 0, 0, 255))
    mesh = trimesh.Trimesh(v, f, visual=trimesh.visual.TextureVisuals(uv=uvs, material=material), process=False)
    mesh.fix_normals()
    path = out_dir / f"{a.mesh.stem}.obj"
    mesh.export(path)
    print(f"{path}  triangles={len(f)} vertices={len(v)} size(m)={np.round(mesh.extents, 2)} texture={a.texture}px")
    return 0


if __name__ == "__main__":
    sys.exit(main())
