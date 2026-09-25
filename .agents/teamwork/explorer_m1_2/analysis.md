# Milestone M1 Analysis: Track Mapping, Track Enum, Serialized Fields & Public API for MusicManager.cs

## Executive Summary
This analysis details the audio architecture and public API contract for `MusicManager.cs` to satisfy Milestone M1 requirements in *Castle of the D20 / Castle of Dice*. It establishes the 1:1 mapping between all 7 existing MP3 tracks in `Assets/Music/` and the discrete `MusicTrackType` enum, details the serialized fields for inspector assignment and editor automation, defines the complete public API contract, and designs the state-tracking mechanism for exploration music preservation during combat interruptions.

---

## 1. Inventory of Audio Tracks & Direct Game Mapping

All 7 audio tracks exist in `Assets/Music/` with verified `.meta` files and asset GUIDs:

| # | Filename | Asset GUID | File Size | MusicTrackType Enum Member | Game Context & State Trigger |
|---|----------|------------|-----------|----------------------------|------------------------------|
| 1 | `VillageSong.mp3` | `8c695f2c8079fff438568e6ed65231a4` | 2,177,567 B | `MusicTrackType.Village` | Starting haven (Oakhaven / Kivenkolo Village). Fired on game start and `GameManager.OnLocationChanged(GameLocation.Village)`. |
| 2 | `Castle_adventure_song.mp3` | `9fbf8d6dbe585eb41846c4f923b7ef84` | 2,863,535 B | `MusicTrackType.CastleAdventure` | Free exploration outside combat across Castle zones: `Forest`, `Courtyard`, `Library`, and `CrownHall`. Fired on `GameManager.OnLocationChanged`. |
| 3 | `Cellar_combat_music.mp3` | `2d54406a66699564c8d0eafe28646b97` | 2,885,423 B | `MusicTrackType.CellarCombat` | Tavern Cellar hostile encounter (Vermin/Rats). Fired on `DungeonRoomController.OnRoomCombatStarted` with `roomLocation == "Cellar"`. |
| 4 | `CursedCommander_Combat_music.mp3` | `64736f9872e4eb245be5d34208a0d48a` | 2,582,063 B | `MusicTrackType.CursedCommanderCombat` | Wing 1 Boss battle (Cursed Commander / Kirottu Komentaja). Fired on `DungeonRoomController.OnRoomCombatStarted` with `bossIdentifier == "CursedCommander"`. |
| 5 | `Malakor_combat_music.mp3` | `a276eb9eef11c9748b6f72cfae8bfb51` | 2,769,455 B | `MusicTrackType.MalakorCombat` | Wing 2 Boss battle (Shadow Mage Malakor / Varjomaagi Malakor). Fired on `DungeonRoomController.OnRoomCombatStarted` with `bossIdentifier == "ShadowMageMalakor"`. |
| 6 | `1_Combat_GargoyleKing_music.mp3` | `7bf5b50875e533e4b958c21bbddca4ad` | 2,889,263 B | `MusicTrackType.GargoyleKingPhase1` | Wing 3 Final Boss Phase 1 battle (The Gargoyle King / Kivettymiskuningas). Fired on `DungeonRoomController.OnRoomCombatStarted` with `bossIdentifier == "GargoyleKing"`. |
| 7 | `2_Combat_GargoyleKing_music.mp3` | `496ee8670bc23514a8072d733cb535c5` | 2,885,423 B | `MusicTrackType.GargoyleKingPhase2` | Wing 3 Final Boss Phase 2 Stone Form (enchanted granite skin). Fired dynamically on `GargoyleKingBoss.OnStoneFormActivated` when HP $\le 50\%$. |

---

## 2. Enum Design: `MusicTrackType`

The enum is placed in the `CastleOfTheD20.Core` namespace:

```csharp
namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Discrete music track identifiers for Castle of the D20.
    /// Maps 1:1 to the 7 MP3 assets in Assets/Music/.
    /// </summary>
    public enum MusicTrackType
    {
        None = 0,
        Village = 1,                 // Assets/Music/VillageSong.mp3
        CastleAdventure = 2,         // Assets/Music/Castle_adventure_song.mp3
        CellarCombat = 3,            // Assets/Music/Cellar_combat_music.mp3
        CursedCommanderCombat = 4,   // Assets/Music/CursedCommander_Combat_music.mp3
        MalakorCombat = 5,           // Assets/Music/Malakor_combat_music.mp3
        GargoyleKingPhase1 = 6,      // Assets/Music/1_Combat_GargoyleKing_music.mp3
        GargoyleKingPhase2 = 7       // Assets/Music/2_Combat_GargoyleKing_music.mp3
    }
}
```

