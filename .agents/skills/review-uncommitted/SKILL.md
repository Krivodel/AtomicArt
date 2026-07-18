---
name: review-uncommitted
description: Review of uncommitted .cs and .axaml files using code-style-reviewer and axaml-reviewer
---

## CODEX COMMAND BOUNDARY

The active Codex root agent is only the orchestrator and aggregator for this command.

The root agent MUST:
- Execute only this skill
- Get the uncommitted file list from git
- Review only uncommitted `.cs` and `.axaml` files
- Send each `.cs` file to `code-style-reviewer`
- Send each `.axaml` file to `axaml-reviewer`
- Send only file paths to reviewers, not extra review instructions
- Use maximum 1 file per reviewer for concentration
- Use maximum 4 parallel reviewer agents; batch sequentially when there are more files
- Aggregate all returned findings into one numbered table
- Return the table in Russian and stop

The root agent MUST NOT:
- Modify files
- Fix findings
- Refactor anything
- Review committed files
- Review file types other than `.cs` and `.axaml`
- Invoke implementation, fix, design, or research skills
- Spawn `worker` agents
- Continue into fixing after the review is complete

Allowed writes:
- None

If `code-style-reviewer` or `axaml-reviewer` custom agents are unavailable, stop and tell the user which custom agent is unavailable. Do not replace the missing reviewers with root-agent review.

Прокинь каждый незалитый .cs и .axaml файл из гита в code-style-reviewer и axaml-reviewer. Составь таблицу замечаний с нумерацией. Максимум 1 файл на 1 ревьюера для концентрации. Максимум 4 параллельных агента.
