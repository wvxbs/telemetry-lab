# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

import re
import unicodedata


def repair_mojibake(value: str) -> str:
    if not any(marker in value for marker in ("\u00c3", "\u00c2", "\ufffd")):
        return value
    try:
        repaired = value.encode("cp1252", errors="strict").decode("utf-8", errors="strict")
    except UnicodeError:
        return value
    return repaired if repaired != value else value


def slugify(value: str) -> str:
    value = repair_mojibake(value)
    text = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii")
    text = re.sub(r"[^a-zA-Z0-9]+", "-", text.lower()).strip("-")
    return text or "telemetry"


def pretty_token(value: str) -> str:
    value = repair_mojibake(value)
    value = re.sub(r"[_-]+", " ", value).strip()
    return value.title() if value else value


def ascii_fold(value: str) -> str:
    value = repair_mojibake(value)
    return unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii").lower()


def category_for_metric(name: str) -> str:
    low = ascii_fold(name)
    if "gpu" in low:
        return "GPU"
    if "cpu" in low or "core" in low:
        return "CPU"
    if "mem" in low or "ram" in low:
        return "Mem\u00f3ria"
    if "disk" in low or "ssd" in low or "drive" in low:
        return "Disco"
    if "temp" in low or " c" in low:
        return "Temperatura"
    if "w" in low or "power" in low:
        return "Pot\u00eancia"
    return "Outros"
