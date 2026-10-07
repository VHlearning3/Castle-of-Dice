# Elf waitress build scripts

Background-Blender scripts that turned Tripo's `Brown fantasy elf 3d model.glb` into
`BlenderSources/Elf_Waitress.blend` and `Assets/Characters/NPC_Elf_Waitress/Elf_Waitress.fbx` (2026-10-07).
They were run in order from a scratch folder (the `D` path at the top of each), each opening the previous stage's .blend:

1. `stage1_mesh.py`: new UV layout (face 2.6x bigger, skirt smaller, overlaps packed apart), 4096 texture baked to 2048.
2. `stage2_trays.py`: bread tray and beer-tankard tray on an 8x8 colour palette.
3. `stage3_rig.py`: Mixamo skeleton (from the Action Adventure Pack character) fitted to her joints.
4. `stage4_skin.py`: bone-heat weights on a 10x voxel proxy copied to the mesh, hair and arm fixes, trays on the hand bones.
5. `stage5_anims.py` (+ `retarget.py`, `poselib.py`): Idle and Walk (Mixamo, trays carried), Serve (hand-made), Attack and Die (knight clips).
6. `stage6_export.py`: .blend, 1024 game texture and the FBX.

Unity side: menu `CastleOfDice/Setup Elf Waitress (prefab only)` (`SetupElfWaitressEditor`).
