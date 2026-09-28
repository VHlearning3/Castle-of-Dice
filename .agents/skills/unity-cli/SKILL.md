---
name: unity-cli
description: Comprehensive operational guide and reference for unity-cli.exe in Castle of Dice. Use whenever inspecting Unity scene hierarchies, finding components, querying GameObject values, running automated playmode tests, reading console logs, or driving Unity Editor automation over TCP port 6400.
---

# Unity CLI Tool Guide & Reference

The project root contains `unity-cli.exe`, a Rust-based CLI interfacing with the running Unity Editor over TCP (port 6400).

---

## 1. Critical Rules & Windows PowerShell Invariants

> [!CAUTION]
> **Never pass inline JSON strings via `--json` in Windows PowerShell.**
> PowerShell strips and misinterprets quotes, resulting in `unexpected argument` or `Failed to parse JSON parameters`.

### Safe Execution Pattern (Always use `--params-file`):
```powershell
$paramPath = "scratch/cli_params.json"
Set-Content -Path $paramPath -Value '{"componentType": "PlayerProgressionManager"}'
.\unity-cli.exe tool call find_by_component --params-file $paramPath
```

---

## 2. Essential Automation Tools

### Inspecting Scene & Components

| Task | Tool Name | Parameters File Example |
|---|---|---|
| Find Object by Component | `find_by_component` | `{"componentType": "QuestHUDUIController"}` |
| Get Hierarchy Tree | `get_hierarchy` | `{}` or `{"root": "/Canvas"}` |
| Inspect GameObject Details | `get_gameobject_details` | `{"target": "/Canvas/PlayerHUD"}` |
| Read Component Values | `get_component_values` | `{"target": "/PlayerProgressionManager", "componentType": "PlayerProgressionManager"}` |

### Console & Compilation

| Task | Tool Name | Parameters File Example |
|---|---|---|
| Read Console Logs | `read_console` | `{}` |
| Clear Console | `clear_console` | `{}` |
| Refresh Assets & Recompile | `refresh_assets` | `{}` |
| Check Compilation State | `get_compilation_state` | `{}` |

### PlayMode & Unit Testing

| Task | Tool Name | Parameters File Example |
|---|---|---|
| Run PlayMode/EditMode Tests | `run_tests` | `{"testPlatform": "PlayMode"}` |
| Get Test Results | `get_test_status` | `{}` |
| Enter Play Mode | `play_game` | `{}` |
| Exit Play Mode | `stop_game` | `{}` |
| Get Editor State | `get_editor_state` | `{}` |

---

## 3. Asynchronous Tasks & Timeout Guidance

- Fast commands (e.g. `find_by_component`, `get_editor_state`) usually finish within 500–1500 ms.
- Heavy operations (`run_tests`, `refresh_assets`, large hierarchies) may transition to background tasks.
- If a task runs in the background, use `manage_task` with action `status` to inspect completion; do not spam in a tight loop.
