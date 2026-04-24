---
title: "Analytics"
type: dashboard
tags: [meta]
updated: 2026-04-08
---

# Analytics

Visual analytics powered by the [Charts View](https://github.com/caronchen/obsidian-chartsview-plugin) Obsidian plugin.

## Page Distribution by Type

<!-- CUSTOMIZE: Update these numbers as your wiki grows. -->
<!-- The LLM can update this page during lint operations. -->

```chartsview
type: pie
options:
  legend:
    display: true
    position: right
data:
  - label: Concepts
    value: 0
  - label: Entities
    value: 0
  - label: Summaries
    value: 0
  - label: Syntheses
    value: 0
```

## Confidence Distribution

```chartsview
type: bar
options:
  legend:
    display: false
  indexAxis: y
data:
  - label: High
    value: 0
    backgroundColor: "#4caf50"
  - label: Medium
    value: 0
    backgroundColor: "#ff9800"
  - label: Low
    value: 0
    backgroundColor: "#f44336"
```

## Top Tags

<!-- CUSTOMIZE: Replace these placeholder tags with your actual tags after ingesting sources. -->

```chartsview
type: wordcloud
options:
  maxRotation: 0
  minRotation: 0
data:
  - tag: placeholder-tag-1
    value: 1
  - tag: placeholder-tag-2
    value: 1
  - tag: placeholder-tag-3
    value: 1
```
