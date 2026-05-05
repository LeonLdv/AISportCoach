---
name: commit-message
description: Analyze staged changes and create a descriptive commit message automatically
allowed-tools: Bash Read Grep Glob
---

# Smart Git Commit Message Skill

Analyzes your staged changes and automatically generates a descriptive commit message following the project's conventions.

## Usage

1. Stage your changes in Rider (check files in commit panel) or via `git add <files>`
2. Run: `/commit-message`
3. The skill analyzes changes and creates a commit with an auto-generated message

## How It Works

You are a git commit message generator. Follow this workflow:

### Step 1: Verify Staged Changes

Check if there are any staged changes:

```bash
git diff --staged --quiet
```

- If nothing is staged, inform the user and stop
- If changes exist, proceed to analysis

### Step 2: Gather Change Information

Collect comprehensive information about the staged changes:

```bash
# Get file change summary
git diff --staged --name-status

# Get detailed diff
git diff --staged --stat

# Get recent commit messages for style reference
git log --oneline -5
```

### Step 3: Analyze Changes

Examine the staged changes and identify:

**File categories:**
- **Domain entities** (`.cs` in `src/AISportCoach.Domain/Entities/`) - Core business objects
- **Application layer** (`.cs` in `src/AISportCoach.Application/`) - Use cases, handlers, interfaces
- **Infrastructure** (`.cs` in `src/AISportCoach.Infrastructure/`) - Repositories, services, external integrations
- **API layer** (`.cs` in `src/AISportCoach.API/`) - Controllers, DTOs, middleware
- **Migrations** (`.cs` in `Migrations/`) - Database schema changes
- **Tests** (`.cs` in `tests/`) - Test files
- **Configuration** (`appsettings.json`, `.csproj`, `CLAUDE.md`) - Project configuration
- **Documentation** (`.md` files)

**Change types:**
- **A** (Added) - New files
- **M** (Modified) - Changed files
- **D** (Deleted) - Removed files

**Patterns to detect:**
- New entity + repository + configuration = "Add [Entity] entity, repository, and configuration"
- Multiple test files = "Add [feature] tests"
- Migration file = Mention the migration purpose
- Controller + DTOs = "Add [feature] endpoints"
- Multiple layers touched = List affected layers

### Step 4: Generate Commit Message

Follow these strict conventions from the project's git history:

**Message structure:**
- Start with present-tense verb: `Add`, `Update`, `Fix`, `Enhance`, `Remove`, `Refactor`
- Be concise but descriptive (1-2 sentences max)
- Mention key components or features affected
- Use Oxford comma for lists
- NO bullet points in commit messages - write as prose

**Verb selection rules:**
- **Add** - New features, entities, files, or capabilities (wholly new)
- **Update** - Enhancements to existing features, dependency updates
- **Fix** - Bug fixes
- **Enhance** - Improvements to existing functionality
- **Remove** - Deleted code or features
- **Refactor** - Code restructuring without behavior change

**Examples from this project:**
```
Add JWT authentication, subscription tiers, WebAuthn support, user profile management, and token services to application and infrastructure layers

Update project dependencies to latest versions and add references for Identity and JWT authentication support

Add comprehensive authentication functional tests and update video FK to ApplicationUser

Enhance ExceptionHandlingMiddleware to handle authentication and subscription-related exceptions
```

**Message length:**
- Aim for under 100 characters when possible
- Can go longer if needed to be accurate
- Never sacrifice clarity for brevity

### Step 5: Create the Commit

Execute the commit with the generated message:

```bash
git commit -m "$(cat <<'EOF'
<generated message>
EOF
)"
```

**IMPORTANT:** Do NOT include any "Co-Authored-By" attribution in the commit message.

### Step 6: Confirm Success

After creating the commit, show:
- ✓ Success indicator
- The commit message used
- Commit hash (short form)
- Files changed count
- Lines added/removed

Example output:
```
✓ Commit created successfully

Message: "Add authentication functional tests and update video FK to ApplicationUser"
Commit: 9c76bde
Files: 19 changed (+1027/-39)
```

## Edge Cases

- **No staged changes:** "Nothing staged. Use `git add <files>` or check files in Rider's commit panel."
- **Only whitespace changes:** Still commit, but mention "formatting" in message
- **Very large changeset:** Focus on the main theme, don't list every file
- **Mixed concerns:** List the top 2-3 themes, e.g., "Add auth tests, update migrations, and fix FK references"

## Commit Message Patterns

- **Add** - New features, files, or capabilities
- **Update** - Enhancements to existing features
- **Fix** - Bug fixes
- **Enhance** - Improvements to existing functionality
- **Remove** - Deleted code or features
- **Refactor** - Code restructuring without behavior change

## Notes

- This skill commits **staged changes only** (what you've checked in Rider or `git add`ed)
- Messages are auto-generated based on actual changes
- You can always amend with `git commit --amend` if needed
- The skill respects your selection in Rider's commit panel exactly