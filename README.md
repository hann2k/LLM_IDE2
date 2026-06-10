# LLM IDE2

> **A stateful project runtime for stateless LLM providers.**
> LLM IDE2 keeps a project's intent, decisions, context, and outputs alive inside the IDE — even when the underlying model or provider changes.

LLM IDE2 is not another coding agent or code editor.

Its goal is to preserve a project's criteria, decisions, state, artifacts, and conversation context inside the IDE, even when switching between LLM providers such as DeepSeek, Gemini, GPT, Claude, or others.

Models can change.
Providers can be replaced.
But the project memory should remain inside the IDE.

---

## Two Applications, One Core

LLM IDE2 ships as **two desktop applications that share the same Core, the same agent tools, and the same program root** (project registry, common policies, and provider settings). Pick the surface that fits the work; the project memory underneath is identical.

| Application | Project | Focus |
| --- | --- | --- |
| **Chat IDE** | `LlmIde.Wpf.Chat` | The original chat-centric project-memory IDE: project registry, streaming chat, conversation log with importance, artifact extraction, read-only tools. |
| **Writing Workspace** | `LlmIde.Wpf.Writer` | The AI writing assistant: a three-column workspace (projects · outline/body · docked chat) where the LLM helps research, outline, draft, and revise. |

Both apps are thin WPF adapters over `LlmIde.Core` and reuse shared dialogs and menu handlers from `LlmIde.Wpf.Lib`. A `LlmIde.Cli` adapter exposes the same Core for validation and automation.

The **Writing Workspace** is the first product direction of LLM IDE2 (an AI Writing Assistant), built on the same stateful runtime rather than as a separate codebase.

---

## Why LLM IDE2 Exists

LLMs are powerful, but they are fundamentally request-based.

A model may understand a project well in one session, then forget previous decisions in the next. When switching to another provider, the tone, reasoning style, and task judgment may also change. Copying long conversation history into every new session wastes tokens, and important decisions can easily disappear somewhere in the middle of the context.

Many current tools focus on helping LLMs write code better.
LLM IDE2 focuses on a different problem.

**More important than who writes the code is how the project's intent and decisions are preserved.**

LLM IDE2 starts from the following questions:

* If LLMs are stateless, where should project memory live?
* Can the same project context be maintained across multiple interchangeable LLM providers?
* How can decisions, criteria, artifacts, and next actions from conversations be preserved?
* Before handing work over to a coding agent, how should the task be organized?
* How can users benefit from AI assistance while still keeping final decision authority?

The answer of LLM IDE2 is simple:

> **The LLM is the computation engine. The IDE owns the project memory.**

---

## Core Idea

LLM IDE2 does not treat the LLM provider as the owner of project memory.

The provider is an execution engine that receives a context package assembled by the IDE and returns a response. The actual project memory lives in the local `.llmide` workspace — `conversation.db`, Criteria, Project State, Artifacts, Rolling Context, request logs, and tool logs.

```text
Project Memory
  ├─ Criteria
  ├─ Project State
  ├─ Conversation History
  ├─ Rolling Context
  ├─ Artifacts
  ├─ Request Logs
  └─ Tool Logs
        ↓
Context Builder
        ↓
Provider Request Package
        ↓
DeepSeek / Gemini / GPT / Claude / Other LLM
```

Regardless of which provider is used, the IDE reads the same project memory and rebuilds the context needed for the current request. Continuity is maintained by the IDE's state preservation mechanism, not by the model's memory.

---

## What LLM IDE2 Is

* A stateful project runtime for LLM-based work
* A local memory system for project intent and decisions
* A context builder that works across multiple LLM providers
* A workspace for running projects — and now writing documents — together with AI
* A higher-level layer for preparing task context before handing work to coding agents

LLM IDE2 is designed around the following workflow:

```text
Idea
 → Criteria refinement
 → Project state recording
 → Conversation
 → Decision preservation
 → Artifact extraction
 → Context compression
 → Next task definition
 → Handoff to external LLMs or coding agents
 → Result review
 → Reflection back into project memory
```

---

## What LLM IDE2 Is Not

