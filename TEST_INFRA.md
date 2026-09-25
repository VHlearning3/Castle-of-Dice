# Castle of Dice — E2E Test Infrastructure & Test Suite Specification

**Version**: 1.0.0  
**Target Platform**: Unity 6 (6000.3.14f1) / WebGL & Desktop  
**Integrity Mode**: Development / Regression Protection  
**Test Writer**: `teamwork_preview_test_writer` (`test_writer_e2e_1`)  
**Specification Reference**: `PROJECT.md`, `ORIGINAL_REQUEST.md`, `Castle of dice.txt`

---

## 1. Test Philosophy: Opaque-Box & Requirement-Driven

The Castle of Dice E2E Test Suite evaluates the game's audio engine, dynamic state transitions, boss encounters, and UI systems from an **opaque-box, requirement-driven** perspective:

1. **Behavioral Contract Testing**:
   Tests interact with systems exclusively through their documented public interfaces, event broadcasts, and observable runtime side-effects (e.g., active `AudioClip`, volume levels on dual `AudioSource` channels, `CurrentLocation` in `GameManager`, modal visibility in `DefeatUIController`, text content in `MainMenuController`).
2. **Deterministic Expected Outputs**:
   Every expected output is derived directly from the canonical requirements defined in `PROJECT.md` and `ORIGINAL_REQUEST.md` (e.g., 1.2s crossfade duration, equal-power $\sin/\cos$ power curve preserving $0\text{ dB}$, 1.5s victory fanfare delay, exact MP3 filenames in `Assets/Music/`, `Forest = 4` enum value).
3. **Progressive Testability & Decoupled Execution**:
   The test harness operates smoothly across all development phases:
   - When implementation classes (`MusicManager`, `GameLocation.Forest`) are compiled in `Assembly-CSharp`, tests execute against the live runtime components.
   - When implementation is in progress, the test adapter safely verifies asset existence, interface contract preconditions, and structural conformance without halting compilation or throwing unhandled exceptions.
4. **Zero Flakiness & Test Isolation**:
   Each test fixture sets up its own isolated environment, creates or resets temporary GameObjects/AudioSources, executes synchronously or through controlled frame stepping, and cleans up completely in `TearDown` without leaving orphan GameObjects in scenes.

---

## 2. Feature Inventory Mapping

