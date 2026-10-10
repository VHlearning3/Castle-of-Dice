"""Sir Roland's armour texture in steel grey.

The Tripo texture (Assets/Characters/tripo_rgb_9f4fbd7c-....jpg) has large dark navy patches that read as
holes and smudges on the armour. This keeps its light/dark shape but maps it onto a narrow steel-grey range:
navy becomes mid steel, highlights stay light, fine grooves stay a little darker. The original file is kept;
the knight's material (M_PlayerKnight) points at the result, Assets/Characters/PlayerKnight_Steel.jpg.

Run: blender -b --factory-startup --python BlenderSources/Knight_SteelTexture.py -- <project root>
"""
import os
import sys

import bpy
import numpy as np

SOURCE = "Assets/Characters/tripo_rgb_9f4fbd7c-4c41-4556-8b04-e296ba1d0d00.jpg"
TARGET = "Assets/Characters/PlayerKnight_Steel.jpg"

GREY_FLOOR = 0.32      # what the darkest navy becomes
GREY_RANGE = 0.34      # floor + range = brightest highlight
TINT = np.array((0.97, 0.99, 1.02), np.float32)   # cool steel
KEEP_CHROMA = 0.12     # how much of the original blue survives


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = argv[0] if argv else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    floor, rng = GREY_FLOOR, GREY_RANGE
    if os.environ.get("KNIGHT_GREY"):   # "floor,range" for trying other greys
        floor, rng = (float(v) for v in os.environ["KNIGHT_GREY"].split(","))
    target = os.environ.get("KNIGHT_GREY_OUT") or os.path.join(root, TARGET)
    src = bpy.data.images.load(os.path.join(root, SOURCE))
    w, h = src.size
    px = np.array(src.pixels[:], dtype=np.float32).reshape(h, w, 4)
    rgb = px[..., :3]
    lum = (rgb @ np.array((0.299, 0.587, 0.114), np.float32))[..., None]
    lo, hi = np.percentile(lum, 2), np.percentile(lum, 98)
    t = np.clip((lum - lo) / max(hi - lo, 1e-4), 0, 1)
    grey = (floor + rng * t) * TINT
    out = np.clip(grey + KEEP_CHROMA * (rgb - lum), 0, 1)

    dst = bpy.data.images.new("PlayerKnight_Steel", w, h, alpha=False)
    dst.pixels[:] = np.concatenate([out, np.ones((h, w, 1), np.float32)], axis=2).ravel()
    dst.filepath_raw = target
    dst.file_format = 'JPEG'
    bpy.context.scene.render.image_settings.quality = 92
    dst.save(quality=92)
    print("[KnightSteel] wrote", target, w, "x", h)


main()
