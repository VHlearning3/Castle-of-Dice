# Agent Constraints & Execution Rules

## Task Scope & Safety Rules
1. **Scope Limit:** Implement ONLY the specific feature requested. Never expand scope to other systems.
2. **File Limit:** Do not create or modify more than 2–3 files per task without explicit confirmation.
3. **No Autonomous Testing Scripts:** Never generate standalone PowerShell (`.ps1`) or Bash test runners. Rely only on standard compilation checks (`dotnet build`).
4. **Git Operations:** 
   - Never create git worktrees or merge branches automatically.
   - All work must stay inside the active working branch.
   - Never run `git reset --hard` or destructive git commands.
5. **Unity Safety:** Never delete or rename existing `.meta` files. Keep all C# code compatible with Unity WebGL.