| Feature ID | Feature Name | Description | Test Tier | Test Case Count | Test Class |
|---|---|---|---|---|---|
| **F1.1** | Dual-Channel Crossfade MusicManager | WebGL-compatible persistent manager with 2 AudioSources and 1.2s equal-power crossfading | Tier 1 | 5 | `Tier1_F1_MusicManagerCoreTests` |
| **F1.2** | 7 MP3 Tracks Audio Mapping | Direct mapping & playback for Village, Castle Adventure, Cellar, 3 Bosses, & Phase 2 | Tier 1 | 5 | `Tier1_F1_MusicManagerCoreTests` |
| **F1.3** | AudioManager BGM Delegation | Prevent audio collisions; `AudioManager` yields BGM control when `MusicManager.Instance != null` | Tier 1 | 5 | `Tier1_F1_MusicManagerCoreTests` |
| **F2.1** | GameLocation.Forest Extension | Add `Forest = 4` enum value to `GameLocation` preserving YAML serialization | Tier 1 | 5 | `Tier1_F2_StateAudioIntegrationTests` |
| **F2.2** | DoorTeleporter Location Sync | Update `GameManager.Instance.SetLocation` on door teleportation to trigger location events | Tier 1 | 5 | `Tier1_F2_StateAudioIntegrationTests` |
| **F2.3** | Dynamic Exploration Music | Crossfade to `VillageSong` on Village, `Castle_adventure_song` on Forest/Courtyard/Library/CrownHall | Tier 1 | 5 | `Tier1_F2_StateAudioIntegrationTests` |
| **F2.4** | Boss Encounter Combat Tracks | Map CursedCommander, Malakor, GargoyleKing, and Cellar combat tracks on combat trigger | Tier 1 | 5 | `Tier1_F2_StateAudioIntegrationTests` |
| **F2.5** | Gargoyle King Phase 2 Shift | Immediate crossfade to `2_Combat_GargoyleKing_music` on `OnStoneFormActivated` (HP $\le$ 50%) | Tier 1 | 5 | `Tier1_F2_StateAudioIntegrationTests` |
| **F2.6** | Combat End Restoration | Wait ~1.5s after `OnCombatEnded` for fanfares, then restore exploration music | Tier 1 | 5 | `Tier1_F2_StateAudioIntegrationTests` |
| **F3.1** | Scottish Harp Attribution UI | Display Pixabay Scottish Harp credit and URL in Main Menu UI per `Castle of dice.txt` | Tier 1 | 3 | `Tier1_F3_AttributionAndUITests` |
| **F3.2** | UI Systems Validation | Verify `AbilityTooltipUI`, `DefeatUIController`, `MainMenuController` operate safely | Tier 1 | 3 | `Tier1_F3_AttributionAndUITests` |
| **F4.1** | BuildVillageEditor Music Setup | Automatically instantiate `MusicManager` under `Managers` with 7 clips | Tier 1 | 2 | `Tier1_F4_EditorToolingTests` |
| **F4.2** | Control Window Integration | Add MusicManager status and setup button to `CastleOfDiceControlWindow` | Tier 1 | 1 | `Tier1_F4_EditorToolingTests` |
| **F4.3** | GUID & .meta Preservation | Ensure 0 changes or corruptions to existing `.meta` files and asset GUIDs | Tier 1 | 2 | `Tier1_F4_EditorToolingTests` |
| **Boundary** | Edge Cases & Stress Conditions | Zero/negative fade duration, rapid switching, null clips, WebGL autoplay unlock simulation | Tier 2 | 10 | `Tier2_BoundaryTests` |
| **Pairwise** | Cross-Feature Combinations | Multi-zone transitions, boss combat -> defeat -> retry flows, phase shifts | Tier 3 | 5 | `Tier3_CrossFeatureTests` |
| **E2E Run** | Real-World Application Scenarios | Full dungeon campaign run from Village to Wing 3 boss; WebGL browser lifecycle | Tier 4 | 2 | `Tier4_RealWorldScenarioTests` |
| **Hardening**| Adversarial Hardening | Concurrent event bombardment, extreme inputs, audio memory leak verification | Tier 5 | 4 | `Tier5_AdversarialTests` |
| **Total** | | **Comprehensive Test Suite** | **Tiers 1–5** | **76** | |

---

## 3. Test Architecture & Runner Infrastructure

### 3.1 Directory Layout
```
Assets/
  Tests/
    E2E/
      Common/
        E2EAudioAssert.cs               # Lightweight assert utility with rich failure diagnostics
        MusicManagerTestDriver.cs       # Decoupled reflection/runtime bridge to MusicManager
        E2ETestCaseAttribute.cs         # Test metadata attribute (Tier, Feature, Description)
        E2ETestSuite.cs                 # Base suite class providing setup/teardown and runner hooks
      Tier1_FeatureCoverage/
        Tier1_F1_MusicManagerCoreTests.cs
        Tier1_F2_StateAudioIntegrationTests.cs
        Tier1_F3_AttributionAndUITests.cs
        Tier1_F4_EditorToolingTests.cs
      Tier2_BoundaryCornerCases/
        Tier2_BoundaryTests.cs
      Tier3_CrossFeatureCombinations/
        Tier3_CrossFeatureTests.cs
      Tier4_RealWorldScenarios/
        Tier4_RealWorldScenarioTests.cs
      Tier5_AdversarialHardening/
        Tier5_AdversarialTests.cs
      Editor/
        E2ETestRunner.cs                # Editor MenuItem runner, batchmode method, file trigger watcher
        E2ETestReportWindow.cs          # Interactive Editor Window displaying test results & pass/fail UI
```

### 3.2 Executable Runner Commands

