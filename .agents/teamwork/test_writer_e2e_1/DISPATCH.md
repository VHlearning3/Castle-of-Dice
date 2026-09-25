# Task Assignment: E2E Testing Track — Test Suite Creation

## Identity
- Archetype: teamwork_preview_test_writer
- Working Directory: D:\Unity\3D DnD selainpeli\.agents\teamwork\test_writer_e2e_1
- Parent: Orchestrator (ca3fb1df-84ce-4aaa-ac7d-83cdd973b003)

## Objective
Design and implement the complete opaque-box E2E test suite for Castle of Dice audio systems and dynamic game events:
1. Read `D:\Unity\3D DnD selainpeli\.agents\teamwork\ORIGINAL_REQUEST.md` and `D:\Unity\3D DnD selainpeli\PROJECT.md`.
2. Create `D:\Unity\3D DnD selainpeli\TEST_INFRA.md` following the required template:
   - Test philosophy (opaque-box, requirement-driven)
   - Feature inventory mapping
   - Test architecture, runner command, and pass/fail semantics
   - Coverage thresholds (Tiers 1-4)
3. Implement test cases in `Assets/Tests/E2E/` using Unity standard test framework or C# runner:
   - Tier 1: Feature Coverage (>=5 test cases per feature for all 7 music tracks & core systems)
   - Tier 2: Boundary & Corner Cases (zero fade duration, rapid switching, missing clip fallback, concurrent calls, WebGL audio context unlock simulation)
   - Tier 3: Cross-Feature Combinations (pairwise interactions: Village -> Cellar -> Victory; Village -> Forest -> Courtyard -> CursedCommander -> Defeat -> Retry; Gargoyle King Phase 1 -> Phase 2 -> Victory)
   - Tier 4: Real-World Application Scenarios (full dungeon run from Village to Wing 3 boss)
4. Create an executable test runner (e.g. standalone test runner script or editor test runner) that can execute all E2E tests and output clear pass/fail results.
5. Once the test suite and runner are verified and ready, publish `D:\Unity\3D DnD selainpeli\TEST_READY.md` containing the runner command and coverage summary.

## Output
Write your analysis and test structure to `D:\Unity\3D DnD selainpeli\.agents\teamwork\test_writer_e2e_1\handoff.md`.
When `TEST_READY.md` is published, send a completion message to the orchestrator.

## 2026-09-24T19:02:55Z
You are an E2E Test Writer for Castle of Dice.
Your working directory is D:\Unity\3D DnD selainpeli\.agents\teamwork\test_writer_e2e_1.
Read D:\Unity\3D DnD selainpeli\.agents\teamwork\test_writer_e2e_1\DISPATCH.md, D:\Unity\3D DnD selainpeli\.agents\teamwork\ORIGINAL_REQUEST.md, and D:\Unity\3D DnD selainpeli\PROJECT.md.
Create D:\Unity\3D DnD selainpeli\TEST_INFRA.md following the required methodology (Tiers 1-4).
Implement comprehensive E2E tests and test runner scripts in Assets/Tests/E2E/.
When complete and verified, publish D:\Unity\3D DnD selainpeli\TEST_READY.md and write your handoff report to D:\Unity\3D DnD selainpeli\.agents\teamwork\test_writer_e2e_1\handoff.md.
Send a message back when TEST_READY.md is published.

