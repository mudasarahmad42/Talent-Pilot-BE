# Contributing To Talent Pilot Backend

This repository uses a protected-main workflow. Contributors must work from feature branches and open pull requests into `main`.

## Main Branch Protection

- Do not push directly to `main`.
- Only the code owner, Mudasar Ahmad, may make direct changes to `main`.
- All contributors, including AI-assisted contributors, must create a branch and open a pull request.
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

Use short, descriptive branches:

```text
feature/workflow-claim-api
schema/job-request-fulfillment
fix/auth-refresh-token
docs/agent-guardrails
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
- AI agents must not overwrite unrelated user or agent work.
- AI agents must report files changed, tests run, SQL scripts changed, and unresolved risks.
- Contributors are responsible for reviewing AI-generated code before opening a PR.
