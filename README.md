# Telemetry Lab

Want a dashboard with the real telemetry of your PC? Run an HWiNFO64 CSV log, drop it into Telemetry Lab, and get a readable view of temperatures, power, clocks, loads, memory, storage, and other sensors without building spreadsheets by hand.

Telemetry Lab is made for people who test hardware, tune performance, compare machines, run benchmarks, or just want to understand what their PC is doing during a game, render, compile, export, or stress test.

## Pitch

Telemetry Lab turns raw HWiNFO64 CSV logs into an interactive performance dashboard.

Use it to:

- inspect how your PC behaves during games, benchmarks, and creative workloads;
- understand CPU, GPU, memory, disk, temperature, power, and clock behavior in one place;
- compare two telemetry reports side by side;
- register benchmark scores that tools do not keep after execution;
- link benchmark results to the telemetry log captured during the run;
- create custom charts from any numeric sensor in the report;
- keep benchmark history as portable JSON files saved through your browser.

Instead of taking screenshots of scores and scrolling through giant CSV files, you can keep a clean record of what happened, where it happened, and how the machine behaved while it happened.

## Apresentacao

Quer uma dashboard com as informacoes reais do seu PC? Rode um log CSV no HWiNFO64, abra no Telemetry Lab, e veja temperaturas, consumo, clocks, cargas, memoria, armazenamento e outros sensores de um jeito legivel, sem montar planilha na mao.

O Telemetry Lab foi feito para quem testa hardware, ajusta desempenho, compara maquinas, roda benchmarks, ou simplesmente quer entender o que o PC esta fazendo durante um jogo, render, compilacao, exportacao ou teste de estresse.

Use para:

- analisar o comportamento do PC em jogos, benchmarks e programas de criacao;
- entender CPU, GPU, memoria, disco, temperatura, consumo e clocks em um so lugar;
- comparar dois relatorios de telemetria lado a lado;
- registrar scores de benchmarks que nao salvam resultado depois da execucao;
- relacionar o resultado do benchmark ao log capturado durante o teste;
- criar graficos customizados com qualquer sensor numerico do relatorio;
- manter historico de benchmarks em arquivos JSON portateis salvos pelo navegador.

Em vez de tirar print de score e se perder em CSV gigante, voce guarda um registro claro do que foi testado, qual foi o resultado, e como a maquina se comportou durante o teste.

## Features

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

The app is in Portuguese by default and includes an English switch in the sidebar.

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
docker build -t wvxbs/telemetry-lab:latest .
```

Run detached with Docker Compose:

```bash
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

## License

This project is licensed under the GNU General Public License v3.0 or later (GPL-3.0-or-later). See [LICENSE](LICENSE) for details.

Copyright (C) 2026 Gabriel Ferreira.

## Author And Contact

- Author: Gabriel Ferreira
- Email: gabriel.ferreira7854@gmail.com
- LinkedIn: https://www.linkedin.com/in/gabriel-ferreira-021a44140/
- GitHub: https://github.com/wvxbs
