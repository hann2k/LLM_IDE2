# LLM IDE2

> **A stateful project runtime for stateless LLM providers.**
> LLM IDE2 adds a stateful project runtime on top of stateless LLM providers, allowing project intent, decisions, context, and outputs to persist even when the underlying model changes.

LLM IDE2 is not another coding agent or code editor.

The goal of this project is to preserve a project's criteria, decisions, state, artifacts, and conversation context inside the IDE, even when switching between different LLM providers such as DeepSeek, Gemini, GPT, Claude, or other models.

Models can change.
Providers can be replaced.
But the project memory should remain inside the IDE.

---

## Why LLM IDE2 Exists

LLMs are powerful, but they are fundamentally request-based.

A model may understand a project well in one session, then forget previous decisions in the next session. When switching to another provider, the tone, reasoning style, coding style, and task judgment may also change. Copying long conversation history into every new session wastes tokens, and important decisions can easily disappear somewhere in the middle of the context.

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

The provider is an execution engine that receives a context package assembled by the IDE and returns a response. The actual project memory is stored inside the local `.llmide` workspace, including `conversation.db`, Criteria, Project State, Artifacts, Rolling Context, request logs, and tool logs.

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

In this architecture, the most important factor is not the capability of a specific model.

Regardless of which LLM provider is used, the IDE reads the same project memory and rebuilds the context needed for the current request. Therefore, continuity is maintained by the IDE's state preservation mechanism, not by the model's memory.

---

## What LLM IDE2 Is

LLM IDE2 is closer to the following:

* A stateful project runtime for LLM-based work
* A local memory system for project intent and decisions
* A context builder that works across multiple LLM providers
* A workspace for running projects together with AI
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

LLM IDE2 does not compete on code generation.

Other tools and agents may write code better.
LLM IDE2 manages what sits above them: **project memory, criteria, decisions, context, and task handoff.**

---

## Target User

The first target user of LLM IDE2 is not a project manager or architect already familiar with large-scale project methodologies.

Instead, LLM IDE2 is designed for people such as:

* Solo developers who can write code but often lose track of what to do first
* Developers with outsourcing or implementation experience who are less familiar with defining product direction and priorities
* People who switch between ChatGPT, Claude, Codex, Cursor, and other tools, causing project context to scatter
* People who spend time searching for decisions made earlier in a conversation
* People who get stuck before asking an LLM or coding agent to do a well-defined task
* People who want to preserve project criteria and change history locally

LLM IDE2 is not a tool that replaces user judgment with the authority of an experienced PM.

Instead, it continuously structures the current state and next actions so that users do not lose the project.

---

# LLM IDE2 Documentation

This directory contains internal design documents for LLM IDE2.

Most documents are currently written in Korean because the project is being actively developed by the author. English summaries will be added gradually as the project stabilizes.

## Documents

### 1. Product Principles

Defines the product direction, core assumptions, and design boundaries of LLM IDE2.

Key topics:

* Stateful project runtime for stateless LLM providers
* IDE-owned project memory
* Provider independence
* Human-in-the-loop workflow
* Separation from coding agents

### 2. Architecture Baseline

Describes the baseline architecture of the project.

Key topics:

* Core-centered architecture
* CLI and WPF sharing the same Core
* Local `.llmide` project storage
* Context Builder
* Provider abstraction
* Conversation, artifacts, and rolling context storage

### 3. Development Roadmap

Tracks the current development direction and planned implementation steps.

Key topics:

* Core / CLI MVP
* WPF integration
* Provider settings
* Rolling context compression
* Artifact extraction
* Agent loop and tool isolation

### 4. Change Decision Log

Records important architectural and product decisions made during development.

Key topics:

* Why certain features were added, postponed, or removed
* Why the project changed direction
* Design trade-offs
* Criteria updates

### 5. Code Convention

Defines development rules and coding conventions.

Key topics:

* Core logic must not be placed in the UI layer
* API keys and personal project data must not be committed
* Build and tests should pass before changes are finalized
* Documentation should be updated when the architecture changes

## Language Note

The main README is written in English for public portfolio and international collaboration purposes.

Detailed internal documents may remain in Korean while the project is under active development. This reflects the current development workflow, not a limitation of the architecture.

---

## Design Principles

### 1. IDE Owns the Memory

Long-term project memory belongs to the IDE, not to the LLM provider.

Criteria, Project State, conversation logs, request logs, artifacts, and context summaries remain inside each project's `.llmide` workspace. The LLM receives a context package assembled by the IDE for each request.

### 2. Provider Is Replaceable

The provider is a replaceable computation engine.

