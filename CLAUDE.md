# Police Pursuit (Unity 6 URP, Android)

- If `SESSION_HANDOVER.md` exists, read it first (current queue, uncommitted work, next steps). Then `docs/PROGRESS.md`. Tasks are in `docs/FEATURE-TICKETS.md` (open world: OW list).
- Code is in `unity/Assets`. Open only files needed for the current ticket.
- Don't search `unity/Library`, `Temp`, `Logs`, `Obj`, `Build` or `Builds`.
- No subagents unless asked.
- Keep output short. Don't re-check what already passed.
- Test on a scratch copy. Never upgrade the project's Unity version.
- No commit or push unless asked.
- Scratch tests: Unity `6000.6.10f1` in batch mode on a copy of `unity/Assets`, `Packages`, `ProjectSettings`, with repo `shared/` copied next to it. Delete old result XML first; read the log for `error CS` before the XML. Never kill all `Unity.exe` (the user may have an editor open).
- Open world lives in `Scripts/Gameplay/OpenWorld.cs` and friends, built in code; keep it separate from the endless run.
- Commit only a step whose tests passed; say when steps were tested together rather than one by one.
- Never commit the Go source copy in the repo root or `art/generated/`. Never put API keys in chat or files.
- Images: `tools/comfy_image.py` (start `C:\ComfyUI\run_cpu.bat` first; close Unity) or `tools/gen_image.py` (needs `OPENAI_API_KEY`).