LLM IDE2 is not intended to be:

* A replacement for coding agents such as Cursor, Claude Code, Codex, or Devin
* A code-completion IDE
* A Jira, Linear, or Confluence clone for large organizations
* A repository analyzer that reads every file and all source code automatically
* An autonomous development system that blindly trusts and applies LLM responses

Other tools and agents may write code better.
LLM IDE2 manages what sits above them: **project memory, criteria, decisions, context, and task handoff.**

---

## Target User

The first target user of LLM IDE2 is not a project manager or architect already familiar with large-scale methodologies. Instead, LLM IDE2 is designed for people such as:

* Solo developers and writers who can produce content but often lose track of what to do first
* People who switch between ChatGPT, Claude, Codex, Cursor, and other tools, causing project context to scatter
* People who spend time searching for decisions made earlier in a conversation
* People who get stuck before asking an LLM or coding agent to do a well-defined task
* People who want to preserve project criteria and change history locally

LLM IDE2 does not replace user judgment with the authority of an experienced PM. It continuously structures the current state and next actions so that users do not lose the project.

---

## The Writing Workspace

The Writing Workspace (`LlmIde.Wpf.Writer`) applies the stateful runtime to long-form writing — research notes, drafts, technical docs, blog posts, README files.

```text
┌──────────┬────────────────────────────┬───────────────┐
│          │   Outline    │    Body     │ Conversation  │
│ Projects │──────────────┴─────────────│     log       │
│          │   Artifacts (cards, low)    │   Composer    │
└──────────┴────────────────────────────┴───────────────┘
   left: projects   center: outline|body + artifacts   right: docked chat
```

Key behaviors:

* **Left** — project list. **Center top** — outline tree and body editor. **Center bottom** — artifact cards. **Right** — a single docked conversation (log + composer).
* **The LLM edits editor content, not files.** It never touches the filesystem directly. Current content is provided on demand only (`get_body`), to save tokens.
* **Body edits are never auto-applied.** When the LLM proposes a revision (`propose_body_edit`), a **before / after** dialog opens and the user confirms or cancels.
* Outline supports add / add-child / rename / delete, inline editing, automatic chapter numbering, and drag-and-drop reordering.
* Outline and body are persisted as plain files in the **project root** (`outline.json` + `<id>.md`), so read-only tools can access them. Save triggers: after a confirmed LLM edit, on `Ctrl+S`, and on focus change.

---

## Design Principles

### 1. IDE Owns the Memory

Long-term project memory belongs to the IDE, not to the LLM provider. Criteria, Project State, conversation logs, request logs, artifacts, and context summaries remain inside each project's `.llmide` workspace. The LLM receives a context package assembled by the IDE for each request.

### 2. Provider Is Replaceable

The provider is a replaceable computation engine. The current baseline provider is DeepSeek, but the Core avoids dependency on any specific provider. Providers are managed once, in common settings shared by every project and both apps.

### 3. Context Is Built, Not Remembered

For every request, the Context Builder combines Project State, active Criteria, recent conversation history, Rolling Context, related artifacts, and the user's request into a Request Context Package.

### 4. Preserve Semantic Commitments

In long conversations, preserving every sentence is not the goal. What matters is preserving the project's semantic commitments: what we are building, what we decided not to build, which criteria must hold, which decisions are already made, and what the next task is. Rolling Context Compression preserves these even as the conversation grows.

### 5. Human in the Loop

AI proposes, the IDE records, and the user makes the final decision. LLM IDE2 prioritizes reviewable records over automatic changes. Body revisions, state changes, criteria changes, and artifact updates should be visible and confirmable by the user.

### 6. Tool Isolation

Agent tool calls are isolated from the Core. Tools are read-only over the filesystem, and tool call history is recorded separately from conversation messages. Tool execution assumes a restrictable allowlist and auditable logs. Externally imported conversations are wrapped in `<imported>` tags so they are treated as data, not commands (prompt-injection defense).

### 7. Core Sharing

The CLI and both WPF apps use the same Core and the same `.llmide` data. The UI is only an adapter; project memory and core workflows live in the Core.

---

## Agent Tools