The current baseline provider is DeepSeek, but the Core is designed to avoid dependency on any specific provider. Gemini, GPT, Claude, or other providers should be able to use the same project memory and context builder.

### 3. Context Is Built, Not Remembered

LLM IDE2 does not assume that the LLM remembers.

For every request, the Context Builder combines Project State, active Criteria, recent conversation history, Rolling Context, related artifacts, and the user's request into a Request Context Package.

### 4. Preserve Semantic Commitments

In long conversations, preserving every sentence is not the goal.

What matters is preserving the project's semantic commitments:

* What are we building?
* What have we decided not to build?
* Which criteria must be followed?
* Which decisions have already been made?
* Which constraints must continue to hold?
* What is the next task?

Rolling Context Compression helps preserve these core commitments even as the conversation grows longer.

### 5. Human in the Loop

AI proposes, the IDE records, and the user makes the final decision.

LLM IDE2 prioritizes reviewable records over automatic changes. Important state changes, criteria changes, and artifact updates should be visible and confirmable by the user.

### 6. Tool Isolation

Agent tool calls are isolated from the Core.

Tools start as read-only, and tool call history is recorded separately from conversation messages. Tool execution assumes a restrictable allowlist and auditable logs.

### 7. Core Sharing

CLI and WPF use the same Core and the same `.llmide` data.

The UI is only an adapter. Project memory and core workflows live in the Core.

---

## Project Memory Model

The central object of LLM IDE2 is not the source code file.
It is the project memory.

### Criteria

Criteria are rules that the project must continue to follow.

Examples:

* Always respond in Korean.
* Do not directly compete with coding agents.
* Do not place Core business logic inside the UI layer.
* Do not commit API keys or personal project data.
* Do not automatically apply state changes without user approval.

### Project State

Project State represents the current state of the project.

Examples:

* Current phase
* Completed work
* Work in progress
* Next actions
* Blockers
* Recent decisions

### Conversation History

Conversation History stores conversations with the LLM.

It is not just a chat log.
It is source data for project judgment.

### Rolling Context

Rolling Context is a compressed representation of important context from long conversations.

Instead of injecting the entire previous conversation every time, LLM IDE2 maintains summarized context required for the project.

### Artifacts

Artifacts are outputs extracted from conversations.

Examples:

* README drafts
* Design documents
* Decision logs
* Task instructions
* Code blocks
* Analysis results

### Request Logs

Request Logs record what context was sent to the LLM so that requests can be inspected or reproduced later.

### Tool Logs

Tool Logs provide an audit trail of which agent tools were called and what results they returned.

---

## Working Loop

The basic working loop of LLM IDE2 is not a feature checklist.
It is the following process:

```text
1. The user enters a thought, problem, or task into the IDE.
2. The IDE sends the user's request together with project memory and Criteria to the LLM.
3. The LLM responds based on the current context.
4. The user extracts decisions, criteria, artifacts, and next actions from the response.
5. The IDE stores them into project memory.
6. The next request uses the stored memory again through the Context Builder.
```

The goal of this loop is not to have more conversations.

The goal is to promote meaningful judgments from conversation into project memory and connect them to the next task.

---

## Example Use Cases

### 1. When the Project Direction Becomes Unclear

The user enters a concern such as:

```text
Should this project avoid building coding features directly and instead become an LLM work orchestration tool?
```

The IDE injects existing Criteria, Project State, Decision Log, and previous conversation summaries.
The LLM then proposes whether the new direction conflicts with previous decisions and what decision needs to be made now.

### 2. When Switching to Another LLM

Even if the user works with DeepSeek first and later switches to GPT or Claude, project memory remains inside `.llmide`, not inside the provider.

The new provider receives a Context Package assembled by the IDE and continues from the current project criteria and state.

### 3. When Handing Work to an External Coding Agent

LLM IDE2 can act as a higher-level layer that prepares task context before handing work to external coding agents such as Codex, Claude Code, or Cursor.

Example handoff:

```text
Background:
The current project aims to provide a provider-independent stateful runtime for LLM-based work.

Goal:
Add a Provider Settings screen to the WPF application.

Constraints:
Do not place Core business logic inside the WPF layer.
Preserve the existing .llmide/settings/providers.json structure.
Do not write API keys to logs.

Completion Criteria:
Build and tests must pass.
The updated structure must be reflected in Docs.
```

### 4. When Reflecting Results Back Into Project Memory

After an external agent or the user completes a task, the result can be pasted back into the IDE.

The IDE can then help organize:

* What was actually completed
* What failed
* Newly created decisions
* Criteria that need to be updated
* Next tasks
* Content that should be saved as artifacts

---

## Architecture

