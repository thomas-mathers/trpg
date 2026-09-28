# TRPG Codebase Guide

## Project Overview

TRPG is a single-player text RPG where an LLM acts as game master — narrating scenes, roleplaying every NPC in conversation, and describing the consequences of the player's actions. The world is procedurally generated (countries, states, cities, districts, buildings, and NPCs with their own professions, schedules, relationships, and daily routines) and persists across sessions.

The LLM's role is deliberately narrow: it narrates and roleplays, but doesn't decide game outcomes. Combat, movement, and time all run through deterministic code (`CombatEngine`, the game clock, scheduling/generator classes); the LLM is only brought in afterward to describe what already happened. This keeps core mechanics reliable and repeatable regardless of which model is behind `IChatClient`.

Backend conventions live in `api/AGENTS.md`; frontend conventions live in `spa/AGENTS.md`. One-time local machine setup (git hooks, MCP servers) is documented in `docs/local-setup.md`.

---

## Agent Output

- Don't restate or summarize what was just built/changed after finishing a task — the diff already shows it. State only new information: caveats, follow-ups, or what to verify.

---

## Comments

- Explain *why*, never *how* — well-named identifiers make the what and how obvious
- One line, maximum. If the justification needs more than that, fix the code (better name, extracted helper) instead of writing a paragraph
- Only when truly necessary — a future reader must be left genuinely confused without it. Default to no comment; most code needs zero
- Justify the code locally and stay context-free: no references to other files, past decisions, tickets, memory docs, or session history. A comment tied to an external fact goes stale the moment that fact changes; one that only depends on the adjacent line(s) can't
- Never comment out code — delete it; git history has it if it's needed again
- No closing-brace comments (`} // end if`) — if a block is long enough to seem to need one, extract a named helper instead

---

## Functions

- Each function does one thing — if you need "and" to describe what it does, split it
- Prefer pure functions (depend only on their parameters, return a value, no side effects) where possible
- Keep functions under 40 lines; if a function exceeds this, extract helpers
- Avoid flag parameters that switch behavior (e.g. a boolean `verbose` parameter) on new code — prefer two separate, differently-named functions over one function with a branching bool

---

## Classes

- Each class, component, or module has a single responsibility — if describing it requires "and", split it into two
- No hard line-count ceiling — length by itself isn't the problem, mixed responsibilities are. Something creeping past a few hundred lines of actual logic is a prompt to re-check whether it's still doing one thing, not an automatic violation
- Something that's mostly static literal data (e.g. a name-pool array or a constants file) can run long without needing a split — the length reflects data volume, not complexity

---

## Bugfixes

- For a bugfix, write a test that reproduces it and confirm it fails before touching the fix. Only then apply the fix and confirm the test passes.

---

## Gameplay Workflows

- Trace a player action end-to-end before coding: UI submission, command validation, mutation, resulting events, narration, and follow-up UI state.
- For cross-layer workflows, cover the critical seams: command outcome, event/transport mapping, and UI handoff. Exercise both outcomes of probabilistic or stateful branches through deterministic seams.
- When a design review asks for a proposal or plan before edits, stop at the proposal until the shape is agreed.

---

## GitHub CLI

- GitHub CLI credentials are stored in the Windows keyring and are unavailable inside the filesystem sandbox. Always run `gh` commands with elevated sandbox permissions; do not treat sandboxed `gh auth status` failures as an invalid user login.
