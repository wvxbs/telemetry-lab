# Telemetry Lab

Streamlit dashboard for HWiNFO CSV logs, benchmark score records, report comparison, and custom charts.

The app is in Portuguese by default and includes an English switch in the sidebar.

## What It Does

- Opens HWiNFO CSV files from browser upload.
- Also accepts a typed path when that path is accessible to the app process, such as local execution paths or Docker bind mounts.
- Infers report context from file and folder names, such as `benchmarks/games/valorant/report.csv`.
- Falls back to a general report context when the path does not carry useful metadata.
- Keeps rich known metrics for the original Cinebench 2026 logs while also exposing generic numeric sensors from any HWiNFO CSV.
- Registers benchmark scores with arbitrary benchmark names, metric names, values, and units.
- Saves benchmark records through the browser, not inside the container.
- Reads benchmark JSON records back through browser upload.
- Links a benchmark record to the currently loaded telemetry report.
- Compares two telemetry CSV reports.
- Provides standard charts plus a custom chart generator with line, area, scatter, bar, heatmap, and table modes.
- Offers opt-in live reload for CSV files that are still being written when using a typed path accessible to the app.

## Docker File Model

In Docker mode, the container serves the app. It should not be used as the user's file manager and benchmark files are not saved inside it.

Use the browser to manage files:

- open CSV files with the upload picker;
- download benchmark JSON files with the download button;
- in Chrome/Edge, use the directory picker button to choose a folder and write the JSON directly there;
- upload existing benchmark JSON files to read them back.

The typed path input still exists, but it only works for paths the app process can see. In Docker that means paths mounted into the container, for example `/data/reports/...`. For ordinary user-selected files, prefer browser upload.

## Docker

Build the image using the project naming convention:

```bash
cd /home/wvxbs/Documentos/tools/telemetry-lab
docker build -t wvxbs/telemetry-lab:latest .
```

Run detached with Docker Compose:

```bash
cd /home/wvxbs/Documentos/tools/telemetry-lab
docker compose up -d --build
```

Open the dashboard at <http://localhost:8501>.

The Compose file mounts this read-only folder so typed paths can still be useful:

```text
/mnt/c/Users/gabri/OneDrive/Documents/tools -> /data/reports (read-only)
```

No write volume is required for benchmark records because saving is browser-managed.

## Detached Docker Run

Without Compose:

```bash
docker run -d \
  --name telemetry-lab \
  --restart unless-stopped \
  -p 8501:8501 \
  -v "/mnt/c/Users/gabri/OneDrive/Documents/tools:/data/reports:ro" \
  wvxbs/telemetry-lab:latest
```

If you do not need typed paths inside Docker, you can omit the `-v` mount and use only browser uploads.

## Local Development

```bash
cd /home/wvxbs/Documentos/tools/telemetry-lab
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
streamlit run app.py
```

When running directly on the machine, typed paths are normal local paths available to your user. Browser upload/download still works the same way.

## Benchmark Records

Benchmark records are saved as `*.telemetry-benchmark.json` files. Each file stores:

- benchmark name;
- scenario/context;
- arbitrary score rows with metric, value, and unit;
- optional link to the loaded HWiNFO CSV report and its inferred context.

Use the Benchmarks tab to create, download, save through Chrome/Edge's directory picker, upload, read, and inspect those records.
