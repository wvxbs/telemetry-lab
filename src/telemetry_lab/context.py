# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

import re
from pathlib import Path
from typing import Callable, Any

from telemetry_lab.config import KNOWN_CONTEXT_TERMS
from telemetry_lab.text_utils import pretty_token, repair_mojibake, slugify


def infer_filename_context(stem: str) -> dict[str, Any]:
    slug = slugify(stem)
    performance_mode = ""
    mode_match = re.search(r"(^|-)(gmode|balanced|balanceado|performance|turbo|silent|quiet|eco)(-|$)", slug)
    if mode_match:
        raw_mode = mode_match.group(2)
        performance_mode = {
            "gmode": "G-Mode",
            "balanced": "Balanced",
            "balanceado": "Balanced",
            "performance": "Performance",
            "turbo": "Turbo",
            "silent": "Silent",
            "quiet": "Quiet",
            "eco": "Eco",
        }.get(raw_mode, pretty_token(raw_mode))
    fps_cap = None
    cap_match = re.search(r"(^|-)(\d{2,4})(?:fps)?cap($|-)", slug)
    if cap_match:
        fps_cap = int(cap_match.group(2))
    date_match = re.search(r"(\d{2})-(\d{2})-(\d{4})-(\d{4})", slug)
    captured_at = ""
    if date_match:
        day, month, year, hhmm = date_match.groups()
        captured_at = f"{year}-{month}-{day} {hhmm[:2]}:{hhmm[2:]}"
    tags = []
    if performance_mode:
        tags.append(slugify(performance_mode))
    if fps_cap:
        tags.append(f"{fps_cap}fps-cap")
    return {
        "performance_mode": performance_mode,
        "fps_cap": fps_cap,
        "captured_at": captured_at,
        "file_tags": tags,
    }


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
    file_context = infer_filename_context(p.stem)
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
    if file_context["performance_mode"]:
        title_bits.append(file_context["performance_mode"])
    if file_context["fps_cap"]:
        title_bits.append(f"{file_context['fps_cap']} FPS cap")
    title = " / ".join(dict.fromkeys([bit for bit in title_bits if bit]))
    if not title:
        title = translate("generic_context") if translate else "General report"
    return {
        "title": title,
        "category": category,
        "workload": workload,
        "tags": sorted(set(hits + file_context["file_tags"])) or ["geral"],
        "file_name": repair_mojibake(p.name),
        "folder": repair_mojibake(str(p.parent)) if str(p.parent) != "." else "",
        "performance_mode": file_context["performance_mode"],
        "fps_cap": file_context["fps_cap"],
        "captured_at": file_context["captured_at"],
    }
