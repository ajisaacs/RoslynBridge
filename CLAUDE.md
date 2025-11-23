# Claude Code Development Notes

## Working with the Roslyn Bridge Skill

### Important: Skill File Modifications

**ALWAYS modify the skill files in the `.claude` project-level directory, NOT in the user-level skill directory.**

The canonical source of truth for the Roslyn Bridge skill is located at:
```
.claude/skills/roslyn-bridge/
├── SKILL.md          # Main skill documentation (MODIFY THIS)
├── scripts/
│   └── rb            # Shell helper script (MODIFY THIS)
```

After making changes to these files, use the sync script to update:
- Your personal project-level skill at `~/.claude/skills/roslyn-bridge/`
- The project-level scripts directory at `scripts/rb`

### Skill Synchronization

To sync changes from the project's `.claude/skills/roslyn-bridge/` to user-level and project scripts:

```powershell
# Run the sync script (with interactive prompts and diff viewing)
.\scripts\sync-skill.ps1

# Or run with -Force to overwrite without prompting
.\scripts\sync-skill.ps1 -Force

# Or run with -DryRun to preview changes without modifying files
.\scripts\sync-skill.ps1 -DryRun
```

This will:
1. Compare and sync `.claude/skills/roslyn-bridge/` → `~/.claude/skills/roslyn-bridge/`
2. Compare and sync `.claude/skills/roslyn-bridge/scripts/rb` → `scripts/rb`
3. Show diffs and prompt for confirmation (unless -Force is used)
4. Create destination directories if they don't exist

### Why This Workflow?

1. **Version Control**: The `.claude` folder is committed to git, allowing team members to get the latest skill automatically
2. **Single Source of Truth**: Prevents confusion about which files to modify
3. **User-Level Sync**: Developers can use the skill from any project after syncing to `~/.claude/skills/`
4. **Project Scripts**: The sync also ensures `scripts/rb` matches the skill version

### Claude Code Skills Documentation

Skills in `.claude/skills/` sync automatically via git. When team members pull the latest changes, they automatically get updated skills.

For personal use across all projects, run the sync script to copy to `~/.claude/skills/roslyn-bridge/`.

## Skill Structure

```
.claude/skills/roslyn-bridge/
├── SKILL.md          # Main skill prompt and documentation
└── scripts/
    └── rb            # Shell script wrapper for WebAPI
```

The `SKILL.md` file contains:
- Skill metadata (name, description)
- Usage instructions for Claude
- API endpoint documentation
- Workflow patterns and examples
- Troubleshooting guidance

The `scripts/rb` helper provides a convenient shell script interface to the Roslyn Bridge WebAPI.
