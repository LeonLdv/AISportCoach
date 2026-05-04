# Create Custom Claude Skill: Smart Git Commit

## Context

**Problem:** Quick way to commit staged changes without manually writing commit messages each time.

**Solution:** Create a custom Claude Code skill (`/commit`) that:
- Commits all staged changes (files already added via `git add` or Rider's staging area)
- Automatically generates descriptive commit messages by analyzing the diff
- Follows the project's commit message conventions (present tense verbs like "Add", "Update", "Fix", "Enhance")

**Why this is useful:**
- Saves time by auto-generating quality commit messages
- Ensures consistent commit message style across the project
- Leverages Claude's ability to understand code changes and summarize them concisely

---

## Skill Design

### Skill Metadata

**File location:** `.claude/skills/commit/SKILL.md`

**Frontmatter:**
```yaml
---
name: commit
description: Analyze staged changes and create a descriptive commit message automatically
allowed-tools: Bash Read Grep Glob
---
```

**Allowed tools:**
- `Bash` - Execute git commands (diff, commit, status)
- `Read` - Read modified files to understand changes better
- `Grep` - Search for patterns in changed files
- `Glob` - Find related files for context

### Workflow

The skill will follow this process:

1. **Check for staged changes**
   - Run `git diff --staged --stat` to see what's staged
   - If nothing is staged, inform the user and exit

2. **Analyze the changes**
   - Run `git diff --staged` to get full diff
   - Identify what was changed:
     - New files added
     - Existing files modified
     - Files deleted
     - Type of changes (code, config, tests, docs, migrations)

3. **Generate commit message**
   - Follow project conventions from git log:
     - Start with present-tense verb: Add, Update, Fix, Enhance, Remove, Refactor
     - Be concise but descriptive (1-2 sentences max)
     - Mention key components or features affected
   - Examples from this project:
     - "Add JWT authentication, subscription tiers, WebAuthn support, user profile management, and token services to application and infrastructure layers"
     - "Update project dependencies to latest versions and add references for Identity and JWT authentication support"
     - "Fix VideoUpload foreign key to reference ApplicationUser instead of UserProfile"

4. **Create the commit**
   - Use heredoc format for proper message formatting
   - Execute: `git commit -m "$(cat <<'EOF'\n<message>\nEOF\n)"`

5. **Confirm to user**
   - Show the commit message that was used
   - Display commit hash
   - Show number of files changed

---

## Implementation Steps

### Step 1: Create Skill Directory

Create: `.claude/skills/commit/`

### Step 2: Write SKILL.md

**File:** `.claude/skills/commit/SKILL.md`

**Content structure:**
```markdown
---
name: commit
description: Analyze staged changes and create a descriptive commit message automatically
allowed-tools: Bash Read Grep Glob
---

# Smart Git Commit Skill

This skill analyzes your staged changes and automatically generates a descriptive commit message following the project's conventions.

## Usage

1. Stage your changes: `git add <files>` or use Rider's commit panel
2. Run: `/commit`
3. The skill will analyze changes and create a commit with an auto-generated message

## How It Works

[Detailed workflow steps 1-5 as described above]

## Commit Message Patterns

The skill follows these conventions:
- **Add** - New features, files, or capabilities
- **Update** - Enhancements to existing features
- **Fix** - Bug fixes
- **Enhance** - Improvements to existing functionality
- **Remove** - Deleted code or features
- **Refactor** - Code restructuring without behavior change

## Examples

[Show 2-3 example commit messages the skill would generate]

## Notes

- This skill commits **staged changes only** (what you've already `git add`ed)
- Messages are auto-generated; you can always amend with `git commit --amend` if needed
```

### Step 3: Implement the Skill Logic

The skill's main logic section should:

1. **Safety check:**
```bash
# Check if anything is staged
git diff --staged --quiet && echo "Nothing staged" || echo "Changes found"
```

2. **Get change summary:**
```bash
# Get list of changed files
git diff --staged --name-status

# Get detailed diff
git diff --staged
```

3. **Analysis logic:**
- Count files added/modified/deleted
- Identify file types (.cs, .csproj, .md, migrations, configs)
- Determine the nature of changes:
  - New entity/feature if new .cs files in Domain/Application
  - Configuration if appsettings.json changed
  - Migration if new migration file
  - Test if files in tests/ directory
  - Infrastructure if files in Infrastructure/

4. **Message generation rules:**
- If multiple file types: list the main components affected
- If single feature: be specific about what was added/fixed
- Keep it under 100 characters when possible
- Use Oxford comma for lists
- Be specific about technology/component names

5. **Execute commit:**
```bash
git commit -m "$(cat <<'EOF'
<generated message>
EOF
)"
```

---

## Critical Files

| File | Purpose |
|------|---------|
| `.claude/skills/commit/SKILL.md` | The skill definition (NEW) |
| `.git/` | Git repository where commits will be made |

---

## Verification

After creating the skill:

1. **Verify skill is recognized:**
```bash
# The skill should appear in available skills list
# Check by typing `/` in Claude Code
```

2. **Test with simple change:**
```bash
# Make a small change
echo "# Test" >> test.md
git add test.md

# Run the skill
/commit

# Verify commit was created
git log -1 --pretty=format:"%s"
```

3. **Test with complex changes:**
```bash
# Stage multiple files of different types
git add src/SomeFile.cs tests/SomeTest.cs appsettings.json

# Run the skill
/commit

# Verify message quality
git log -1
```

---

## Example Output

When user runs `/commit`:

```
Analyzing staged changes...

Files staged:
  M  src/AISportCoach.API/Program.cs
  M  src/AISportCoach.Infrastructure/Migrations/20260504150844_ChangeVideoUploadUserFKToApplicationUser.cs
  
Detected changes:
- Modified: API configuration
- Modified: Database migration

Generated commit message:
"Uncomment auto-migration code and update VideoUpload FK to reference ApplicationUser"

Creating commit...
[main abc1234] Uncomment auto-migration code and update VideoUpload FK to reference ApplicationUser
 2 files changed, 15 insertions(+), 8 deletions(-)

✓ Commit created successfully
```

---

## Future Enhancements (Optional)

Ideas for v2:
- Add optional argument to override auto-generated message: `/commit "Custom message"`
- Support for multi-line commit messages with body
- Interactive mode that shows suggested message and asks for confirmation
- Integration with conventional commits format (feat:, fix:, etc.)
