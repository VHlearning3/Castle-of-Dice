"""Renders the Choose Your Hero portraits (Assets/Resources/HeroPortraits/<Hero>.png).

Input: BlenderSources/HeroPortraits/<Hero>/pose.obj + pose.json, written by Unity's
CastleOfTheD20.Editor.HeroPortraitExport (the hero posed exactly as in game, weapons included).
Each hero gets a three-quarter waist-up view, warm key light, a coloured rim in the hero's colour,
a painted backdrop, a vignette and a gold frame matching the hero cards.

Run in background Blender:
  blender -b --factory-startup --python BlenderSources/HeroPortraits_build.py -- <project root> [Hero ...]
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

W, H = 500, 540          # 250 x 270 UI units at 2x
SAMPLES = 96

HEROES = {
    # backdrop centre / edge, rim colour, camera yaw (deg, + = hero's left), frame height (share of body), lift
    "Warrior": dict(bg=(0.42, 0.11, 0.07), edge=(0.035, 0.02, 0.02), rim=(1.0, 0.45, 0.25), key=0.45, yaw=22, frame=0.72, lift=0.0, motes=(1.0, 0.6, 0.25)),
    "Mage":    dict(bg=(0.20, 0.13, 0.46), edge=(0.025, 0.02, 0.06), rim=(0.55, 0.6, 1.0), key=1.0, yaw=26, frame=0.74, lift=0.05, motes=(0.75, 0.8, 1.0)),
    "Rogue":   dict(bg=(0.06, 0.30, 0.22), edge=(0.015, 0.035, 0.03), rim=(0.45, 1.0, 0.7), key=1.0, yaw=30, frame=0.72, lift=0.0, motes=(0.6, 1.0, 0.75)),
}


def unity_to_blender(v):
    # pose.json is in OBJ space (Unity with x mirrored); the OBJ importer maps (x, y, z) -> (x, -z, y)
    return Vector((v[0], -v[2], v[1]))


def build_scene(root, hero):
    cfg = HEROES[hero]
    src = os.path.join(root, "BlenderSources", "HeroPortraits", hero)
    meta = json.load(open(os.path.join(src, "pose.json")))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.obj_import(filepath=os.path.join(src, "pose.obj"), forward_axis='NEGATIVE_Z', up_axis='Y')
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']

    mats = {m["name"]: m for m in meta["materials"]}
    for mat in bpy.data.materials:
        info = mats.get(mat.name)
        if info is None:
            continue
        mat.use_nodes = True
        nt = mat.node_tree
        bsdf = nt.nodes.get("Principled BSDF")
        for n in list(nt.nodes):
            if n.type == 'TEX_IMAGE':
                nt.nodes.remove(n)
        bsdf.inputs["Metallic"].default_value = info["metallic"]
        bsdf.inputs["Roughness"].default_value = max(0.25, 1.0 - info["smoothness"])
        col = info["color"]
        if info["texture"] and os.path.exists(info["texture"]):
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = bpy.data.images.load(info["texture"], check_existing=True)
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = 'RGBA'
            mix.blend_type = 'MULTIPLY'
            mix.inputs["Factor"].default_value = 1.0
            nt.links.new(tex.outputs["Color"], mix.inputs[6])
            mix.inputs[7].default_value = (col[0], col[1], col[2], 1)
            nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
        else:
            bsdf.inputs["Base Color"].default_value = (col[0], col[1], col[2], 1)

    head = unity_to_blender(meta["head"])
    zmin = min((o.matrix_world @ Vector(c)).z for o in objs for c in o.bound_box)
    body = head.z - zmin
    front = unity_to_blender(meta["forward"]).normalized()

    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = SAMPLES
    sc.cycles.use_denoising = True
    sc.cycles.device = 'CPU'
    sc.render.resolution_x, sc.render.resolution_y = W, H
    sc.render.film_transparent = True
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Medium High Contrast'

    world = bpy.data.worlds.new("World")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (*[c * 0.6 for c in cfg["bg"]], 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.25

    # Camera: three-quarter view, slightly from above, framing head to waist
    frame_h = cfg["frame"] * body
    centre = head + Vector((0, 0, 0.25 * body - 0.5 * frame_h + cfg["lift"] * body))
    yaw = math.radians(cfg["yaw"])
    dir_ = Vector((front.x * math.cos(yaw) - front.y * math.sin(yaw), front.x * math.sin(yaw) + front.y * math.cos(yaw), 0)).normalized()
    dir_ = (dir_ + Vector((0, 0, 0.12))).normalized()
    cam_data = bpy.data.cameras.new("PortraitCam")
    cam_data.lens = 70
    cam_data.sensor_fit = 'VERTICAL'
    cam = bpy.data.objects.new("PortraitCam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    fov = 2 * math.atan(cam_data.sensor_height / (2 * cam_data.lens))
    dist = frame_h * 0.5 / math.tan(fov * 0.5)
    cam.location = centre + dir_ * dist
    cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam_data.clip_start = dist * 0.05
    cam_data.clip_end = dist * 20

    def area(name, color, power, pos, size):
        d = bpy.data.lights.new(name, 'AREA')
        d.color = color
        d.energy = power
        d.size = size
        o = bpy.data.objects.new(name, d)
        sc.collection.objects.link(o)
        o.location = pos
        o.rotation_euler = (head - o.location).to_track_quat('-Z', 'Y').to_euler()
        return o

    side = Vector((-dir_.y, dir_.x, 0)).normalized()
    s = body  # light distances scale with the hero
    area("Key", (1.0, 0.86, 0.68), 110 * cfg["key"] * s * s, head + dir_ * 0.9 * s + side * 0.7 * s + Vector((0, 0, 0.55 * s)), 0.6 * s)
    area("Fill", (0.7, 0.75, 0.9), 22 * cfg["key"] * s * s, head + dir_ * 0.9 * s - side * 0.9 * s + Vector((0, 0, 0.1 * s)), 0.9 * s)
    area("RimL", cfg["rim"], 160 * s * s, head - dir_ * 0.7 * s + side * 0.6 * s + Vector((0, 0, 0.35 * s)), 0.3 * s)
    area("RimR", cfg["rim"], 110 * s * s, head - dir_ * 0.7 * s - side * 0.65 * s + Vector((0, 0, 0.2 * s)), 0.3 * s)
    return cfg


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def composite(render_path, out_path, cfg, seed):
    img = bpy.data.images.load(render_path)
    px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)   # bottom-up rows
    rgb, a = px[..., :3], px[..., 3:4]

    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u = (xx + 0.5) / W * 2 - 1
    v = (yy + 0.5) / H * 2 - 1
    # Backdrop: glow behind the head, fading to the hero's dark edge colour
    d = np.sqrt((u * 1.0) ** 2 + ((v - 0.35) * 0.85) ** 2)
    g = smoothstep(0.0, 1.35, d)[..., None]
    bg = np.array(cfg["bg"], np.float32) * (1 - g) + np.array(cfg["edge"], np.float32) * g
    # Soft drifting haze and a few glowing motes for depth
    rng = np.random.default_rng(seed)
    haze = np.zeros((H, W), np.float32)
    for _ in range(9):
        cx, cy, r = rng.uniform(-1, 1), rng.uniform(-0.8, 1), rng.uniform(0.25, 0.6)
        haze += np.exp(-((u - cx) ** 2 + (v - cy) ** 2) / (r * r)) * rng.uniform(0.04, 0.09)
    bg = bg * (1 + haze[..., None])
    motes = np.zeros((H, W), np.float32)
    for _ in range(26):
        cx, cy, r = rng.uniform(-0.95, 0.95), rng.uniform(-0.9, 0.95), rng.uniform(0.006, 0.02)
        motes += np.exp(-((u - cx) ** 2 + ((v - cy) * H / W) ** 2) / (r * r)) * rng.uniform(0.25, 0.8)
    bg = bg + np.clip(motes, 0, 1)[..., None] * np.array(cfg["motes"], np.float32) * 0.55
    # Bottom fade so the bust sinks into darkness
    bottom = smoothstep(-1.0, -0.35, v)[..., None]
    out = rgb * a + bg * (1 - a)
    out = out * (0.35 + 0.65 * bottom)
    # Vignette
    vig = 1 - 0.55 * smoothstep(0.55, 1.45, np.sqrt(u * u + v * v))[..., None]
    out = out * vig

    # Gold frame: dark outer line, bevelled gold band, dark inner line, inner shadow
    edge = np.minimum(np.minimum(xx, W - 1 - xx), np.minimum(yy, H - 1 - yy))
    gold_hi = np.array((1.0, 0.86, 0.45), np.float32)
    gold_lo = np.array((0.55, 0.36, 0.1), np.float32)
    band = (edge >= 2) & (edge < 9)
    t = ((edge - 2) / 7.0)[..., None]
    bevel = np.abs(t - 0.4) / 0.6
    gold = gold_hi * (1 - bevel) + gold_lo * bevel
    out = np.where(band[..., None], gold, out)
    out = np.where((edge < 2)[..., None], np.array((0.08, 0.05, 0.02), np.float32), out)
    out = np.where(((edge >= 9) & (edge < 11))[..., None], np.array((0.12, 0.07, 0.02), np.float32), out)
    shadow = 1 - 0.45 * (1 - smoothstep(11, 26, edge))[..., None]
    out = np.where((edge >= 11)[..., None], out * shadow, out)

    res = np.concatenate([np.clip(out, 0, 1), np.ones((H, W, 1), np.float32)], axis=2)
    dst = bpy.data.images.new("portrait", W, H, alpha=False)
    dst.pixels[:] = res.ravel()
    dst.filepath_raw = out_path
    dst.file_format = 'PNG'
    dst.save()


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = argv[0] if argv else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    heroes = argv[1:] or list(HEROES)
    out_dir = os.path.join(root, "Assets", "Resources", "HeroPortraits")
    os.makedirs(out_dir, exist_ok=True)
    tmp = os.path.join(root, "BlenderSources", "HeroPortraits")
    for i, hero in enumerate(heroes):
        cfg = build_scene(root, hero)
        raw = os.path.join(tmp, hero, "raw.png")
        bpy.context.scene.render.filepath = raw
        bpy.context.scene.render.image_settings.file_format = 'PNG'
        bpy.context.scene.render.image_settings.color_mode = 'RGBA'
        bpy.ops.render.render(write_still=True)
        composite(raw, os.path.join(out_dir, hero + ".png"), cfg, seed=7 + i)
        print("[HeroPortraits]", hero, "->", os.path.join(out_dir, hero + ".png"))


main()