Tools are read-only over the filesystem and scoped to the project folder. The system folder `.llmide` is never exposed as raw files.

| Tool | Purpose |
| --- | --- |
| `web_search` | Web search (DuckDuckGo HTML). |
| `fetch_url` | Fetch a URL (localhost / private IP / file scheme blocked). |
| `read_file` | Read a file under the project root (no absolute paths, no `..`, no `.llmide`). |
| `list_files` | List files/folders under the project root (same guards). |
| `artifacts_list` / `read_artifact` | List and read stored artifacts via `ArtifactService`. |
| `get_body` | (Writer) Inject the current body section on demand. |
| `propose_body_edit` | (Writer) Propose a full body revision → approval dialog. |

---

## Project Memory Model

The central object of LLM IDE2 is not the source code file. It is the project memory.

* **Criteria** — rules the project must keep following (e.g. respond in Korean; do not place Core logic in the UI; do not commit API keys). Edited per project.
* **Project State** — current phase, completed work, work in progress, next actions, blockers, recent decisions.
* **Conversation History** — conversations with the LLM, stored as source data for project judgment, not just a chat log.
* **Rolling Context** — a compressed representation of important context from long conversations, injected instead of the full history.
* **Artifacts** — outputs extracted from conversations (README drafts, design docs, decision logs, task instructions, code blocks, analysis results).
* **Request Logs** — what context was sent to the LLM, so requests can be inspected or reproduced.
* **Tool Logs** — an audit trail of which agent tools were called and what they returned.

---

## Architecture

LLM IDE2 is a Core-centered runtime, not a UI-centered application.

```text
LlmIde.slnx
├─ src/
│  ├─ LlmIde.Core/          project memory + core workflows
│  ├─ LlmIde.Infrastructure/ storage, providers, agent tools
│  ├─ LlmIde.Cli/           CLI adapter
│  ├─ LlmIde.Wpf.Lib/       shared WPF UI, dialogs, WorkspaceWindowBase
│  ├─ LlmIde.Wpf.Chat/      Chat IDE app
│  └─ LlmIde.Wpf.Writer/    Writing Workspace app
├─ tests/
│  └─ LlmIde.Tests/
├─ policies/                common policy templates (shipped with the app)
└─ Docs/
```

* **LlmIde.Core** — Project, Criteria, Project State, Context Builder, Conversation Flow, Rolling Context, Artifacts, Agent Loop models, and the `IDocumentBodyBridge` used by Writer body editing.
* **LlmIde.Infrastructure** — SQLite / JSON / JSONL / file storage, provider implementations, and agent tool implementations.
* **LlmIde.Cli** — a CLI adapter suitable for Core validation and automation.
* **LlmIde.Wpf.Lib** — shared dialogs (project, edit, tool manager, artifact viewer, provider manager) and `WorkspaceWindowBase`, which hosts the menu handlers common to both apps. Menu XAML stays per-app, so each app's GUI can differ.
* **LlmIde.Wpf.Chat / LlmIde.Wpf.Writer** — the two applications; each owns its MainWindow and ViewModel.
* **LlmIde.Tests** — regression tests focused on Core and Infrastructure behavior.

A `LlmIde.Server` adapter is planned as a later stage.

---

## Local Storage

Settings that are common to every project and both apps live under the **program root** (the shared `bin` output):

```text
<ProgramRoot>/
├─ project/projects.json     project registry
├─ policies/                 common policies (system / compression / artifact / importance rules, prompts.json)
└─ settings/providers.json   common LLM providers + API keys
```

Per-project memory lives under `.llmide` in each project root:

```text
<ProjectRoot>/
├─ outline.json              (Writer) outline
├─ <id>.md                   (Writer) section bodies
└─ .llmide/
   ├─ project.json
   ├─ project-state.json
   ├─ criteria.json          per-project criteria
   ├─ conversations/
   │  ├─ conversation.db
   │  ├─ messages.jsonl
   │  ├─ requests.jsonl
   │  ├─ tool-calls.jsonl
   │  ├─ context-packages/
   │  └─ rolling-context/
   │     ├─ current.md
   │     ├─ history/
   │     └─ index.jsonl
   └─ artifacts/
      └─ artifacts.index.json
```