### Design Rationale:
1. `None = 0` provides a clean default/silence state.
2. Explicit integer values ensure deterministic serialization and debugger readability.
3. Separation between exploration tracks (`Village`, `CastleAdventure`) and combat tracks (`CellarCombat` ... `GargoyleKingPhase2`) allows simple classification helpers:
   - `IsExplorationTrack(MusicTrackType track)`: returns `track == MusicTrackType.Village || track == MusicTrackType.CastleAdventure`.
   - `IsCombatTrack(MusicTrackType track)`: returns `track >= MusicTrackType.CellarCombat && track <= MusicTrackType.GargoyleKingPhase2`.

---

## 3. Serialized Fields & Inspector Configuration

`MusicManager.cs` exposes serialized fields for all 7 clips, volume controls, and the dual AudioSources:

```csharp
#region Serialized Fields - Audio Clips

[Header("Audio Tracks (Assets/Music/)")]
[Tooltip("Oakhaven / Kivenkolo Village theme (VillageSong.mp3)")]
[SerializeField] private AudioClip villageSongClip;

[Tooltip("Castle and Forest exploration theme (Castle_adventure_song.mp3)")]
[SerializeField] private AudioClip castleAdventureSongClip;

[Tooltip("Tavern cellar combat encounter (Cellar_combat_music.mp3)")]
[SerializeField] private AudioClip cellarCombatClip;

[Tooltip("Wing 1 Boss combat theme (CursedCommander_Combat_music.mp3)")]
[SerializeField] private AudioClip cursedCommanderCombatClip;

[Tooltip("Wing 2 Boss combat theme (Malakor_combat_music.mp3)")]
[SerializeField] private AudioClip malakorCombatClip;

[Tooltip("Wing 3 Final Boss Phase 1 combat theme (1_Combat_GargoyleKing_music.mp3)")]
[SerializeField] private AudioClip gargoyleKingPhase1Clip;

[Tooltip("Wing 3 Final Boss Phase 2 Stone Form combat theme (2_Combat_GargoyleKing_music.mp3)")]
[SerializeField] private AudioClip gargoyleKingPhase2Clip;

#endregion

#region Serialized Fields - Volume & Settings

[Header("Volume & Fading")]
[Range(0f, 1f)] [SerializeField] private float masterVolume = 0.8f;
[Range(0f, 1f)] [SerializeField] private float musicVolume = 0.7f;
[SerializeField] private float defaultFadeDuration = 1.2f;

[Header("Audio Sources (Dual-Channel Crossfader)")]
[SerializeField] private AudioSource audioSourceA;
[SerializeField] private AudioSource audioSourceB;

#endregion
```

### Programmatic / Automated Editor Assignment Support
To support automated setup in `BuildVillageEditor.cs` and E2E test harnesses without reflection:
```csharp
public void AssignClips(
    AudioClip village,
    AudioClip castleAdventure,
    AudioClip cellarCombat,
    AudioClip cursedCommander,
    AudioClip malakor,
    AudioClip gargoylePhase1,
    AudioClip gargoylePhase2)
{
    villageSongClip = village;
    castleAdventureSongClip = castleAdventure;
    cellarCombatClip = cellarCombat;
    cursedCommanderCombatClip = cursedCommander;
    malakorCombatClip = malakor;
    gargoyleKingPhase1Clip = gargoylePhase1;
    gargoyleKingPhase2Clip = gargoylePhase2;
}
```

Also individual read-only properties for test verification:
```csharp
public AudioClip VillageSongClip => villageSongClip;
public AudioClip CastleAdventureSongClip => castleAdventureSongClip;
public AudioClip CellarCombatClip => cellarCombatClip;
public AudioClip CursedCommanderCombatClip => cursedCommanderCombatClip;
public AudioClip MalakorCombatClip => malakorCombatClip;
public AudioClip GargoyleKingPhase1Clip => gargoyleKingPhase1Clip;
public AudioClip GargoyleKingPhase2Clip => gargoyleKingPhase2Clip;
```

---

## 4. Public API Specification for `MusicManager.cs`

