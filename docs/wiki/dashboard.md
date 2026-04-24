---
title: "Dashboard"
type: dashboard
tags: [meta]
updated: 2026-04-08
---

# Dashboard

Live queries powered by the [Dataview](https://github.com/blacksmithgu/obsidian-dataview) Obsidian plugin.

## Low Confidence Pages

Pages that need more sources or evidence to strengthen.

```dataview
TABLE confidence, sources, updated
FROM "wiki/concepts" OR "wiki/entities"
WHERE confidence = "low"
SORT updated DESC
```

## All Concepts by Tag

```dataview
TABLE tags, confidence, updated
FROM "wiki/concepts"
SORT file.name ASC
```

## Recently Updated Pages

The 15 most recently modified wiki pages.

```dataview
TABLE type, tags, updated
FROM "wiki/"
SORT updated DESC
LIMIT 15
```

## Pages with Most Sources

Pages informed by the greatest number of raw sources.

```dataview
TABLE length(sources) AS "Source Count", confidence, updated
FROM "wiki/concepts" OR "wiki/entities"
WHERE sources
SORT length(sources) DESC
LIMIT 10
```

## Orphan Check

Pages that may lack inbound links (review manually — Dataview cannot check incoming links directly).

```dataview
TABLE type, tags, updated
FROM "wiki/concepts" OR "wiki/entities"
WHERE length(file.inlinks) = 0
SORT updated ASC
```

## Entity Overview

```dataview
TABLE tags, updated
FROM "wiki/entities"
SORT file.name ASC
```
