---
title: "Flashcards"
type: flashcards
tags: [meta, flashcards]
updated: 2026-04-08
---

# Flashcards

Spaced repetition cards for the [Spaced Repetition](https://github.com/st3v3nmw/obsidian-spaced-repetition) Obsidian plugin.

## Format

Each flashcard uses this format:

```
Question text goes here
?
Answer text goes here
```

Separate cards with blank lines. The `?` on its own line separates question from answer.

Ask the LLM to generate flashcards from any wiki page:
```
Generate flashcards from [[concepts/concept-name]]
```

---

## Cards

What is the purpose of the "ingest" workflow?
?
The ingest workflow reads a raw source document, creates a summary page, identifies and creates/updates concept and entity pages, adds cross-links between all touched pages, and updates the index and log.

What are the three confidence levels and when is each used?
?
**High** — well-established idea with multiple corroborating sources and concrete examples. **Medium** — supported by sources but limited examples or single-source. **Low** — single mention, anecdotal, or speculative.

What does the "lint" operation check for?
?
Orphan pages (no inbound links), stale claims, contradictions between pages, missing cross-links, incomplete sections, and low-confidence pages that could be strengthened.