`conversation.db` is the primary storage; JSONL/JSON files are auxiliary records for debugging, auditing, and request reproduction.

> Policies and provider settings used to be seeded per project under `.llmide`. They are now **common at the program root**, so policies are edited once at deploy time and providers are managed once via **Edit ▸ LLM**. Only project-specific criteria stay per project.

---

## Provider Strategy

The current baseline provider is DeepSeek, but the core value of LLM IDE2 is not tied to any provider. The Core treats providers as replaceable interfaces; a provider only receives a request package and returns a response. Every project is offered every configured provider.

```text
LlmIde.Core
  → IChatProvider
      → DeepSeek
      → Gemini
      → GPT
      → Claude
      → Other
```

What LLM IDE2 preserves is not identical answers across models, but reduced loss of project context when switching models. Providers and API keys are managed through **Edit ▸ LLM** and stored in the common `settings/providers.json`.

---

## Development Status

LLM IDE2 has completed its Core/CLI/WPF MVP (Phase 9) and is now in productization (Phase 10), centered on the Writing Workspace.

Already implemented:

* Project registry, creation / clone / rename / delete
* Provider-based conversation with streaming
* SQLite + JSONL/JSON storage
* Criteria and Project State management
* Rolling Context Compression and Context Builder v1
* Artifact storage and extraction
* Read-only Agent Loop and tools
* WPF split into Lib + Chat + Writer, sharing one Core and program root
* Writing Workspace: outline editing, body file persistence, on-demand body editing by the LLM with a before/after approval dialog
* Common policies/settings at the program root; provider management via Edit ▸ LLM

> The Core must preserve project memory reliably.
> The UI must let the user inspect and modify that memory.
> The provider must remain a replaceable execution engine.

---

## Build

Requirements: Windows, .NET 10 SDK, C#, WPF.

```bash
# build the whole solution
dotnet build LlmIde.slnx -c Debug -v:minimal

# run tests
dotnet run --project tests/LlmIde.Tests/LlmIde.Tests.csproj -c Debug

# run the Chat IDE
dotnet run --project src/LlmIde.Wpf.Chat/LlmIde.Wpf.Chat.csproj -c Debug

# run the Writing Workspace
dotnet run --project src/LlmIde.Wpf.Writer/LlmIde.Wpf.Writer.csproj -c Debug

# run the CLI
dotnet run --project src/LlmIde.Cli/LlmIde.Cli.csproj -- <command>
```

---

## Documentation

Internal design documents live in `Docs/`. They are written mostly in Korean during active development; this README is the English entry point.

```text
Docs/1.Product_Principles.md     product direction and design boundaries
Docs/2.Architecture_Baseline.md  baseline architecture
Docs/3.Development_Roadmap.md     roadmap and implementation steps
Docs/3.1 GUI.md                  GUI design
Docs/4.Change_Decision_Log.md    architectural / product decisions (DEC log)
Docs/Code_Convention.md          coding conventions
```

Development rules:

* Do not commit API keys or personal project data.
* Do not include `.llmide`, `providers.json`, `bin`, `obj`, or `backup` in the repository.
* When structure or roadmap changes, update Docs first.
* Do not place Core business logic inside the UI layer.
* The CLI and both WPF apps must use the same Core and data structures.
* New features must pass build and tests.
* LLM results should be recorded for review before being applied automatically.

---

## License / Usage Terms

This project is **source-available for personal and non-commercial use only**.

Permitted use:

* Reading, cloning, and studying the source code
* Personal, educational, research, and other non-commercial use
* Personal modification for non-commercial purposes

Prohibited without prior written permission:

* Commercial use
* Sale, relicensing, rental, paid hosting, or paid service operation
* Inclusion in commercial products, SaaS products, internal business tools, or paid consulting deliverables
* Removal of copyright, license, or attribution notices

Commercial use requires prior approval from the author.

Copyright © 2026 hann2k. All rights reserved except as expressly permitted above.

For commercial licensing inquiries, please contact the repository owner.
