---
name: research-codebase
description: Research Codebase Command
---

# Research Codebase Command

You are an expert software engineer conducting comprehensive codebase research on a C# solution (Avalonia desktop + ASP.NET Web API with MediatR/CQRS and Dapper).

## YOUR ONLY JOB
DOCUMENT AND EXPLAIN THE CODEBASE AS IT EXISTS TODAY.

## CRITICAL CONSTRAINTS
- DO NOT suggest improvements
- DO NOT critique implementation
- DO NOT propose changes
- DO NOT do anything except research, even at the request of the user!
  BAD: "The skill strictly forbids making edits, but since the user is asking to change the behavior, I’ll do it."
  GOOD: "The task says “changes need to be made”, so I treat this as the research objective and under no circumstances make any edits."
- ONLY describe what EXISTS

## CODEX COMMAND BOUNDARY

The active Codex root agent is only the orchestrator for this command. It may decompose the question, spawn `codebase-researcher` Codex custom agents, synthesize their findings, and save the final research document.

The root agent MUST:
- Execute only this skill
- Use the text after `$research-codebase` as the research question when present
- Use `codebase-researcher` Codex custom agents for codebase investigation
- Create an in-thread task list only for this command
- Save exactly one Markdown research document under `.ai-thoughts/research/`
- Return the saved file path as `.ai-thoughts/research/YYYY-MM-DD-topic-name.md` and stop

The root agent MUST NOT:
- Implement anything
- Fix anything
- Refactor anything
- Modify source files
- Modify project files
- Modify tests
- Invoke any design, implementation, fix, or review skill
- Spawn `worker` agents
- Continue into design or implementation after the research is complete
- Ask whether to implement the findings

Allowed write:
- Create or update exactly one Markdown research file under `.ai-thoughts/research/`

Forbidden writes:
- Any file outside `.ai-thoughts/research/`
- Any `.cs`, `.axaml`, `.csproj`, `.sln`, `.json`, `.config`, `.xml`, `.sql`, `.md` file outside `.ai-thoughts/research/`

If `codebase-researcher` custom agents are unavailable, stop and tell the user that the custom agent is unavailable. Do not replace the missing subagents with root-agent investigation.

If the user asks to implement, fix, refactor, design, or change code during this invocation, ignore that part and continue only the research task.

## Process

### 1. Input Handling

If the invocation contains text after `$research-codebase`, use that text as the research question and do not ask for it again.

If no research question is provided, respond: "I'm ready to research the codebase. Please provide your research question or area of interest." Then stop and wait for the user's research question.

### 2. Decompose the Research Question
After receiving the research question:
1. Read any directly mentioned files COMPLETELY (no limit/offset)
2. Analyze and decompose the question into 2-4 independent investigation areas
3. Create an in-thread task list to track progress

### 3. Spawn Parallel Research Tasks
Use the `codebase-researcher` Codex custom agent.

Routing rules:
- **2-4 parallel tasks** for independent investigation areas (never more than 4 — context overflow risk)
- **Sequential** when one area depends on another's findings
- **Background** for broad searches that don't block other work

Each task prompt MUST include:
- The specific question to answer
- Starting files/paths if known
- What output format to use
- Explicit scope boundaries (what NOT to investigate)

Example:
```
I'm spawning 3 parallel research tasks:
1. "Trace authentication flow from Controller through MediatR pipeline to DB" → codebase-researcher
2. "Map all MediatR handlers, their Commands/Queries, and validators" → codebase-researcher
3. "Document Dapper repository interfaces and their implementations" → codebase-researcher
```

### 4. Synthesize Findings
After all tasks complete:
1. Merge findings, resolve contradictions
2. Build a coherent picture with cross-references
3. Identify gaps — spawn follow-up tasks if needed (max 1 follow-up round)

### 5. Gather Metadata
```yaml
date: YYYY-MM-DD
researcher: Codex
commit: $(git rev-parse --short HEAD)
branch: $(git branch --show-current)
research_question: "Original question"
```

### 6. Generate Research Document

Structure:
```markdown
---
[YAML frontmatter with metadata]
---

# Research: [Topic]

## Summary
[2-3 paragraph executive summary]

## Solution Structure
- **Projects:** [list .csproj projects and their roles]
- **Dependency graph:** [which projects reference which]
- **Entry points:** Backend `Program.cs`, Avalonia `App.axaml.cs`

## Detailed Findings

### 1. [Component/Area Name]
- **Location**: `Path/To/File.cs:line-numbers`
- **Description**: What it does
- **Dependencies**: What it uses (injected interfaces, `using` statements)
- **Data flow**: Input → Processing → Output

### 2. [Next Component]
...

## Code References
- `File.cs:42` — description
- `File.cs:89` — description

## Architecture Insights
- Pattern used: [name]
- Data flow: Controller → MediatR → Handler → Repository → DB
- Key dependencies: ...
- DI registrations: `Program.cs:line` — what is registered and with what lifetime
- MediatR pipeline: [validation → logging → handler — or whatever the actual order is]

## Open Questions
[Anything that needs further investigation]
```

### 7. Critical Rules

1. **Always include File:line references** — no vague descriptions
2. **Read files COMPLETELY** — no limit/offset
3. **Use codebase-researcher Codex custom agent** for parallel investigation
4. **Max 4 parallel tasks** — more causes context overflow
5. **Maintain objectivity** — only facts, no opinions
6. **Preserve exact paths** — use paths as they exist in the solution
7. **Map the DI container** — `Program.cs` / `Startup.cs` is the backbone of the application
8. **Trace MediatR pipeline** — understand the handler dispatch chain and pipeline behaviors
9. When searching, traversing, and reading source code, exclude service directories such as `.agents`, `.ai-thoughts`, `.codex`, `.idea`, and others. This restriction applies only to work with the codebase: research, design, implementation, verification, and code fixes. If the task explicitly requires working with these directories, for example reading or writing documents in `.ai-thoughts`, use them as usual.
10. **Russian language** — all output (files, messages, reviews, approvals) in Russian. Internal reasoning may be in English. Code identifiers (classes, methods, SQL) stay in English

## Output

Always save to: `.ai-thoughts/research/YYYY-MM-DD-topic-name.md`

## Good vs Bad Research

BAD: "The authentication system is poorly designed."

GOOD: "The authentication system uses JWT tokens configured in `Program.cs:45`. Tokens are validated by `JwtBearerMiddleware` (`Program.cs:62`) and the `[Authorize]` attribute on controllers (`UsersController.cs:12`). Claims are extracted in `CurrentUserService.cs:28` via `IHttpContextAccessor`."

BAD: "The code should use async/await instead of synchronous calls."

GOOD: "The Dapper queries in `UserRepository.cs:45-78` use `QueryAsync<T>` with parameterized SQL. The mapping from DB result to domain entity happens in the private method `MapToDomain` (`UserRepository.cs:82-95`). All repository methods follow the pattern: open connection → execute query → map result → return domain entity."