#### Option A: One-Click Execution inside Unity Editor
- Click Unity top menu: `CastleOfDice` $\rightarrow$ `Tests` $\rightarrow$ `Run All E2E Tests (Tiers 1-5)`
- Or open interactive dashboard: `CastleOfDice` $\rightarrow$ `Tests` $\rightarrow$ `Open E2E Test Dashboard`

#### Option B: Terminal / PowerShell Script (Seamless when Editor is Open)
```powershell
powershell -ExecutionPolicy Bypass -File .\Run-E2ETests.ps1
```
*How it works*: If Unity Editor is open, the script touches `Temp/run_tests.trigger`. Unity's `EditorApplication.update` detects the trigger, executes all 76 tests within the running Editor domain, writes structured results to `TestResults_E2E.json` and `TestResults_E2E.txt`, and PowerShell prints colored pass/fail output.

#### Option C: Headless Batchmode (CI / Automation when Editor is Closed)
```powershell
& "D:\6000.3.14f1\Editor\Unity.exe" -projectpath "D:\Unity\3D DnD selainpeli" -batchmode -nographics -quit -executeMethod CastleOfTheD20.Tests.E2E.Editor.E2ETestRunner.RunAllTestsBatchmode -logFile "TestResults_E2E.log"
```

### 3.3 Pass/Fail Semantics & Result Codes
- **Exit Code 0**: 100% of required tests passed. Zero assertion failures. Zero unexpected exceptions.
- **Exit Code 1**: One or more test assertions failed, or an unhandled exception occurred.
- **Exit Code 2**: Compilation or test setup error.

The runner exports machine-readable output to `TestResults_E2E.json`:
```json
{
  "totalTests": 76,
  "passed": 76,
  "failed": 0,
  "skipped": 0,
  "durationMs": 1420.5,
  "results": [ ... ]
}
```

---

## 4. Coverage Thresholds (Tiers 1–4)

| Tier | Focus Area | Minimum Required Tests | Implemented Tests | Pass Criteria |
|---|---|---|---|---|
| **Tier 1** | Primary Feature Logic | $\ge$ 5 per feature | **55** | 100% pass across all 7 MP3 tracks & systems |
| **Tier 2** | Boundaries & Corner Cases | $\ge$ 10 edge cases | **10** | 100% pass under zero-fade, rapid input, null clips |
| **Tier 3** | Cross-Feature Interactions | $\ge$ 5 multi-step flows | **5** | 100% pass for multi-location & boss combat flows |
| **Tier 4** | Real-World Application Scenarios | $\ge$ 2 end-to-end runs | **2** | 100% pass for full campaign dungeon run & WebGL lifecycle |
| **Tier 5** | Adversarial Hardening | $\ge$ 4 stress tests | **4** | 100% pass on GUID integrity, leak checks, event spam |
| **Total** | **All Tiers Combined** | $\ge$ 76 tests | **76** | **Zero failures tolerated** |

---

## 5. Verification & Oracle Derivations

1. **Equal-Power Volume Derivation**:
   For any normalized crossfade time $t \in [0, 1]$:
   - Target incoming volume: $V_{in}(t) = \sin(t \cdot \pi/2) \cdot V_{target}$
   - Target outgoing volume: $V_{out}(t) = \cos(t \cdot \pi/2) \cdot V_{current}$
   - Sum of acoustic powers: $V_{in}^2 + V_{out}^2 = 1.0$ (flat $0\text{ dB}$ across transition).
2. **AudioSource 2D Configuration**:
   - `spatialBlend == 0f` (mandatory 2D stereo).
   - `priority == 0` (highest priority to protect from WebGL voice stealing).
   - `loop == true`.
3. **Event Subscriptions & Propagation**:
   - `GameManager.OnLocationChanged` triggers appropriate BGM track crossfade.
   - `DungeonRoomController.OnRoomCombatStarted` triggers boss/cellar track.
   - `GargoyleKingBoss.OnStoneFormActivated` triggers Phase 2 track (`2_Combat_GargoyleKing_music.mp3`).
   - `TurnManager.OnCombatEnded` triggers 1.5s delayed restoration of exploration BGM.
