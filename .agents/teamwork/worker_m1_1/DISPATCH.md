# Task Assignment: Milestone M1 Implementation (MusicManager Core & AudioManager Coexistence)

## Identity
- Archetype: teamwork_preview_worker
- Working Directory: D:\Unity\3D DnD selainpeli\.agents\teamwork\worker_m1_1
- Parent: Orchestrator (ca3fb1df-84ce-4aaa-ac7d-83cdd973b003)

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## Write Ownership
You have exclusive write ownership of:
- `Assets/Scripts/Core/MusicManager.cs`
- `Assets/Scripts/Core/AudioManager.cs`
Do NOT write to any other source files.

## Technical Specifications
Read the detailed analyses and blueprints created by the 3 M1 Explorers:
- `D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_m1_1\analysis.md` (Audio engine, equal-power crossfading, WebGL coroutine design, 2D stereo configuration)
- `D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_m1_2\analysis.md` (Track mapping for all 7 MP3s, MusicTrackType enum, public API)
- `D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_m1_3\analysis.md` (AudioManager BGM delegation, SFX preservation, singleton lifecycle)
- `D:\Unity\3D DnD selainpeli\PROJECT.md`
- `D:\Unity\3D DnD selainpeli\.agents\teamwork\ORIGINAL_REQUEST.md`

### Implementation Tasks:
1. Implement `Assets/Scripts/Core/MusicManager.cs`:
   - Persistent Singleton (`public static MusicManager Instance { get; private set; }`).
   - Dual-channel `AudioSource` ping-pong setup (`audioSourceA`, `audioSourceB`) with `spatialBlend = 0f` (2D stereo), `loop = true`, `playOnAwake = false`, `priority = 0`.
   - Equal-power ($\sin / \cos$) 1.2s smooth volume interpolation coroutine.
   - `MusicTrackType` enum with values for all 7 tracks (`None`, `Village`, `CastleAdventure`, `CellarCombat`, `CursedCommanderCombat`, `MalakorCombat`, `GargoyleKingPhase1`, `GargoyleKingPhase2`).
   - Serialized `AudioClip` fields for the 7 tracks.
   - Public API: `PlayMusic(AudioClip clip, float fadeDuration = 1.2f)`, `PlayTrack(MusicTrackType track, float fadeDuration = 1.2f)`, `PlayCombatMusicForBoss(string bossIdentifier, string roomLocation = "")`, `RestoreExplorationMusic(float delay = 1.5f)`, `StopMusic(float fadeDuration = 1.2f)`, `AssignClips(...)`.
   - Cancellation of pending exploration restore coroutines if combat is immediately re-engaged (e.g. Defeat Retry).
   - WebGL autoplay unmuting check on user interaction.
2. Update `Assets/Scripts/Core/AudioManager.cs`:
   - In `Start()`, check `if (MusicManager.Instance != null) return;` before playing BGM on `bgmSource`.
   - In `HandleLocationChanged()` and `HandlePlayModeChanged()`, yield BGM playback when `MusicManager.Instance != null`.
   - Keep SFX (`sfxSource`, victory/defeat fanfares, dice rolls) 100% functional.
   - Add two-way handshake: `MusicManager.Awake()` calls `AudioManager.Instance?.StopBGM()` if active.
3. Verify compilation and report results.

## Output
Write your implementation details and verification results to `D:\Unity\3D DnD selainpeli\.agents\teamwork\worker_m1_1\handoff.md`.
Then send a completion message to the orchestrator.

## 2026-09-24T19:06:18Z
You are the implementation worker for Milestone M1 (MusicManager Core & AudioManager Coexistence).
Your working directory is D:\Unity\3D DnD selainpeli\.agents\teamwork\worker_m1_1.
Read your dispatch file at D:\Unity\3D DnD selainpeli\.agents\teamwork\worker_m1_1\DISPATCH.md.
MANDATORY INTEGRITY WARNING: DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.
Implement Assets/Scripts/Core/MusicManager.cs and update Assets/Scripts/Core/AudioManager.cs per the explorer blueprints.
Verify your changes, compile, document everything in handoff.md, and send a message back.
