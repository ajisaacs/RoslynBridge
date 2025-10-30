# Claude Code Development Notes

## Working with the Roslyn Bridge Skill

### Important: Skill File Modifications

**ALWAYS modify the skill files in the `.claude` project-level directory, NOT in the user-level skill directory.**

The canonical source of truth for the Roslyn Bridge skill is located at:
```
.claude/skills/roslyn-bridge/
├── SKILL.md          # Main skill documentation (MODIFY THIS)
├── scripts/
│   └── rb.ps1        # PowerShell helper script (MODIFY THIS)
```

After making changes to these files, use the sync script to update:
- Your personal project-level skill at `~/.claude/skills/roslyn-bridge/`
- The project-level scripts directory at `scripts/rb.ps1`

### Skill Synchronization

To sync changes from the project's `.claude/skills/roslyn-bridge/` to user-level and project scripts:

```powershell
# Run the sync script
.\scripts\sync-skill.ps1
```

This will:
1. Copy `.claude/skills/roslyn-bridge/` → `~/.claude/skills/roslyn-bridge/`
2. Copy `.claude/skills/roslyn-bridge/scripts/rb.ps1` → `scripts/rb.ps1`

### Why This Workflow?

1. **Version Control**: The `.claude` folder is committed to git, allowing team members to get the latest skill automatically
2. **Single Source of Truth**: Prevents confusion about which files to modify
3. **User-Level Sync**: Developers can use the skill from any project after syncing to `~/.claude/skills/`
4. **Project Scripts**: The sync also ensures `scripts/rb.ps1` matches the skill version

### Claude Code Skills Documentation

Skills in `.claude/skills/` sync automatically via git. When team members pull the latest changes, they automatically get updated skills.

For personal use across all projects, run the sync script to copy to `~/.claude/skills/roslyn-bridge/`.

## Skill Structure

```
.claude/skills/roslyn-bridge/
├── SKILL.md          # Main skill prompt and documentation
└── scripts/
    └── rb.ps1        # PowerShell wrapper for WebAPI
```

The `SKILL.md` file contains:
- Skill metadata (name, description)
- Usage instructions for Claude
- API endpoint documentation
- Workflow patterns and examples
- Troubleshooting guidance

The `scripts/rb.ps1` helper provides a convenient PowerShell interface to the Roslyn Bridge WebAPI.
