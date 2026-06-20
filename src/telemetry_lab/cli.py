# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Gabriel Ferreira
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

from telemetry_lab.cli_service import format_text, load_cli_report, to_jsonable
from telemetry_lab.units import normalize_temperature_unit


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="telemetry-lab",
        description="Read HWiNFO CSV telemetry from the terminal using Telemetry Lab's core parser and metric selection.",
    )
    parser.add_argument("csv", help="Path to a HWiNFO CSV file readable by this process.")
    parser.add_argument(
        "--group",
        choices=["summary", "gaming", "cpu", "gpu", "memory", "storage", "power", "temperature", "fps", "all"],
        default="summary",
        help="Metric group to print. Default: summary.",
    )
    parser.add_argument(
        "--temperature",
        choices=["C", "F"],
        default="C",
        help="Temperature unit for display. Default: C.",
    )
    parser.add_argument(
        "--format",
        choices=["text", "json"],
        default="text",
        help="Output format. Default: text.",
    )
    parser.add_argument("--limit", type=int, default=24, help="Maximum metric rows to print. Use 0 for no limit.")
    parser.add_argument("--live", action="store_true", help="Treat the CSV as an ongoing log and refresh continuously.")
    parser.add_argument("--interval", type=float, default=2.0, help="Refresh interval in seconds for --live. Default: 2.")
    parser.add_argument("--once", action="store_true", help="With --live, read once but label values as live/current.")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    path = Path(args.csv).expanduser()
    if not path.exists():
        parser.error(f"CSV not found: {path}")

    limit = None if args.limit == 0 else max(1, args.limit)
    temperature = normalize_temperature_unit(args.temperature)

    try:
        if args.live and not args.once:
            return _run_live(path, args.group, temperature, args.format, limit, max(0.5, args.interval))
        report = load_cli_report(path, temperature_unit=temperature, live=args.live, group=args.group)
        _print_report(report, args.format, limit)
        return 0
    except KeyboardInterrupt:
        return 130
    except Exception as exc:
        print(f"telemetry-lab: {exc}", file=sys.stderr)
        return 1


def _run_live(path: Path, group: str, temperature: str, output_format: str, limit: int | None, interval: float) -> int:
    while True:
        report = load_cli_report(path, temperature_unit=temperature, live=True, group=group)
        if output_format == "text":
            print("\033[2J\033[H", end="")
        _print_report(report, output_format, limit)
        if output_format == "json":
            sys.stdout.flush()
        time.sleep(interval)


def _print_report(report, output_format: str, limit: int | None) -> None:
    if output_format == "json":
        print(json.dumps(to_jsonable(report, limit=limit), ensure_ascii=False, indent=2))
        return
    print(format_text(report, limit=limit))


if __name__ == "__main__":
    raise SystemExit(main())
