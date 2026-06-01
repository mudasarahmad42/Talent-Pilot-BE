# Contributing To Talent Pilot Backend

This repository uses a protected-main workflow. Contributors must work from feature branches and open pull requests into `main`.

## Push Policy

- Contributors must not push automatically after generating or editing code.
- A push is allowed only when Mudasar Ahmad or the current user explicitly asks for it in the active session.
- When a push is requested, push only the contributor's own branch.
- Never push directly to `main`.
- Never push unrelated files, another contributor's work, local build outputs, secrets, or temporary files.
- AI-assisted contributors must summarize what will be pushed before pushing when the branch contains mixed or dirty work.

## Main Branch Protection

- Do not push directly to `main`.
- Only the code owner, Mudasar Ahmad, may make direct changes to `main`.
- All contributors, including AI-assisted contributors, must create their own branch and open a pull request.
- Contributors should not share branches unless the code owner explicitly coordinates that work.
- Pull requests must pass backend validation before merge.
- Pull requests must include a contributor log update under `contributors/<contributor-name>/README.md`.
- Direct pushes to `main` should be blocked in GitHub branch protection settings.
- This repo also includes a local `.githooks/pre-push` guard. Enable it after cloning with `git config core.hooksPath .githooks`.

## Recommended GitHub Branch Protection

For repository admins, configure `main` with:

- Require a pull request before merging.
- Require approvals before merging.
- Dismiss stale approvals when new commits are pushed.
- Require conversation resolution before merging.
- Require status checks before merging.
- Require branch to be up to date before merging.
- Restrict who can push to matching branches to Mudasar Ahmad only.
- Do not allow force pushes.
- Do not allow deletions.
- Include administrators if the team wants the rule to apply even to admins.

Recommended required status checks:

```text
dotnet test
database script runner, when SQL scripts changed
```

Local hook setup:

```powershell
git config core.hooksPath .githooks
```

## Branch Naming

Use short, descriptive branches that identify the owner or workstream:

```text
mudasar/workflow-claim-api
feature/<contributor-name>/workflow-claim-api
schema/<contributor-name>/job-request-fulfillment
fix/<contributor-name>/auth-refresh-token
docs/<contributor-name>/agent-guardrails
```

Avoid vague branch names such as `changes`, `fixes`, `new-work`, or `agent-output`.

## Avoiding Merge Conflicts

- Pull the latest `main` before creating a branch.
- Keep each branch focused on one API, workflow, schema slice, or fix.
- Do not let multiple agents edit the same file unless one agent owns the file and the others provide notes only.
- Coordinate ownership of high-conflict files such as SQL seed scripts, Dapper repositories, DI registration, and shared DTOs.
- Prefer additive SQL changes and idempotent scripts.
- Avoid broad renames or formatting-only edits during feature work.
- Update backend docs in the same PR as endpoint, schema, or workflow behavior changes.

## Resolving Merge Conflicts

1. Commit or stash your current work.
2. Fetch the latest remote changes.
3. Rebase or merge the latest `main` into your branch.
4. Resolve conflicts by preserving the intended behavior from both sides.
5. Re-run `dotnet test`.
6. If SQL changed, run the database script runner or document why it was not run.
7. Review the diff before pushing.
8. Push the resolved branch and update the PR description with conflict-resolution notes.

Recommended commands:

```powershell
git fetch origin
git switch <your-branch>
git rebase origin/main
```

If the rebase becomes confusing:

```powershell
git rebase --abort
```

Then ask the code owner or coordinating contributor for help.

## AI Agent Rules

- Each AI agent must be assigned an explicit file/folder scope.
- AI agents must read `AGENTS.md`, `SECURITY_GUIDELINES.md`, and this file before editing.
- AI agents must follow SOLID principles pragmatically while avoiding unnecessary abstractions or overengineered designs.
- Backend changes must preserve layer boundaries: thin controllers, focused application services, repository-owned persistence, and explicit DTO contracts.
- AI agents must not overwrite unrelated user or agent work.
- AI agents must report files changed, tests run, SQL scripts changed, and unresolved risks.
- Contributors are responsible for reviewing AI-generated code before opening a PR.