LLM IDE2 is a Core-centered runtime, not a UI-centered application.

```text
LlmIde.slnx
├─ src/
│  ├─ LlmIde.Core/
│  ├─ LlmIde.Infrastructure/
│  ├─ LlmIde.Cli/
│  └─ LlmIde.Wpf/
├─ tests/
│  └─ LlmIde.Tests/
├─ policies/
└─ Docs/
```

### LlmIde.Core

Handles project memory and core workflows.

* Project
* Criteria
* Project State
* Context Builder
* Conversation Flow
* Rolling Context
* Artifacts
* Agent Loop models and interfaces

### LlmIde.Infrastructure

Handles external systems and storage.

* SQLite storage
* JSON / JSONL storage
* File system storage
* Provider implementations
* Agent tool implementations

### LlmIde.Cli

A CLI adapter suitable for Core validation and automation.

### LlmIde.Wpf

A GUI adapter that allows users to inspect and manage project memory directly.

### LlmIde.Tests

Regression tests focused on Core and Infrastructure behavior.

---

## Local Storage

The project registry is stored under the executable program root:

```text
<IdeProgramRoot>/project/projects.json
```

Actual project data is stored under `.llmide` in each project root:

```text
<ProjectRoot>/
└─ .llmide/
   ├─ project.json
   ├─ project-state.json
   ├─ criteria.json
   ├─ policies/
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
   ├─ artifacts/
   │  └─ artifacts.index.json
   └─ settings/
      └─ providers.json
```

`conversation.db` is the primary storage.
JSONL and JSON files are auxiliary records for debugging, auditing, and request reproduction.

---

## Provider Strategy

The current baseline provider is DeepSeek.

However, the core value of LLM IDE2 is not tied to a specific provider. The Core treats providers as replaceable interfaces. A provider only receives a request package assembled by the IDE and returns a response.

```text
LlmIde.Core
  → IChatProvider
      → DeepSeek
      → Gemini
      → GPT
      → Claude
      → Other
```

Provider performance and behavior may differ.

What LLM IDE2 tries to preserve is not identical answers across all models, but reduced loss of project context when switching models.

---

## Development Status

LLM IDE2 is currently integrating a WPF GUI on top of a Core/CLI-based MVP.

The following core areas have already been implemented:

* Project registry
* Provider-based conversation
* SQLite + JSONL / JSON storage
* Criteria management
* Project State management
* Rolling Context Compression
* Context Builder v1
* Artifact storage and extraction
* Read-only Agent Loop
* Shared Core/data model between CLI and WPF

Progress is managed by direction rather than by a simple feature list:

> The Core must preserve project memory reliably.
> The UI must allow the user to inspect and modify that memory.
> The provider must remain a replaceable execution engine.

---

## Development Rules

LLM IDE2 development follows these rules:

* Do not commit API keys or personal project data.
* Do not include `.llmide`, `providers.json`, `bin`, `obj`, or `backup` in the repository.
* When structure or roadmap changes, update Docs first.
* Do not place Core business logic inside the UI layer.
* CLI and WPF must use the same Core and the same data structure.
* New features must pass build and tests.
* LLM results should be recorded for review before being applied automatically.

Main documents:

```text
Docs/1.Product_Principles.md
Docs/2.Architecture_Baseline.md
Docs/3.Development_Roadmap.md
Docs/4.Change_Decision_Log.md
Docs/Code_Convention.md
```

---

## Build

Requirements:

* Windows
* .NET 10 SDK
* C#
* WPF

Build:

```bash
dotnet build LlmIde.slnx -c Debug -v:minimal
```

Run tests:

```bash
dotnet run --project tests/LlmIde.Tests/LlmIde.Tests.csproj -c Debug
```

Run WPF:

```bash
dotnet run --project src/LlmIde.Wpf/LlmIde.Wpf.csproj -c Debug
```

Run CLI:

```bash
dotnet run --project src/LlmIde.Cli/LlmIde.Cli.csproj -- <command>
```

---

## License / Usage Terms

This project is **source-available for personal and non-commercial use only**.

Permitted use:

* Reading, cloning, and studying the source code
* Personal, educational, research, and other non-commercial use
* Personal modification for non-commercial purposes

The following uses are prohibited without prior written permission:

* Commercial use
* Sale, relicensing, rental, paid hosting, or paid service operation
* Inclusion in commercial products, SaaS products, internal business tools, or paid consulting deliverables
* Removal of copyright, license, or attribution notices

Commercial use requires prior approval from the author.

Copyright © 2026 hann2k. All rights reserved except as expressly permitted above.

For commercial licensing inquiries, please contact the repository owner.
