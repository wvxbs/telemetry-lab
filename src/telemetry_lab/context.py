# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

from pathlib import Path
from typing import Callable, Any

from telemetry_lab.config import KNOWN_CONTEXT_TERMS
from telemetry_lab.text_utils import pretty_token, repair_mojibake, slugify


def infer_context(source: str, translate: Callable[[str], str] | None = None) -> dict[str, Any]:
    p = Path(source)
    raw_parts = [part for part in p.parts if part not in ("/", "\\")]
    tokens = [slugify(part) for part in raw_parts if slugify(part)]
    hits = []
    for token in tokens:
        if token in KNOWN_CONTEXT_TERMS or any(term in token for term in KNOWN_CONTEXT_TERMS):
            hits.append(token)
    parent = slugify(p.parent.name)
    stem = slugify(p.stem)
    workload = hits[-1] if hits else parent or stem or "geral"
    category = "geral"
    joined = "/".join(tokens)
    if any(term in joined for term in ("games", "game", "jogos", "jogo", "valorant")):
        category = "games"
    elif any(term in joined for term in ("produtividade", "productivity")):
        category = "produtividade"
    elif any(term in joined for term in ("benchmarks", "benchmark", "cinebench", "geekbench")):
        category = "benchmarks"
    generic_workload_terms = ("benchmark", "history", "games", "game", "jogos", "jogo", "benchmarks")
    if parent and not any(term in parent for term in generic_workload_terms):
        workload = parent
    title_bits = [pretty_token(category), pretty_token(workload)]
    title = " / ".join(dict.fromkeys([bit for bit in title_bits if bit]))
    if not title:
        title = translate("generic_context") if translate else "General report"
    return {
        "title": title,
        "category": category,
        "workload": workload,
        "tags": sorted(set(hits)) or ["geral"],
        "file_name": repair_mojibake(p.name),
        "folder": repair_mojibake(str(p.parent)) if str(p.parent) != "." else "",
    }
