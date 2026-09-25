# Task Assignment: Milestone M1 Exploration (Track Mapping & Public API)

## Identity
- Archetype: teamwork_preview_explorer
- Working Directory: D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_m1_2
- Parent: Orchestrator (ca3fb1df-84ce-4aaa-ac7d-83cdd973b003)

## Objective
Analyze Milestone M1 track mapping and public API for `MusicManager.cs`:
1. Read `D:\Unity\3D DnD selainpeli\.agents\teamwork\ORIGINAL_REQUEST.md` and `D:\Unity\3D DnD selainpeli\PROJECT.md`.
2. Review the 7 audio tracks:
   - `VillageSong.mp3`
   - `Castle_adventure_song.mp3`
   - `Cellar_combat_music.mp3`
   - `CursedCommander_Combat_music.mp3`
   - `Malakor_combat_music.mp3`
   - `1_Combat_GargoyleKing_music.mp3`
   - `2_Combat_GargoyleKing_music.mp3`
3. Define the Track enum/type mapping, serialized fields, and public API:
   - `PlayMusic(AudioClip clip, float fadeDuration = 1.2f)`
   - `PlayTrack(MusicTrackType track, float fadeDuration = 1.2f)`
   - `PlayCombatMusicForBoss(string bossIdentifier, string roomLocation = "")`
   - `RestoreExplorationMusic(float delay = 1.5f)`
   - `StopMusic(float fadeDuration = 1.2f)`
4. Design how currently playing track is tracked and how exploration music is remembered when combat interrupts it.

## Output
Write your findings to `D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_m1_2\analysis.md` and handoff report to `D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_m1_2\handoff.md`.
Then send a message to orchestrator.
