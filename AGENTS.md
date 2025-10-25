 # Repository Guidelines

 ## Project Structure & Module Organization
 - `RoslynBridge/` – VSIX source (C#). Key folders: `Server/` (HTTP host), `Services/` (Roslyn queries, refactorings), `Models/`, `Constants/`.
 - Build artifacts: `RoslynBridge/bin/` and `RoslynBridge/obj/`.
 - Claude skills: `.claude/skills/roslyn-api/SKILL.md` (HTTP query reference for local discovery/testing).

 ## Build, Test, and Development Commands
 - Build (CLI): `msbuild RoslynBridge.sln /p:Configuration=Debug`
 - Build (VS): Open `RoslynBridge.sln`, set `RoslynBridge` as startup, press F5 to launch the Experimental Instance.
 - Health check (server running in VS):
   - PowerShell: `$b=@{queryType='getprojects'}|ConvertTo-Json; Invoke-RestMethod -Uri 'http://localhost:59123/query' -Method Post -Body $b -ContentType 'application/json'`
   - curl (Windows): `curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getprojects\"}"`

 ## Coding Style & Naming Conventions
 - Language: C# (net48 VSIX). Use 4‑space indentation; braces on new lines.
 - Naming: `PascalCase` for types/methods; `camelCase` for locals; private fields as `_camelCase`.
 - Prefer `async`/`await`, avoid blocking the UI thread; use `JoinableTaskFactory` when switching.
 - Keep nullable annotations consistent with project settings.
 - Run Format Document and Organize Usings before commits.

 ## Testing Guidelines
 - No unit test project yet. Validate via HTTP endpoints (see SKILL.md): `getprojects`, `getdiagnostics`, `getsolutionoverview`, `getsymbol`, etc.
 - Expected response shape: `{ success, message, data, error }` (JSON).
 - Lines are 1‑based; columns 0‑based. File paths in JSON require escaped backslashes.

 ## Commit & Pull Request Guidelines
 - Commits: concise, imperative subject (e.g., "Add diagnostics endpoint"), with short body explaining rationale and scope.
 - PRs: include description, linked issues, sample requests/responses, and screenshots when UI/VS behavior is affected.
 - Checklist: builds clean, `getdiagnostics` shows no new errors, code formatted, usings organized.

 ## Security & Configuration Tips
 - Server binds to `http://localhost:59123/` and accepts only POST; CORS is permissive for local tooling. Do not expose externally.
 - Endpoints: `/query` (main), `/health` route exists but still requires POST.
 - Adjust port/paths in `RoslynBridge/Constants/ServerConstants.cs` if needed.
