# Task Assignment: Milestone M1 Exploration (AudioManager Coexistence & Lifecycle)

## Identity
- Archetype: teamwork_preview_explorer
- Working Directory: D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_m1_3
- Parent: Orchestrator (ca3fb1df-84ce-4aaa-ac7d-83cdd973b003)

## Objective
Analyze Milestone M1 coexistence between `MusicManager` and `AudioManager`:
1. Read `D:\Unity\3D DnD selainpeli\.agents\teamwork\ORIGINAL_REQUEST.md` and `D:\Unity\3D DnD selainpeli\PROJECT.md`.
2. Inspect `Assets/Scripts/Core/AudioManager.cs` in detail.
3. Determine how `AudioManager.cs` should yield BGM playback to `MusicManager.cs`:
   - What happens in `AudioManager.Start()`?
   - What happens when `AudioManager` receives location change or combat ended events?
   - How to ensure SFX continue playing cleanly on `sfxSource` while BGM is exclusively driven by `MusicManager`.
4. Ensure Singleton lifecycle of `MusicManager`:
   - Awake logic, duplicate destruction, `DontDestroyOnLoad` behavior.

## Output
Write your findings to `D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_m1_3\analysis.md` and handoff report to `D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_m1_3\handoff.md`.
Then send a message to orchestrator.
