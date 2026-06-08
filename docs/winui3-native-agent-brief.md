# WinUI 3 Native App Brief

Goal: build Telemetry Lab as a native Windows 11 app without Streamlit while preserving the core dashboard workflow.

The WinUI app must treat the existing Streamlit implementation as a frontend that is being replaced, not as the product architecture. Reusable behavior should live outside UI code where practical. GitHub Actions packaging comes last, after the app opens locally and looks correct.

## Stack

- WinUI 3.
- Windows App SDK stable available on this machine.
- .NET 8 with `net8.0-windows10.0.19041.0`.
- Unpackaged, self-contained publish for `win-x64` only after local validation.
- No native `NavigationView` for now. A prior WinUI 3 project on this machine hit black-window/native crashes in unpackaged/self-contained mode.
- Avoid `InfoBar` until explicitly re-tested. It previously crashed in `Microsoft.UI.Xaml.dll`.

## Visual Target

Reference apps: Windows 11 Settings, PowerToys, Snipping Tool, Windows Terminal.

The app should use:

- extended titlebar;
- Mica first, Acrylic fallback;
- Segoe UI;
- calm left navigation made from stable controls;
- cards and setting rows instead of old form layouts;
- icons in action buttons;
- rounded corners around 6-8 px;
- light/dark theme following Windows where possible.

Avoid:

- WinForms/WPF-looking gray forms;
- generic web dashboard layout;
- black-window-prone controls;
- cropped text;
- cluttered cards inside cards.

## Validation

Before packaging or GitHub Actions:

1. build locally;
2. publish locally;
3. open the executable on Windows;
4. keep it alive for at least 60 seconds;
5. capture a screenshot;
6. check app logs under `%LOCALAPPDATA%\TelemetryLab`;
7. check Event Viewer if it crashes;
8. only then add CI/package workflow.

## First Native Milestone

The first milestone should support:

- open a HWiNFO CSV from the Windows file picker;
- type/paste a readable CSV path;
- reread the same CSV while it is still growing;
- show sample/sensor counts;
- show curated power, temperature, FPS, and load summaries;
- show a simple native chart;
- show raw columns/rows preview.