### Method 1: `PlayTrack(MusicTrackType track, float fadeDuration = 1.2f)`
- **Behavior**:
  - If `track == MusicTrackType.None`: forwards to `StopMusic(fadeDuration)`.
  - Redundancy Check: If `track == currentTrack && IsPlaying && !isStopping`: returns immediately without interrupting playback.
  - Resolves `AudioClip` via `GetClip(track)`.
  - Missing Clip Safety: If resolved `AudioClip == null`, logs warning `[MusicManager] Audio clip for track '{track}' is not assigned.` and returns cleanly without throwing.
  - Exploration State Tracking: If `IsExplorationTrack(track)`, records `currentExplorationTrack = track`.
  - Restoration Cancellation: Cancels any pending `RestoreExplorationMusic` coroutine.
  - Crossfade: Starts dual-channel crossfade coroutine over `fadeDuration` (or executes immediate instant switch if `fadeDuration <= 0f`).
  - Sets `currentTrack = track`.

### Method 2: `PlayMusic(AudioClip clip, float fadeDuration = 1.2f)`
- **Behavior**:
  - Overload for direct `AudioClip` playback.
  - If `clip == null`: forwards to `StopMusic(fadeDuration)`.
  - Redundancy Check: If `clip == CurrentClip && IsPlaying && !isStopping`: returns immediately.
  - Reverse-lookup: Resolves `clip` to `MusicTrackType` via `GetTrackTypeForClip(clip)`.
  - If resolved track is an exploration track, updates `currentExplorationTrack`.
  - Cancels any pending restoration coroutine.
  - Starts crossfade to `clip` over `fadeDuration`.
  - Sets `currentTrack = resolvedTrack`.

### Method 3: `PlayCombatMusicForBoss(string bossIdentifier, string roomLocation = "")`
- **Behavior**:
  - Resolves the combat track using priority matching on `bossIdentifier` and `roomLocation`:
    ```csharp
    public MusicTrackType ResolveCombatTrack(string bossIdentifier, string roomLocation = "")
    {
        string boss = (bossIdentifier ?? string.Empty).Trim();
        string room = (roomLocation ?? string.Empty).Trim();

        // 1. Direct bossIdentifier matching
        if (boss.IndexOf("Commander", StringComparison.OrdinalIgnoreCase) >= 0 ||
            boss.IndexOf("Komentaja", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return MusicTrackType.CursedCommanderCombat;
        }
        if (boss.IndexOf("Malakor", StringComparison.OrdinalIgnoreCase) >= 0 ||
            boss.IndexOf("Varjomaagi", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return MusicTrackType.MalakorCombat;
        }
        if (boss.IndexOf("Gargoyle", StringComparison.OrdinalIgnoreCase) >= 0 ||
            boss.IndexOf("Kivettymiskuningas", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return MusicTrackType.GargoyleKingPhase1;
        }

        // 2. Room location matching
        if (room.IndexOf("Cellar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            room.IndexOf("Kellari", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return MusicTrackType.CellarCombat;
        }
        if (room.IndexOf("Courtyard", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return MusicTrackType.CursedCommanderCombat;
        }
        if (room.IndexOf("Library", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return MusicTrackType.MalakorCombat;
        }
        if (room.IndexOf("CrownHall", StringComparison.OrdinalIgnoreCase) >= 0 ||
            room.IndexOf("ThroneRoom", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return MusicTrackType.GargoyleKingPhase1;
        }

        // 3. Fallback
        return MusicTrackType.CellarCombat;
    }
    ```
  - Pre-combat Exploration Memory: If currently playing an exploration track (`Village` or `CastleAdventure`), persists `currentExplorationTrack = currentTrack`.
  - Cancels any pending exploration restore coroutine.
  - Calls `PlayTrack(targetTrack, defaultFadeDuration)`.

### Method 4: `RestoreExplorationMusic(float delay = 1.5f)`
- **Behavior**:
  - Called upon combat resolution (`TurnManager.OnCombatEnded(bool isVictory)`).
  - Allows the victory or defeat fanfare sound effect (duration ~0.8s to 1.5s in `AudioManager`) to finish cleanly.
  - Cancels any prior restore coroutine.
  - Starts `RestoreExplorationMusicCoroutine(delay)`:
    - Waits for `delay` seconds using `WaitForSeconds(delay)`.
    - Determines destination exploration track:
      - If `GameManager.Instance != null`:
        - If `GameManager.Instance.CurrentLocation == GameLocation.Village`: `MusicTrackType.Village`
        - Else: `MusicTrackType.CastleAdventure` (Forest, Courtyard, Library, CrownHall)
      - Else: falls back to `currentExplorationTrack != MusicTrackType.None ? currentExplorationTrack : MusicTrackType.Village`.
    - Calls `PlayTrack(targetTrack, defaultFadeDuration)`.
  - Cancellation Guarantee: If new combat is started or `PlayCombatMusicForBoss` / `PlayTrack` is called before the delay completes, the coroutine is cancelled so combat music is not overridden.

