# Telemetry Lab CLI

The CLI is a terminal surface over the same parsing and metric-selection core used by the Streamlit dashboard.

It is intentionally simpler than the UI. It prints filtered, practical telemetry summaries instead of charts and rich analysis.

## Local Usage

Run from the repository:

```bash
PYTHONPATH=src python -m telemetry_lab.cli "/path/to/report.CSV"
```

Useful options:

```bash
PYTHONPATH=src python -m telemetry_lab.cli report.CSV --group gaming
PYTHONPATH=src python -m telemetry_lab.cli report.CSV --group power
PYTHONPATH=src python -m telemetry_lab.cli report.CSV --group temperature --temperature F
PYTHONPATH=src python -m telemetry_lab.cli report.CSV --format json
```

## Docker Usage

The CLI image is:

```text
wvxbs/telemetry-lab-cli
```

Run it by mounting the folder that contains your HWiNFO CSV:

```bash
docker run --rm \
  -v "/path/to/reports:/reports:ro" \
  wvxbs/telemetry-lab-cli \
  /reports/report.CSV
```

Gaming-focused summary:

```bash
docker run --rm \
  -v "/path/to/reports:/reports:ro" \
  wvxbs/telemetry-lab-cli \
  /reports/report.CSV --group gaming
```

JSON output for scripts:

```bash
docker run --rm \
  -v "/path/to/reports:/reports:ro" \
  wvxbs/telemetry-lab-cli \
  /reports/report.CSV --group summary --format json
```

## Live CSV Reading

Use `--live` when HWiNFO is still writing the CSV:

```bash
docker run --rm \
  -v "/path/to/reports:/reports:ro" \
  wvxbs/telemetry-lab-cli \
  /reports/report.CSV --group gaming --live --interval 2
```

In live mode, the primary value is the latest sample and is labeled as current.

In static mode, the primary value is never treated as current:

- power, load, FPS, RAM, and VRAM use average as the primary value;
- temperatures use peak as the primary value;
- the raw last sample remains available in JSON as `last_sample`.

## Groups

Available groups:

- `summary`: compact CPU/GPU/system/FPS/VRAM signals.
- `gaming`: FPS, GPU/CPU power, temperatures, usage, RAM, and VRAM.
- `power`: curated power sensors.
- `temperature`: curated temperature sensors.
- `fps`: detected frame-rate metrics.
- `all`: every numeric sensor after Telemetry Lab parsing.

## Build Locally

```bash
docker build -f Dockerfile.cli -t wvxbs/telemetry-lab-cli:local .
```

Then run:

```bash
docker run --rm \
  -v "/path/to/reports:/reports:ro" \
  wvxbs/telemetry-lab-cli:local \
  /reports/report.CSV
```
