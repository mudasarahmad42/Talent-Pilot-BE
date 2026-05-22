# Contributor Log Template

Every contributor must maintain a personal session log under:

```text
contributors/<contributor-name>/README.md
```

Use 10-20 lines per work session. Keep it factual and reviewable.

## Branch And Push Rules

- Each contributor must use their own branch.
- Do not work directly on `main`.
- Do not push automatically after a coding or documentation session.
- Push only when Mudasar Ahmad or the current user explicitly asks for a push.
- If the working tree contains mixed changes, stage and push only the files that belong to the current requested task.

## Required Format

```text
## YYYY-MM-DD - Branch: <branch-name>

- Commit summary: <short summary or "pending commit">
- Purpose: <what this session tried to accomplish>
- Files touched: <important files or folders>
- Endpoints changed: <yes/no, endpoint names if yes>
- Schema changed: <yes/no, script names if yes>
- Seed/stored procedures changed: <yes/no, script names if yes>
- Tests run: <commands and result>
- Known risks: <remaining gaps or "none known">
- AI assistance: <tool/model used or "none">
```

## Review Rule

Pull requests without an updated contributor log should not be approved.
