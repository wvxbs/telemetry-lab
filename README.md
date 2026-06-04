# Telemetry Lab

Want a clean, interactive dashboard with the real telemetry of your PC? Run an HWiNFO64 CSV log, open it in Telemetry Lab, and turn raw sensor data into a clear story about temperatures, power, clocks, load, memory, storage, and system behavior.

Telemetry Lab helps you understand what happened during a benchmark, a gaming session, a render, an export, a compile, or any demanding workload. It is built for people who want answers, not screenshots scattered across folders and giant CSV files nobody wants to read by hand.

## Pitch

Telemetry Lab turns HWiNFO64 logs into a practical performance dashboard for your machine.

With it, you can see how your PC behaves under pressure, compare different runs, keep benchmark scores organized, and connect each result to the telemetry captured during the test. That means you can stop guessing whether a game is CPU-bound, whether your GPU is power-limited, whether temperatures changed after a tweak, or whether a new configuration actually improved anything.

It is especially useful for:

- gaming performance checks, from quick sessions to repeatable test runs;
- benchmark tracking, including tools that do not keep score history for you;
- creative workloads, such as rendering, exporting, compiling, and editing;
- before-and-after comparisons after driver, BIOS, cooling, power, or tuning changes;
- building a personal performance history without relying on screenshots.

Instead of opening a CSV and hunting for meaning, you upload the report and get charts, summaries, comparisons, benchmark records, and custom visualizations in one place.

## Apresentacao

Quer uma dashboard bonita e direta com as informacoes reais do seu PC? Rode um relatorio CSV no HWiNFO64, abra no Telemetry Lab e transforme dados brutos de sensores em uma visao clara sobre temperaturas, consumo, clocks, carga, memoria, armazenamento e comportamento do sistema.

O Telemetry Lab ajuda voce a entender o que aconteceu durante um benchmark, uma partida, um render, uma exportacao, uma compilacao ou qualquer carga pesada. Ele foi feito para quem quer resposta, nao uma pasta cheia de prints e arquivos CSV gigantes que ninguem tem paciencia de ler na mao.

Com ele, voce consegue ver como o PC se comporta sob pressao, comparar execucoes diferentes, organizar scores de benchmarks e relacionar cada resultado ao log de telemetria capturado durante o teste. Isso ajuda a responder perguntas como: o jogo esta limitado por CPU? A GPU bateu limite de energia? A temperatura mudou depois de um ajuste? A nova configuracao realmente melhorou alguma coisa?

Ele e especialmente util para:

- analisar desempenho em jogos, de sessoes rapidas a testes repetiveis;
- acompanhar benchmarks, inclusive ferramentas que nao guardam historico de scores;
- avaliar programas de criacao, renderizacao, exportacao, compilacao e edicao;
- comparar antes e depois de drivers, BIOS, refrigeracao, energia ou ajustes finos;
- criar um historico pessoal de desempenho sem depender de screenshots.

Em vez de abrir um CSV e procurar significado no caos, voce sobe o relatorio e tem graficos, resumos, comparacoes, registros de benchmark e visualizacoes customizadas em um so lugar.

## Features

- Open HWiNFO64 CSV reports directly from the browser.
- Use typed paths only when you intentionally expose a path to the app process, such as local development or an optional Docker bind mount.
- Infer context from file and folder names, such as `benchmarks/games/valorant/report.csv`.
- Keep working even when the path has no useful context, using a general report fallback.
- Preserve rich views for known Cinebench 2026 logs while still exposing generic numeric sensors from any HWiNFO CSV.
- Switch temperature display between Celsius and Fahrenheit.
- Register benchmark results with custom benchmark names, score names, values, and units.
- Save benchmark records through the browser, not inside the container.
- Load saved benchmark JSON files back into the app.
- Link benchmark records to the telemetry report captured during the same run.
- Compare two telemetry reports side by side.
- Build custom charts with line, area, scatter, bar, heatmap, and table modes.
- Enable live reload for CSV files that are still being written, when using a typed path accessible to the app.

The app is in Portuguese by default and includes an English switch in the sidebar.

## Browser File Model

The browser is the main file interface. The container serves the app, but it is not the user's file manager and benchmark files are not saved inside it unless the user explicitly downloads or saves them through the browser.

Use the browser to manage files:

- open CSV files with the upload picker;
- download benchmark JSON files with the download button;
- in Chrome/Edge, use the directory picker button to choose a folder and write the JSON directly there;
- upload existing benchmark JSON files to read them back.

The optional typed path input exists for advanced cases only. It only works for paths the app process can see. In Docker, that means a bind mount you configured yourself. For ordinary user-selected files, use browser upload.

## Docker

Build the image using the project naming convention:

```bash
docker build -t wvxbs/telemetry-lab:latest .
```

Run detached with Docker Compose:

```bash
docker compose up -d --build
```

Open the dashboard at <http://localhost:8501>. Choose the CSV in the browser, then save benchmark records through download or the browser directory picker.

No volume is required for normal use.

## Optional Mounted Reports

Mounting reports is optional. Use it only for very large files, repeated comparisons, or live reload from a CSV that is still being written.

```bash
docker run -d \
  --name telemetry-lab \
  --restart unless-stopped \
  -p 8501:8501 \
  -e TELEMETRY_LAB_REPORT_DIR=/reports \
  -v "/path/to/your/hwinfo-reports:/reports:ro" \
  wvxbs/telemetry-lab:latest
```

The `TELEMETRY_LAB_REPORT_DIR` value is only a convenience default for the optional path field. The browser upload flow works without it.

## Detached Docker Run

Without Compose:

```bash
docker run -d \
  --name telemetry-lab \
  --restart unless-stopped \
  -p 8501:8501 \
  wvxbs/telemetry-lab:latest
```

## Local Development

```bash
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

## License

This project is licensed under the GNU General Public License v3.0 or later (GPL-3.0-or-later). See [LICENSE](LICENSE) for details.

Copyright (C) 2026 Gabriel Ferreira.

## Author And Contact

- Author: Gabriel Ferreira
- Email: gabriel.ferreira7854@gmail.com
- LinkedIn: https://www.linkedin.com/in/gabriel-ferreira-021a44140/
- GitHub: https://github.com/wvxbs