### Method 5: `StopMusic(float fadeDuration = 1.2f)`
- **Behavior**:
  - Smoothly fades out currently playing audio source to zero volume over `fadeDuration`.
  - If `fadeDuration <= 0f`: stops audio sources immediately.
  - Sets `currentTrack = MusicTrackType.None`.

### Method 6: `GetClip(MusicTrackType trackType)`
- **Behavior**:
  - Deterministic switch expression mapping `MusicTrackType` to the corresponding serialized `AudioClip` field. Returns `null` if unassigned or `None`.

### Method 7: Volume Controls & Properties
- `public MusicTrackType CurrentTrack => currentTrack;`
- `public MusicTrackType CurrentExplorationTrack => currentExplorationTrack;`
- `public AudioClip CurrentClip => activeSource != null ? activeSource.clip : null;`
- `public bool IsPlaying => (activeSource != null && activeSource.isPlaying) || (inactiveSource != null && inactiveSource.isPlaying);`
- `public bool IsCrossFading => isCrossFading;`
- `public float MasterVolume { get; set; }`
- `public float MusicVolume { get; set; }`
- `public void SetVolume(float master, float music)`

---

## 5. State Tracking & Interrupted Exploration Recovery Design

A major challenge in dynamic audio systems is correctly resuming exploration music when combat ends, especially across edge cases (e.g. death in boss room, retry from defeat screen, room transitions during combat end).

### Tracking Mechanics:
1. **`currentTrack`**: Holds the active `MusicTrackType`. Updated whenever a crossfade starts.
2. **`currentExplorationTrack`**: Holds the most recent exploration track (`Village` or `CastleAdventure`).
   - Defaults to `MusicTrackType.Village` on Awake/Start.
   - Updated whenever `PlayTrack` or `PlayMusic` is called with an exploration track.
   - When combat begins, `currentExplorationTrack` remains locked in memory.
3. **Dual Source of Truth for Restoration**:
   When `RestoreExplorationMusic` executes:
   - **Primary Source**: Query `GameManager.Instance.CurrentLocation`. This reflects the physical position of the player (e.g. if the player was teleported back to the village after defeat or remained in the dungeon after victory).
     - `GameLocation.Village` $\rightarrow$ `MusicTrackType.Village`
     - `GameLocation.Courtyard`, `Library`, `CrownHall`, `Forest` $\rightarrow$ `MusicTrackType.CastleAdventure`
   - **Secondary Source**: If `GameManager.Instance == null` (isolated test harness or standalone scene), fallback to `currentExplorationTrack`.
4. **Coroutine Interruption Safety**:
   - `private Coroutine restoreCoroutine;`
   - Any call to `PlayCombatMusicForBoss`, `PlayTrack`, or `StopMusic` executes:
     ```csharp
     if (restoreCoroutine != null)
     {
         StopCoroutine(restoreCoroutine);
         restoreCoroutine = null;
     }
     ```
   - This ensures that if the player retries combat within 1.5 seconds from the Defeat screen (`DefeatUIController.OnRetryClicked`), the delayed exploration restore does not trample the re-engaged boss combat track.

---

## 6. Coexistence with `AudioManager.cs`

In `AudioManager.cs`:
- Currently, `AudioManager` auto-starts `villageBgmClip` or procedural harp in `Start()`.
- To avoid overlapping music tracks:
  1. In `AudioManager.Start()`, check `if (MusicManager.Instance != null) return;` before playing BGM.
  2. In `AudioManager.PlayBGM(AudioClip clip, bool loop = true)`, delegate to `MusicManager.Instance.PlayMusic(clip)` when `MusicManager.Instance != null`.
  3. In `AudioManager.HandleLocationChanged()` and `AudioManager.HandlePlayModeChanged()`, yield BGM control when `MusicManager.Instance != null`.
  4. `AudioManager` continues handling all SFX (`PlaySFX`, `sfxSource`, dice rolls, combat hits, fanfares).

---

## 7. Zero-Error Compilation & Assembly Compatibility

- Uses only standard `UnityEngine`, `System`, `CastleOfTheD20.Core`, `CastleOfTheD20.Combat`, and `CastleOfTheD20.Bosses`.
- Does not import any external packages, plugins, or platform-specific libraries.
- Fully compatible with Unity WebGL compilation targets (`Assembly-CSharp.dll` and `Assembly-CSharp-Editor.dll`).
