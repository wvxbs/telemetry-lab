// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel Ferreira
using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TelemetryLab.WinUI.Telemetry;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace TelemetryLab.WinUI;

public sealed partial class MainWindow : Window
{
    private sealed record HighlightMetric(string Label, string Glyph, string Source, string Value, string Subtitle);

    private readonly CsvTelemetryService _service = new();
    private readonly IntPtr _hwnd;
    private readonly DispatcherTimer _liveReloadTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TelemetryReport _report = TelemetryReport.Empty;
    private TelemetryReport _compareReport = TelemetryReport.Empty;
    private string _section = "Relatório";
    private string _currentPath = string.Empty;
    private string _comparePath = string.Empty;
    private string _language = "pt";
    private string _temperatureUnit = "C";
    private string _chartType = "Linha";
    private string _detailLevel = "Normal";
    private string _metricSearch = string.Empty;
    private double _fpsMinimum = 30;
    private double _fpsMaximum = 1000;
    private long _lastLoadedWriteTicks;
    private long _lastLoadedSize;
    private bool _liveReload;
    private string _liveReloadState = string.Empty;
    private bool _loading;
    private bool _fullscreen;

    private Grid RootGrid = null!;
    private Grid AppTitleBar = null!;
    private StackPanel Sidebar = null!;
    private StackPanel MainSurface = null!;
    private TextBox PathBox = null!;
    private TextBlock HeaderTitle = null!;
    private TextBlock HeaderSubtitle = null!;
    private TextBlock StatusText = null!;
    private FontIcon StatusIcon = null!;
    private Border StatusPanel = null!;
    private TextBlock InstallStateText = null!;
    private TextBlock LiveReloadStateText = null!;

    public MainWindow()
    {
        App.LogInfo("MainWindow start");
        Title = "Telemetry Lab";
        Closed += (_, _) => App.KeepAlive();

        RootGrid = BuildRootGrid();
        Content = RootGrid;
        BuildUi();

        _hwnd = WindowNative.GetWindowHandle(this);
        InitializeWindowChrome();
        TryApplyBackdrop();
        ConfigureKeyboard();
        ConfigureLiveReload();
        SetStatus(T("ready"), T("choose_csv"), StatusKind.Info);
        if (!string.IsNullOrWhiteSpace(App.InitialPath))
        {
            PathBox.Text = App.InitialPath;
            _ = LoadCurrentPathAsync();
        }
    }

    private static Grid BuildRootGrid()
    {
        var grid = new Grid
        {
            MinWidth = 1040,
            MinHeight = 680,
            Background = ResolvePageBackground()
        };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(292) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return grid;
    }

    private void BuildUi()
    {
        AppTitleBar = BuildTitleBar();
        Grid.SetColumnSpan(AppTitleBar, 2);
        RootGrid.Children.Add(AppTitleBar);

        Sidebar = BuildSidebar();
        var sidebarScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = Sidebar
        };
        Grid.SetRow(sidebarScroll, 1);
        RootGrid.Children.Add(sidebarScroll);

        MainSurface = new StackPanel
        {
            Spacing = 18,
            Padding = new Thickness(28, 18, 32, 32)
        };

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = MainSurface
        };
        Grid.SetRow(scroll, 1);
        Grid.SetColumn(scroll, 1);
        RootGrid.Children.Add(scroll);

        RenderMainSurface();
    }

    private Grid BuildTitleBar()
    {
        var titleBar = new Grid
        {
            Height = 40,
            Padding = new Thickness(12, 0, 148, 0),
            Background = TransparentBrush()
        };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleBar.Children.Add(new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(4),
            Background = AccentBrush(),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new FontIcon
            {
                Glyph = "\uE9D2",
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.White)
            }
        });

        var title = new TextBlock
        {
            Text = "Telemetry Lab",
            FontSize = 12,
            Opacity = 0.72,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        Grid.SetColumn(title, 1);
        titleBar.Children.Add(title);
        return titleBar;
    }

    private StackPanel BuildSidebar()
    {
        var side = new StackPanel
        {
            Spacing = 14,
            Padding = new Thickness(16, 14, 14, 18),
            Background = new SolidColorBrush(IsLightTheme
                ? Color.FromArgb(0x58, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x46, 0x18, 0x18, 0x18))
        };

        side.Children.Add(new TextBlock
        {
            Text = "Telemetry Lab",
            FontSize = 20,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
            Margin = new Thickness(4, 0, 0, 0)
        });
        side.Children.Add(new TextBlock
        {
            Text = T("tagline"),
            FontSize = 12,
            Opacity = 0.68,
            Margin = new Thickness(4, -10, 0, 2)
        });

        PathBox = new TextBox
        {
            PlaceholderText = T("csv_path"),
            CornerRadius = new CornerRadius(8),
            MinHeight = 36,
            Text = _currentPath
        };
        side.Children.Add(PathBox);

        var openActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        openActions.Children.Add(BuildActionButton("\uE8E5", T("open"), OpenCsv_Click, primary: true));
        openActions.Children.Add(BuildActionButton("\uE72C", T("reload"), Reload_Click));
        side.Children.Add(openActions);

        side.Children.Add(BuildToggleRow(T("live_reload"), _liveReload, (_, _) =>
        {
            _liveReload = !_liveReload;
            UpdateLiveReloadTimer();
            RebuildSidebar();
        }));
        side.Children.Add(BuildLiveReloadStateRow());

        side.Children.Add(BuildNavItem("\uE9D2", "Relatório", T("report")));
        side.Children.Add(BuildNavItem("\uE7FC", "Jogos", T("gaming")));
        side.Children.Add(BuildNavItem("\uE945", "Potência", T("power")));
        side.Children.Add(BuildNavItem("\uE9CA", "Temperaturas", T("temperatures")));
        side.Children.Add(BuildNavItem("\uE7C1", "Quadros", T("frames")));
        side.Children.Add(BuildNavItem("\uE8AB", "Comparar", T("compare")));
        side.Children.Add(BuildNavItem("\uE8A7", "Dados", T("data")));
        side.Children.Add(BuildNavItem("\uE8A5", "Gráfico", T("custom_chart")));
        side.Children.Add(BuildNavItem("\uE8FD", "Glossário", T("glossary")));
        side.Children.Add(BuildNavItem("\uE896", "Instalação", T("install")));

        side.Children.Add(new Border { Height = 1, Background = SubtleBorderBrush(), Margin = new Thickness(0, 4, 0, 0) });
        side.Children.Add(BuildSettingCombo(T("language"), _language, ["pt", "en"], value => value == "pt" ? "Português" : "English", value =>
        {
            _language = value;
            RebuildSidebar();
            RenderMainSurface();
        }));
        side.Children.Add(BuildSettingCombo(T("temperature_unit"), _temperatureUnit, ["C", "F"], value => value == "C" ? T("celsius") : T("fahrenheit"), value =>
        {
            _temperatureUnit = value;
            RenderMainSurface();
        }));
        side.Children.Add(BuildSettingCombo(T("chart_type"), _chartType, ["Linha", "Área", "Dispersão", "Barras", "Heatmap"], TranslateChartType, value =>
        {
            _chartType = value;
            RenderMainSurface();
        }));
        side.Children.Add(BuildSettingCombo(T("detail_level"), _detailLevel, ["Essencial", "Normal", "Completo"], TranslateDetailLevel, value =>
        {
            _detailLevel = value;
            RenderMainSurface();
        }));

        var searchBox = new TextBox
        {
            Header = T("search"),
            Text = _metricSearch,
            PlaceholderText = T("search_placeholder"),
            CornerRadius = new CornerRadius(8),
            MinHeight = 34
        };
        searchBox.TextChanged += (_, _) =>
        {
            _metricSearch = searchBox.Text;
            RenderMainSurface();
        };
        side.Children.Add(searchBox);

        side.Children.Add(new Border { Height = 1, Background = SubtleBorderBrush(), Margin = new Thickness(0, 8, 0, 0) });
        side.Children.Add(BuildSmallFooter("\uE946", "GPL-3.0-or-later"));
        side.Children.Add(BuildSmallFooter("\uE713", T("f11_hint")));
        return side;
    }

    private UIElement BuildHeader()
    {
        var header = new Grid { ColumnSpacing = 18 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var copy = new StackPanel { Spacing = 5 };
        HeaderTitle = new TextBlock
        {
            Text = _report.Title,
            FontSize = 32,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
            TextWrapping = TextWrapping.Wrap
        };
        HeaderSubtitle = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(_report.Source) ? T("choose_csv_short") : _report.Source,
            FontSize = 13,
            Opacity = 0.76,
            TextWrapping = TextWrapping.Wrap
        };
        copy.Children.Add(HeaderTitle);
        copy.Children.Add(HeaderSubtitle);
        header.Children.Add(copy);

        StatusIcon = new FontIcon { Glyph = "\uE895", FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
        StatusText = new TextBlock { Text = "Pronto", FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        var statusStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        statusStack.Children.Add(StatusIcon);
        statusStack.Children.Add(StatusText);
        StatusPanel = new Border
        {
            Padding = new Thickness(10, 5, 10, 5),
            CornerRadius = new CornerRadius(6),
            Background = TransparentBrush(),
            Child = statusStack,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(StatusPanel, 1);
        header.Children.Add(StatusPanel);
        return header;
    }

    private void RenderMainSurface()
    {
        MainSurface.Children.Clear();
        MainSurface.Children.Add(BuildHeader());
        if (_report.RowCount == 0)
        {
            MainSurface.Children.Add(BuildEmptyState());
            return;
        }

        MainSurface.Children.Add(BuildKpiRow());
        switch (_section)
        {
            case "Jogos":
                MainSurface.Children.Add(BuildGamingSection());
                break;
            case "Potência":
                MainSurface.Children.Add(BuildMetricSection("Potência", T("power_subtitle")));
                break;
            case "Temperaturas":
                MainSurface.Children.Add(BuildMetricSection("Temperatura", T("temperature_subtitle")));
                break;
            case "Quadros":
                MainSurface.Children.Add(BuildFramesSection());
                break;
            case "Comparar":
                MainSurface.Children.Add(BuildCompareSection());
                break;
            case "Gráfico":
                MainSurface.Children.Add(BuildCustomChartSection());
                break;
            case "Glossário":
                MainSurface.Children.Add(BuildGlossarySection());
                break;
            case "Instalação":
                MainSurface.Children.Add(BuildInstallSection());
                break;
            case "Dados":
                MainSurface.Children.Add(BuildDataSection());
                break;
            default:
                MainSurface.Children.Add(BuildOverviewSection());
                break;
        }

        SetStatus(T("loaded"), $"{_report.RowCount:N0} {T("samples").ToLowerInvariant()}, {_report.SensorCount:N0} {T("sensors").ToLowerInvariant()}.", StatusKind.Success);
    }

    private UIElement BuildEmptyState()
    {
        var panel = BuildCardStack(T("empty_title"), T("empty_subtitle"));
        panel.Children.Add(new TextBlock
        {
            Text = T("empty_body"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.76
        });
        return BuildCard(panel);
    }

    private UIElement BuildKpiRow()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        for (var i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        var cards = new[]
        {
            BuildKpiCard(T("samples"), _report.RowCount.ToString("N0"), "\uE8EF"),
            BuildKpiCard(T("sensors"), _report.SensorCount.ToString("N0"), "\uE8A5"),
            BuildKpiCard(T("power"), _report.Summaries.Count(m => m.Group == "Potência").ToString("N0"), "\uE945"),
            BuildKpiCard(T("temperatures"), _report.Summaries.Count(m => m.Group == "Temperatura").ToString("N0"), "\uE9CA")
        };
        for (var i = 0; i < cards.Length; i++)
        {
            Grid.SetColumn(cards[i], i);
            grid.Children.Add(cards[i]);
        }

        return grid;
    }

    private Border BuildKpiCard(string label, string value, string glyph)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Left });
        stack.Children.Add(new TextBlock { Text = value, FontSize = 26, FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 } });
        stack.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.7 });
        return BuildCard(stack, padding: 16);
    }

    private UIElement BuildQuickLookSection()
    {
        var metrics = new[]
        {
            BuildHighlightMetric(T("fps_now"), "\uE7C1", m => CsvTelemetryService.IsFpsMetric(m.Name)),
            BuildHighlightMetric(T("cpu_power_now"), "\uE950", m => IsCpuMetric(m.Name) && CsvTelemetryService.IsPowerMetric(m.Name)),
            BuildHighlightMetric(T("gpu_power_now"), "\uE7F4", m => IsGpuMetric(m.Name) && CsvTelemetryService.IsPowerMetric(m.Name), RankGpuPowerMetric),
            BuildHighlightMetric(T("system_power_now"), "\uE945", m => IsSystemPowerMetric(m.Name)),
            BuildHighlightMetric(T("cpu_temp_now"), "\uE9CA", m => IsCpuMetric(m.Name) && CsvTelemetryService.IsTemperatureMetric(m.Name)),
            BuildHighlightMetric(T("gpu_temp_now"), "\uE9CA", m => IsGpuMetric(m.Name) && CsvTelemetryService.IsTemperatureMetric(m.Name))
        }
        .Where(metric => metric is not null)
        .Cast<HighlightMetric>()
        .Take(_detailLevel == "Essencial" ? 4 : 6)
        .ToList();

        if (metrics.Count == 0)
        {
            return new TextBlock { Text = T("quick_look_empty"), Opacity = 0.72 };
        }

        var columns = _detailLevel == "Essencial" ? 4 : 3;
        return BuildHighlightGrid(metrics, columns);
    }

    private HighlightMetric? BuildHighlightMetric(string label, string glyph, Func<MetricSummary, bool> predicate, Func<string, int>? rank = null)
    {
        var metric = FindMetric(predicate, rank);
        if (metric is null)
        {
            return null;
        }

        return BuildHighlightFromMetric(label, glyph, metric, metric.Last, metric.Average);
    }

    private HighlightMetric BuildHighlightFromMetric(string label, string glyph, MetricSummary metric, double value, double comparison)
    {
        var unit = UnitForMetric(metric.Name);
        return new HighlightMetric(
            label,
            glyph,
            metric.Name,
            FormatHighlightValue(DisplayValue(metric.Name, value), unit),
            $"{T("avg")}: {FormatHighlightValue(DisplayValue(metric.Name, comparison), unit)}");
    }

    private Border BuildHighlightCard(HighlightMetric metric)
    {
        var stack = new StackPanel { Spacing = 7 };
        stack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new FontIcon { Glyph = metric.Glyph, FontSize = 16, Foreground = AccentBrush() },
                new TextBlock { Text = metric.Label, FontSize = 12, Opacity = 0.72, VerticalAlignment = VerticalAlignment.Center }
            }
        });
        stack.Children.Add(new TextBlock
        {
            Text = metric.Value,
            FontSize = 26,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 650 },
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        stack.Children.Add(new TextBlock
        {
            Text = metric.Subtitle,
            FontSize = 11,
            Opacity = 0.64,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        stack.Children.Add(new TextBlock
        {
            Text = metric.Source,
            FontSize = 10,
            Opacity = 0.54,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        return BuildCard(stack, padding: 14);
    }

    private UIElement BuildOverviewSection()
    {
        var panel = BuildCardStack(T("overview"), T("overview_subtitle"));
        panel.Children.Add(BuildQuickLookSection());
        var metrics = FilterMetrics(_report.Summaries).Take(OverviewMetricLimit()).ToList();
        panel.Children.Add(BuildMetricTable(metrics));
        var first = metrics.FirstOrDefault();
        if (first is not null)
        {
            panel.Children.Add(BuildChart(first.Name, _chartType));
        }

        return BuildCard(panel);
    }

    private UIElement BuildMetricSection(string group, string subtitle)
    {
        var metrics = FilterMetrics(_service.CuratedMetrics(_report, group, limit: SectionMetricLimit())).ToList();
        var panel = BuildCardStack(group, subtitle);
        if (metrics.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = T("no_metric"), Opacity = 0.72 });
        }
        else
        {
            panel.Children.Add(BuildMetricTable(metrics));
            panel.Children.Add(BuildChart(metrics[0].Name, _chartType));
        }

        return BuildCard(panel);
    }

    private UIElement BuildGamingSection()
    {
        var panel = BuildCardStack(T("gaming"), T("gaming_subtitle"));
        var highlights = BuildGamingHighlights();
        if (highlights.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = T("no_metric"), Opacity = 0.72, TextWrapping = TextWrapping.Wrap });
            return BuildCard(panel);
        }

        panel.Children.Add(BuildHighlightGrid(highlights, _detailLevel == "Essencial" ? 4 : 3));

        var important = BuildGamingMetricList().ToList();
        if (important.Count > 0)
        {
            panel.Children.Add(BuildMetricTable(FilterMetrics(important).Take(_detailLevel == "Completo" ? 32 : 18)));
            var chartMetric = important.FirstOrDefault(metric => CsvTelemetryService.IsFpsMetric(metric.Name)) ?? important[0];
            panel.Children.Add(BuildChart(chartMetric.Name, _chartType));
        }

        return BuildCard(panel);
    }

    private IReadOnlyList<HighlightMetric> BuildGamingHighlights()
    {
        var cards = new List<HighlightMetric>();
        var fps = FindMetric(metric => CsvTelemetryService.IsFpsMetric(metric.Name), RankHighlightMetric);
        if (fps is not null)
        {
            var filtered = BuildFilteredSummary(fps.Name, _fpsMinimum, _fpsMaximum);
            if (filtered.Samples > 0)
            {
                cards.Add(BuildHighlightFromMetric(T("fps_now"), "\uE7C1", fps, fps.Last, filtered.Average));
                cards.Add(BuildHighlightFromMetric(T("fps_avg"), "\uE9D2", filtered, filtered.Average, filtered.Last));
                cards.Add(BuildHighlightFromMetric(T("fps_1_low"), "\uE74B", filtered, filtered.P1, filtered.Average));
                cards.Add(BuildHighlightFromMetric(T("fps_01_low"), "\uE74B", filtered, filtered.P01, filtered.Average));
            }
        }

        AddCard(cards, T("gpu_power_now"), "\uE7F4", metric => IsGpuMetric(metric.Name) && CsvTelemetryService.IsPowerMetric(metric.Name), RankGpuPowerMetric);
        AddCard(cards, T("gpu_temp_now"), "\uE9CA", metric => IsGpuMetric(metric.Name) && CsvTelemetryService.IsTemperatureMetric(metric.Name), RankHighlightMetric);
        AddCard(cards, T("cpu_temp_now"), "\uE9CA", metric => IsCpuMetric(metric.Name) && CsvTelemetryService.IsTemperatureMetric(metric.Name), RankHighlightMetric);
        AddCard(cards, T("gpu_usage"), "\uE7F4", IsGpuLoadMetric, RankGamingMetric);
        AddCard(cards, T("cpu_usage"), "\uE950", IsCpuLoadMetric, RankGamingMetric);
        AddCard(cards, T("vram_usage"), "\uE8A7", IsVramMetric, RankGamingMetric);
        AddCard(cards, T("ram_usage"), "\uE8A7", IsRamMetric, RankGamingMetric);
        AddCard(cards, T("memory_temp"), "\uE9CA", IsMemoryTemperatureMetric, RankGamingMetric);
        return cards;
    }

    private void AddCard(ICollection<HighlightMetric> cards, string label, string glyph, Func<MetricSummary, bool> predicate, Func<string, int> rank)
    {
        var card = BuildHighlightMetric(label, glyph, predicate, rank);
        if (card is not null)
        {
            cards.Add(card);
        }
    }

    private IEnumerable<MetricSummary> BuildGamingMetricList()
    {
        return _report.Summaries
            .Where(metric =>
                CsvTelemetryService.IsFpsMetric(metric.Name) ||
                IsGpuLoadMetric(metric) ||
                IsCpuLoadMetric(metric) ||
                IsVramMetric(metric) ||
                IsRamMetric(metric) ||
                IsMemoryTemperatureMetric(metric) ||
                (IsGpuMetric(metric.Name) && CsvTelemetryService.IsTemperatureMetric(metric.Name)) ||
                (IsCpuMetric(metric.Name) && CsvTelemetryService.IsTemperatureMetric(metric.Name)) ||
                (IsGpuMetric(metric.Name) && CsvTelemetryService.IsPowerMetric(metric.Name)))
            .Where(metric => !IsVoltageMetric(metric.Name))
            .OrderBy(metric => RankGamingMetric(metric.Name))
            .ThenBy(metric => metric.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    private UIElement BuildHighlightGrid(IReadOnlyList<HighlightMetric> metrics, int columns)
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            RowSpacing = 10
        };
        for (var i = 0; i < columns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        }
        for (var i = 0; i < Math.Ceiling(metrics.Count / (double)columns); i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        for (var i = 0; i < metrics.Count; i++)
        {
            var card = BuildHighlightCard(metrics[i]);
            Grid.SetColumn(card, i % columns);
            Grid.SetRow(card, i / columns);
            grid.Children.Add(card);
        }
        return grid;
    }

    private UIElement BuildDataSection()
    {
        var panel = BuildCardStack(T("data"), T("data_subtitle"));
        panel.Children.Add(BuildMetricTable(FilterMetrics(_report.Summaries).Take(DataMetricLimit())));
        return BuildCard(panel);
    }

    private UIElement BuildMetricTable(IEnumerable<MetricSummary> metrics)
    {
        var stack = new StackPanel { Spacing = 1 };
        stack.Children.Add(BuildMetricHeader());
        foreach (var metric in metrics)
        {
            stack.Children.Add(BuildMetricRow(metric));
        }

        return stack;
    }

    private UIElement BuildMetricHeader()
    {
        return BuildGridRow([T("metric"), T("group"), T("avg"), T("min"), "1%", "0.1%", "P95", T("max"), T("last"), "N"], true);
    }

    private UIElement BuildMetricRow(MetricSummary metric)
    {
        return BuildGridRow([
            metric.Name,
            LocalizeGroup(metric.Group),
            FormatValue(metric, metric.Average),
            FormatValue(metric, metric.Minimum),
            FormatValue(metric, metric.P1),
            FormatValue(metric, metric.P01),
            FormatValue(metric, metric.P95),
            FormatValue(metric, metric.Maximum),
            FormatValue(metric, metric.Last),
            metric.Samples.ToString("N0")
        ], false);
    }

    private static UIElement BuildGridRow(IReadOnlyList<string> values, bool header)
    {
        var row = new Grid
        {
            MinHeight = header ? 34 : 38,
            Padding = new Thickness(8, 4, 8, 4),
            ColumnSpacing = 10,
            Background = header ? LayerBrush(0x20) : TransparentBrush(),
            CornerRadius = new CornerRadius(header ? 6 : 0)
        };
        var widths = new[] { 2.4, 0.8, 0.62, 0.62, 0.62, 0.62, 0.62, 0.62, 0.62, 0.58 };
        foreach (var width in widths.Take(values.Count))
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });
        }

        for (var i = 0; i < values.Count; i++)
        {
            var text = new TextBlock
            {
                Text = values[i],
                FontSize = header ? 12 : 13,
                FontWeight = header ? new Windows.UI.Text.FontWeight { Weight = 600 } : default,
                Opacity = header ? 0.68 : 0.88,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(text, i);
            row.Children.Add(text);
        }

        return row;
    }

    private UIElement BuildChart(string metricName, string chartType)
    {
        var values = _report.Numeric.TryGetValue(metricName, out var series)
            ? series.Where(v => v.HasValue).Select(v => DisplayValue(metricName, v!.Value)).TakeLast(420).ToArray()
            : Array.Empty<double>();
        var unit = UnitForMetric(metricName);
        var xAxis = string.Format(T("x_axis_samples"), values.Length);
        var canvas = new Canvas
        {
            Height = 236,
            MinWidth = 600,
            Background = LayerBrush(IsLightTheme ? (byte)0x50 : (byte)0x34)
        };
        canvas.Loaded += (_, _) => DrawChart(canvas, values, chartType, unit, xAxis);
        canvas.SizeChanged += (_, _) => DrawChart(canvas, values, chartType, unit, xAxis);

        var label = new TextBlock
        {
            Text = $"{metricName} · {TranslateChartType(chartType)}",
            FontSize = 12,
            Opacity = 0.68,
            Margin = new Thickness(0, 8, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var axisLabel = new TextBlock
        {
            Text = $"{T("x_axis")}: {xAxis} · {T("y_axis")}: {unit}",
            FontSize = 11,
            Opacity = 0.62,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(label);
        stack.Children.Add(axisLabel);
        stack.Children.Add(new Border { CornerRadius = new CornerRadius(8), Child = canvas, Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, 2000, 236) } });
        return stack;
    }

    private static void DrawChart(Canvas canvas, IReadOnlyList<double> values, string chartType, string unit, string xAxisLabel)
    {
        canvas.Children.Clear();
        if (values.Count < 2 || canvas.ActualWidth <= 0)
        {
            return;
        }

        var width = canvas.ActualWidth;
        var height = canvas.Height;
        const double left = 58;
        const double right = 12;
        const double top = 14;
        const double bottom = 30;
        var plotWidth = Math.Max(1, width - left - right);
        var plotHeight = Math.Max(1, height - top - bottom);
        var min = values.Min();
        var max = values.Max();
        var span = Math.Max(0.0001, max - min);
        AddChartChrome(canvas, left, top, plotWidth, plotHeight, min, max, unit, xAxisLabel);

        if (chartType == "Heatmap")
        {
            var cellWidth = Math.Max(2, plotWidth / values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                var intensity = (values[i] - min) / span;
                var rect = new Rectangle
                {
                    Width = Math.Ceiling(cellWidth),
                    Height = plotHeight,
                    Fill = new SolidColorBrush(WithAlpha(AccentColor(), (byte)(0x28 + intensity * 0xB8)))
                };
                Canvas.SetLeft(rect, left + i * cellWidth);
                Canvas.SetTop(rect, top);
                canvas.Children.Add(rect);
            }

            return;
        }

        if (chartType == "Barras")
        {
            var barWidth = Math.Max(1, plotWidth / values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                var normalized = (values[i] - min) / span;
                var barHeight = Math.Max(1, normalized * plotHeight);
                var rect = new Rectangle
                {
                    Width = Math.Max(1, barWidth - 1),
                    Height = barHeight,
                    Fill = AccentLayerBrush(0xB0)
                };
                Canvas.SetLeft(rect, left + i * barWidth);
                Canvas.SetTop(rect, top + plotHeight - barHeight);
                canvas.Children.Add(rect);
            }

            return;
        }

        var points = new PointCollection();
        for (var i = 0; i < values.Count; i++)
        {
            var x = left + (values.Count == 1 ? 0 : i * plotWidth / (values.Count - 1));
            var y = top + plotHeight - ((values[i] - min) / span * plotHeight);
            points.Add(new Windows.Foundation.Point(x, y));
        }

        if (chartType == "Área")
        {
            var area = new Polygon
            {
                Fill = AccentLayerBrush(0x4C),
                StrokeThickness = 0
            };
            area.Points.Add(new Windows.Foundation.Point(left, top + plotHeight));
            foreach (var point in points)
            {
                area.Points.Add(point);
            }
            area.Points.Add(new Windows.Foundation.Point(left + plotWidth, top + plotHeight));
            canvas.Children.Add(area);
        }

        if (chartType == "Dispersão")
        {
            foreach (var point in points)
            {
                var dot = new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = AccentBrush()
                };
                Canvas.SetLeft(dot, point.X - 2);
                Canvas.SetTop(dot, point.Y - 2);
                canvas.Children.Add(dot);
            }

            return;
        }

        canvas.Children.Add(new Polyline
        {
            Points = points,
            StrokeThickness = 2.4,
            Stroke = AccentBrush()
        });
    }

    private static void AddChartChrome(Canvas canvas, double left, double top, double plotWidth, double plotHeight, double min, double max, string unit, string xAxisLabel)
    {
        var axisBrush = SubtleBorderBrush();
        canvas.Children.Add(new Line
        {
            X1 = left,
            X2 = left,
            Y1 = top,
            Y2 = top + plotHeight,
            Stroke = axisBrush,
            StrokeThickness = 1
        });
        canvas.Children.Add(new Line
        {
            X1 = left,
            X2 = left + plotWidth,
            Y1 = top + plotHeight,
            Y2 = top + plotHeight,
            Stroke = axisBrush,
            StrokeThickness = 1
        });

        AddCanvasLabel(canvas, FormatAxisValue(max, unit), 8, top - 2, 0.66);
        AddCanvasLabel(canvas, FormatAxisValue(min, unit), 8, top + plotHeight - 14, 0.66);
        AddCanvasLabel(canvas, xAxisLabel, left + Math.Max(0, plotWidth - 150), top + plotHeight + 8, 0.58);
    }

    private static void AddCanvasLabel(Canvas canvas, string text, double left, double top, double opacity)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Opacity = opacity,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 160
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        canvas.Children.Add(label);
    }

    private static string FormatAxisValue(double value, string unit)
    {
        var formatted = Math.Abs(value) >= 100 ? value.ToString("N0") : value.ToString("N1");
        return unit == "-" ? formatted : $"{formatted} {unit}";
    }

    private static StackPanel BuildCardStack(string title, string subtitle)
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 }
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 12,
            Opacity = 0.66,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, -8, 0, 0)
        });
        return stack;
    }

    private static Border BuildCard(UIElement content, double padding = 18)
    {
        return new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(padding),
            Background = new SolidColorBrush(IsLightTheme
                ? Color.FromArgb(0xA8, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x88, 0x2A, 0x2A, 0x2A)),
            BorderBrush = SubtleBorderBrush(),
            Child = content
        };
    }

    private Button BuildActionButton(string glyph, string text, RoutedEventHandler clickHandler, bool primary = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
        content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button
        {
            Content = content,
            MinHeight = 34,
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(6)
        };
        if (primary)
        {
            button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        }

        button.Click += clickHandler;
        return button;
    }

    private UIElement BuildLiveReloadStateRow()
    {
        LiveReloadStateText = new TextBlock
        {
            Text = GetLiveReloadState(),
            FontSize = 12,
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Padding = new Thickness(8, 0, 8, 2)
        };
        row.Children.Add(new FontIcon
        {
            Glyph = _liveReload ? "\uE895" : "\uE711",
            FontSize = 14,
            Foreground = _liveReload ? AccentBrush() : null,
            Opacity = _liveReload ? 1 : 0.72
        });
        row.Children.Add(LiveReloadStateText);
        return row;
    }

    private UIElement BuildNavItem(string glyph, string section, string label)
    {
        var selected = string.Equals(_section, section, StringComparison.OrdinalIgnoreCase);
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 0, 10, 0),
            MinHeight = 38,
            CornerRadius = new CornerRadius(6),
            Background = selected ? AccentLayerBrush(IsLightTheme ? (byte)0x24 : (byte)0x36) : TransparentBrush(),
            BorderBrush = TransparentBrush(),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new FontIcon { Glyph = glyph, FontSize = 16, Foreground = selected ? AccentBrush() : null },
                    new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        button.Click += (_, _) =>
        {
            _currentPath = PathBox.Text;
            _section = section;
            RebuildSidebar();
            RenderMainSurface();
        };
        return button;
    }

    private UIElement BuildToggleRow(string label, bool isOn, RoutedEventHandler handler)
    {
        var toggle = new CheckBox
        {
            Content = label,
            IsChecked = isOn,
            MinHeight = 32
        };
        toggle.Click += handler;
        return toggle;
    }

    private UIElement BuildSettingCombo(string header, string selected, IReadOnlyList<string> values, Func<string, string> label, Action<string> onChanged)
    {
        var combo = new ComboBox
        {
            Header = header,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 34
        };
        foreach (var value in values)
        {
            combo.Items.Add(new ComboBoxItem { Content = label(value), Tag = value });
        }

        combo.SelectedIndex = Math.Max(0, values.ToList().IndexOf(selected));
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string value && value != selected)
            {
                onChanged(value);
            }
        };
        return combo;
    }

    private IEnumerable<MetricSummary> FilterMetrics(IEnumerable<MetricSummary> metrics)
    {
        var query = CsvTelemetryService.Fold(_metricSearch);
        if (string.IsNullOrWhiteSpace(query))
        {
            return metrics;
        }

        return metrics.Where(metric =>
        {
            var text = CsvTelemetryService.Fold($"{metric.Name} {metric.Group} {DescribeMetric(metric.Name)} {LocalizeGroup(metric.Group)}");
            return query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(text.Contains);
        });
    }

    private MetricSummary BuildFilteredSummary(string metricName, double minimum, double maximum)
    {
        if (!_report.Numeric.TryGetValue(metricName, out var series))
        {
            return new MetricSummary(metricName, "FPS", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var clean = series
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Where(value => value >= minimum && value <= maximum)
            .Order()
            .ToArray();

        if (clean.Length == 0)
        {
            return new MetricSummary(metricName, "FPS", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var average = clean.Average();
        var variance = clean.Sum(value => Math.Pow(value - average, 2)) / clean.Length;
        return new MetricSummary(
            metricName,
            "FPS",
            average,
            clean[0],
            clean[^1],
            Percentile(clean, 0.95),
            Percentile(clean, 0.99),
            Percentile(clean, 0.01),
            Percentile(clean, 0.001),
            Percentile(clean, 0.50),
            Math.Sqrt(variance),
            clean[^1],
            clean.Length);
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(sorted.Count * percentile) - 1, 0, sorted.Count - 1);
        return sorted[index];
    }

    private static double Correlation(IReadOnlyList<double> left, IReadOnlyList<double?> right)
    {
        var pairs = left.Zip(right, (x, y) => new { x, y })
            .Where(pair => !double.IsNaN(pair.x) && pair.y.HasValue)
            .Select(pair => (x: pair.x, y: pair.y!.Value))
            .ToArray();
        if (pairs.Length < 8)
        {
            return double.NaN;
        }

        var avgX = pairs.Average(pair => pair.x);
        var avgY = pairs.Average(pair => pair.y);
        var numerator = pairs.Sum(pair => (pair.x - avgX) * (pair.y - avgY));
        var denomX = Math.Sqrt(pairs.Sum(pair => Math.Pow(pair.x - avgX, 2)));
        var denomY = Math.Sqrt(pairs.Sum(pair => Math.Pow(pair.y - avgY, 2)));
        var denominator = denomX * denomY;
        return denominator <= 0.000001 ? double.NaN : numerator / denominator;
    }

    private string FormatValue(MetricSummary metric, double value)
    {
        return DisplayValue(metric.Name, value).ToString("N1");
    }

    private static string FormatHighlightValue(double value, string unit)
    {
        var formatted = Math.Abs(value) >= 100 ? value.ToString("N0") : value.ToString("N1");
        return unit == "-" ? formatted : $"{formatted} {unit}";
    }

    private MetricSummary? FindMetric(Func<MetricSummary, bool> predicate, Func<string, int>? rank = null)
    {
        var ranker = rank ?? RankHighlightMetric;
        return _report.Summaries
            .Where(predicate)
            .Where(metric => !IsVoltageMetric(metric.Name))
            .OrderBy(metric => ranker(metric.Name))
            .ThenBy(metric => metric.Name, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
    }

    private double DisplayValue(string metricName, double value)
    {
        return _temperatureUnit == "F" && CsvTelemetryService.IsTemperatureMetric(metricName)
            ? value * 9 / 5 + 32
            : value;
    }

    private string UnitForMetric(string metricName)
    {
        if (CsvTelemetryService.IsTemperatureMetric(metricName))
        {
            return _temperatureUnit == "F" ? "°F" : "°C";
        }

        var low = CsvTelemetryService.Fold(metricName);
        if (CsvTelemetryService.IsFpsMetric(metricName)) return "FPS";
        if (CsvTelemetryService.IsPowerMetric(metricName)) return "W";
        if (low.Contains("%") || low.Contains("load") || low.Contains("carga") || low.Contains("uso")) return "%";
        if (low.Contains("mhz")) return "MHz";
        if (low.Contains("ghz")) return "GHz";
        if (low.Contains("rpm")) return "RPM";
        if (low.Contains("gb")) return "GB";
        if (low.Contains("mb")) return "MB";

        var bracketStart = metricName.LastIndexOf('[');
        var bracketEnd = metricName.LastIndexOf(']');
        if (bracketStart >= 0 && bracketEnd > bracketStart)
        {
            var unit = metricName[(bracketStart + 1)..bracketEnd].Trim();
            return string.IsNullOrWhiteSpace(unit) ? "-" : unit;
        }

        return "-";
    }

    private static bool IsCpuMetric(string name)
    {
        var low = CsvTelemetryService.Fold(name);
        return low.Contains("cpu") || low.Contains("processador") || low.Contains("core");
    }

    private static bool IsGpuMetric(string name)
    {
        var low = CsvTelemetryService.Fold(name);
        return low.Contains("gpu") || low.Contains("video") || low.Contains("graphics");
    }

    private static bool IsSystemPowerMetric(string name)
    {
        var low = CsvTelemetryService.Fold(name);
        return CsvTelemetryService.IsPowerMetric(name) &&
            (low.Contains("potencia total do sistema") || low.Contains("system total power") || low.Contains("system power"));
    }

    private static bool IsGpuLoadMetric(MetricSummary metric)
    {
        var low = CsvTelemetryService.Fold(metric.Name);
        return IsGpuMetric(metric.Name) && metric.Group == "Carga" &&
            (low.Contains("uso") || low.Contains("load") || low.Contains("utilization") || low.Contains("%"));
    }

    private static bool IsCpuLoadMetric(MetricSummary metric)
    {
        var low = CsvTelemetryService.Fold(metric.Name);
        return IsCpuMetric(metric.Name) && metric.Group == "Carga" &&
            (low.Contains("uso") || low.Contains("load") || low.Contains("utilization") || low.Contains("%"));
    }

    private static bool IsVramMetric(MetricSummary metric)
    {
        var low = CsvTelemetryService.Fold(metric.Name);
        return !IsVoltageMetric(metric.Name) &&
            (low.Contains("vram") || low.Contains("memoria gpu") || low.Contains("gpu memory") || low.Contains("memoria dedicada"));
    }

    private static bool IsRamMetric(MetricSummary metric)
    {
        var low = CsvTelemetryService.Fold(metric.Name);
        return metric.Group == "Memoria" && !IsVramMetric(metric) &&
            (low.Contains("memoria fisica") || low.Contains("physical memory") || low.Contains("ram") || low.Contains("memory load") || low.Contains("carga da memoria"));
    }

    private static bool IsMemoryTemperatureMetric(MetricSummary metric)
    {
        var low = CsvTelemetryService.Fold(metric.Name);
        return CsvTelemetryService.IsTemperatureMetric(metric.Name) &&
            (low.Contains("memoria") || low.Contains("memory") || low.Contains("vram") || low.Contains("junction") || low.Contains("spd hub"));
    }

    private static bool IsVoltageMetric(string name)
    {
        var low = CsvTelemetryService.Fold(name);
        return low.Contains("[v]") || low.Contains("voltage") || low.Contains("tensao") || low.Contains("vdd");
    }

    private static int RankGpuPowerMetric(string name)
    {
        var low = CsvTelemetryService.Fold(name);
        if (low.StartsWith("gpu consumo de energia") || low.StartsWith("gpu power") || low.Contains("gpu total power")) return 0;
        if (low.Contains("total board power") || low.Contains("tbp") || low.Contains("tgp")) return 1;
        if (low.Contains("8-pin") || low.Contains("entrada de energia gpu")) return 4;
        if (low.Contains("linhas gpu")) return 5;
        if (low.Contains("fonte pp")) return 20;
        if (low.Contains("nvvdd") || low.Contains("restante do chip") || low.Contains("system agent")) return 30;
        return 10;
    }

    private static int RankHighlightMetric(string name)
    {
        var low = CsvTelemetryService.Fold(name);
        if (low.Contains("potencia total do sistema") || low.Contains("system total power")) return 0;
        if (low.Contains("cpu package") || low.Contains("consumo de energia total da cpu")) return 1;
        if (low.Contains("gpu consumo de energia") || low.Contains("gpu total power")) return 2;
        if (low.Contains("framerate") || low.Contains("frame rate")) return 3;
        if (low.Contains("hotspot") || low.Contains("ponto quente")) return 4;
        return 10;
    }

    private static int RankGamingMetric(string name)
    {
        var low = CsvTelemetryService.Fold(name);
        if (CsvTelemetryService.IsFpsMetric(name)) return 0;
        if (IsGpuMetric(name) && CsvTelemetryService.IsPowerMetric(name)) return 1 + RankGpuPowerMetric(name);
        if (IsGpuMetric(name) && CsvTelemetryService.IsTemperatureMetric(name)) return 20;
        if (IsCpuMetric(name) && CsvTelemetryService.IsTemperatureMetric(name)) return 21;
        if (low.Contains("vram") || low.Contains("gpu memory") || low.Contains("memoria gpu")) return 30;
        if (low.Contains("ram") || low.Contains("memoria fisica") || low.Contains("physical memory")) return 31;
        if (IsGpuMetric(name) && (low.Contains("%") || low.Contains("load") || low.Contains("uso"))) return 40;
        if (IsCpuMetric(name) && (low.Contains("%") || low.Contains("load") || low.Contains("uso"))) return 41;
        if (IsMemoryTemperatureMetric(new MetricSummary(name, "", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))) return 50;
        return 90;
    }

    private int OverviewMetricLimit() => _detailLevel switch
    {
        "Essencial" => 8,
        "Completo" => 20,
        _ => 12
    };

    private int SectionMetricLimit() => _detailLevel switch
    {
        "Essencial" => 8,
        "Completo" => 28,
        _ => 16
    };

    private int DataMetricLimit() => _detailLevel switch
    {
        "Essencial" => 24,
        "Completo" => 96,
        _ => 42
    };

    private string LocalizeGroup(string group) => (_language, group) switch
    {
        ("en", "Potência") => "Power",
        ("en", "Temperatura") => "Temperature",
        ("en", "Carga") => "Load",
        ("en", "Frequencia") => "Clock",
        ("en", "Memoria") => "Memory",
        ("pt", "Frequencia") => "Frequência",
        ("pt", "Memoria") => "Memória",
        _ => group
    };

    private string TranslateChartType(string value) => (_language, value) switch
    {
        ("en", "Linha") => "Line",
        ("en", "Área") => "Area",
        ("en", "Dispersão") => "Scatter",
        ("en", "Barras") => "Bars",
        _ => value
    };

    private string TranslateDetailLevel(string value) => (_language, value) switch
    {
        ("en", "Essencial") => "Essential",
        ("en", "Normal") => "Normal",
        ("en", "Completo") => "Full",
        _ => value
    };

    private string DescribeMetric(string name)
    {
        if (CsvTelemetryService.IsFpsMetric(name))
        {
            return T("desc_fps");
        }
        if (CsvTelemetryService.IsTemperatureMetric(name))
        {
            return T("desc_temperature");
        }
        if (CsvTelemetryService.IsPowerMetric(name))
        {
            return T("desc_power");
        }

        var group = CsvTelemetryService.GroupFor(name);
        return group switch
        {
            "Carga" => T("desc_load"),
            "Frequencia" => T("desc_clock"),
            "Memoria" => T("desc_memory"),
            _ => T("desc_other")
        };
    }

    private string T(string key)
    {
        return _language == "en" ? key switch
        {
            "tagline" => "Native dashboard for HWiNFO logs",
            "csv_path" => "CSV path",
            "open" => "Open",
            "reload" => "Reload",
            "live_reload" => "Live reload",
            "report" => "Report",
            "gaming" => "Gaming",
            "power" => "Power",
            "temperatures" => "Temperatures",
            "frames" => "Frames",
            "compare" => "Compare",
            "data" => "Data",
            "custom_chart" => "Custom chart",
            "glossary" => "Glossary",
            "install" => "Installation",
            "language" => "Language",
            "temperature_unit" => "Temperature",
            "celsius" => "Celsius",
            "fahrenheit" => "Fahrenheit",
            "chart_type" => "Chart type",
            "detail_level" => "Detail level",
            "x_axis" => "X axis",
            "y_axis" => "Y axis",
            "x_axis_samples" => "last {0:N0} samples",
            "y_axis_unit" => "values in {0}",
            "search" => "Search",
            "search_placeholder" => "power, temperature, fps...",
            "f11_hint" => "F11 fullscreen",
            "choose_csv_short" => "Choose a CSV to start.",
            "ready" => "Ready",
            "choose_csv" => "Choose or paste a HWiNFO CSV first.",
            "loaded" => "Loaded",
            "samples" => "Samples",
            "sensors" => "Sensors",
            "empty_title" => "No report loaded",
            "empty_subtitle" => "Open a HWiNFO CSV or paste a readable path in the sidebar.",
            "empty_body" => "The native app now keeps the common Streamlit analysis flow: stats, focused views, custom charts, glossary, language, units, live reload, and fullscreen.",
            "overview" => "Overview",
            "overview_subtitle" => "Curated summary of the main metric families.",
            "gaming_subtitle" => "Real-time game-relevant signals: FPS, lows, power, thermals, RAM, VRAM, and utilization.",
            "quick_look_empty" => "No quick-look metric was detected in this report.",
            "fps_now" => "Current FPS",
            "fps_avg" => "Average FPS",
            "fps_1_low" => "1% low",
            "fps_01_low" => "0.1% low",
            "cpu_power_now" => "CPU power",
            "gpu_power_now" => "GPU power",
            "system_power_now" => "System power",
            "cpu_temp_now" => "CPU temp",
            "gpu_temp_now" => "GPU temp",
            "gpu_usage" => "GPU usage",
            "cpu_usage" => "CPU usage",
            "vram_usage" => "VRAM",
            "ram_usage" => "RAM",
            "memory_temp" => "Memory temp",
            "power_subtitle" => "Prioritized energy and power sensors.",
            "temperature_subtitle" => "Main component temperatures and hotspots.",
            "frames_subtitle" => "FPS statistics with valid range filtering.",
            "custom_chart_subtitle" => "Use search and chart type to explore any numeric sensor.",
            "compare_subtitle" => "Compare this report with another CSV using common sensors.",
            "compare_path" => "Second CSV path",
            "open_compare" => "Open second",
            "load_compare" => "Load compare",
            "compare_empty" => "Choose a second report to compare averages, P95 values, and deltas.",
            "current" => "Current",
            "glossary_subtitle" => "Meaning and grouping inferred for every numeric sensor.",
            "install_subtitle" => "Install, update, repair, or remove the native Windows app without losing the portable option.",
            "install_state_installed_running" => "Telemetry Lab is installed and this window is running from:",
            "install_state_installed" => "Telemetry Lab is installed at:",
            "install_state_portable" => "Telemetry Lab is running in portable mode. You can keep using it this way or install it for Start Menu, Explorer, Win+R, and Windows installed apps integration.",
            "install_help" => "The installer is per-user and uses the same files shipped in this package. Portable use remains supported: just run the executable from the extracted folder.",
            "install_action" => "Install",
            "update_action" => "Update",
            "repair_action" => "Repair",
            "uninstall_action" => "Uninstall",
            "done" => "Done",
            "running" => "Running",
            "install_process_error" => "Could not start the installer process.",
            "data_subtitle" => "Numeric columns and detected groups.",
            "no_metric" => "No compatible metric was detected.",
            "no_fps" => "No FPS metric was detected. HWiNFO may need RTSS, PresentMon, or another frame source.",
            "metric" => "Metric",
            "group" => "Group",
            "avg" => "Avg",
            "min" => "Min",
            "max" => "Max",
            "last" => "Last",
            "description" => "Description",
            "min_valid_fps" => "Min valid FPS",
            "max_valid_fps" => "Max valid FPS",
            "fps_correlation" => "Likely FPS relationships",
            "on" => "On",
            "off" => "Off",
            "no_path" => "No path",
            "not_found" => "Not found",
            "reading" => "Reading",
            "error" => "Error",
            "live_on" => "Watching for CSV changes.",
            "live_off" => "Stopped watching the CSV.",
            "live_waiting_path" => "Live reload is on, waiting for a readable CSV path.",
            "live_file_missing" => "Live reload is on, but the CSV was not found.",
            "live_watching_file" => "Watching {0} for changes.",
            "live_change_detected" => "Change detected. Reloading CSV...",
            "live_error" => "Live reload failed. Check the CSV and try again.",
            "desc_fps" => "Frame rate or frame source captured during the run.",
            "desc_temperature" => "Temperature sensor. Values are converted when Fahrenheit is selected.",
            "desc_power" => "Power or energy sensor. HWiNFO can expose physical, rail, and aggregate sensors with similar names.",
            "desc_load" => "Utilization or load sensor.",
            "desc_clock" => "Clock or frequency sensor.",
            "desc_memory" => "Memory or VRAM sensor.",
            "desc_other" => "Numeric HWiNFO sensor kept available for inspection.",
            _ => key
        } : key switch
        {
            "tagline" => "Dashboard nativo para logs HWiNFO",
            "csv_path" => "Caminho do CSV",
            "open" => "Abrir",
            "reload" => "Reler",
            "live_reload" => "Leitura dinâmica",
            "report" => "Relatório",
            "gaming" => "Jogos",
            "power" => "Potência",
            "temperatures" => "Temperaturas",
            "frames" => "Quadros",
            "compare" => "Comparar",
            "data" => "Dados",
            "custom_chart" => "Gráfico",
            "glossary" => "Glossário",
            "install" => "Instalação",
            "language" => "Idioma",
            "temperature_unit" => "Temperatura",
            "celsius" => "Celsius",
            "fahrenheit" => "Fahrenheit",
            "chart_type" => "Tipo de gráfico",
            "detail_level" => "Detalhe",
            "x_axis" => "Eixo X",
            "y_axis" => "Eixo Y",
            "x_axis_samples" => "últimas {0:N0} amostras",
            "y_axis_unit" => "valores em {0}",
            "search" => "Busca",
            "search_placeholder" => "potência, temperatura, fps...",
            "f11_hint" => "F11 tela cheia",
            "choose_csv_short" => "Escolha um CSV para iniciar.",
            "ready" => "Pronto",
            "choose_csv" => "Escolha ou cole um CSV do HWiNFO primeiro.",
            "loaded" => "Carregado",
            "samples" => "Amostras",
            "sensors" => "Sensores",
            "empty_title" => "Nenhum relatório carregado",
            "empty_subtitle" => "Abra um CSV do HWiNFO ou cole um caminho acessível no campo lateral.",
            "empty_body" => "O app nativo agora preserva o fluxo comum do Streamlit: estatísticas, visões focadas, gráfico customizado, glossário, idioma, unidades, live reload e tela cheia.",
            "overview" => "Visão geral",
            "overview_subtitle" => "Resumo curado das famílias principais.",
            "gaming_subtitle" => "Sinais relevantes para jogos em tempo real: FPS, lows, potência, temperaturas, RAM, VRAM e uso.",
            "quick_look_empty" => "Nenhuma métrica de leitura rápida foi detectada neste relatório.",
            "fps_now" => "FPS atual",
            "fps_avg" => "FPS médio",
            "fps_1_low" => "1% baixo",
            "fps_01_low" => "0.1% baixo",
            "cpu_power_now" => "Potência CPU",
            "gpu_power_now" => "Potência GPU",
            "system_power_now" => "Potência sistema",
            "cpu_temp_now" => "Temp. CPU",
            "gpu_temp_now" => "Temp. GPU",
            "gpu_usage" => "Uso GPU",
            "cpu_usage" => "Uso CPU",
            "vram_usage" => "VRAM",
            "ram_usage" => "RAM",
            "memory_temp" => "Temp. memórias",
            "power_subtitle" => "Sensores de energia e consumo priorizados.",
            "temperature_subtitle" => "Temperaturas principais e hotspots.",
            "frames_subtitle" => "Estatísticas de FPS com filtro de faixa válida.",
            "custom_chart_subtitle" => "Use busca e tipo de gráfico para explorar qualquer sensor numérico.",
            "compare_subtitle" => "Compare este relatório com outro CSV usando sensores em comum.",
            "compare_path" => "Caminho do segundo CSV",
            "open_compare" => "Abrir segundo",
            "load_compare" => "Carregar comparação",
            "compare_empty" => "Escolha um segundo relatório para comparar médias, P95 e deltas.",
            "current" => "Atual",
            "glossary_subtitle" => "Significado e agrupamento inferidos para cada sensor numérico.",
            "install_subtitle" => "Instale, atualize, repare ou remova o app nativo sem perder a opção portátil.",
            "install_state_installed_running" => "O Telemetry Lab está instalado e esta janela está rodando de:",
            "install_state_installed" => "O Telemetry Lab está instalado em:",
            "install_state_portable" => "O Telemetry Lab está rodando em modo portátil. Você pode continuar usando assim ou instalar para integrar ao Menu Iniciar, Explorer, Win+R e apps instalados do Windows.",
            "install_help" => "A instalação é por usuário e usa os mesmos arquivos enviados neste pacote. O modo portátil continua suportado: basta executar o .exe da pasta extraída.",
            "install_action" => "Instalar",
            "update_action" => "Atualizar",
            "repair_action" => "Reparar",
            "uninstall_action" => "Desinstalar",
            "done" => "Concluído",
            "running" => "Executando",
            "install_process_error" => "Não foi possível iniciar o processo de instalação.",
            "data_subtitle" => "Colunas numéricas e grupos detectados.",
            "no_metric" => "Nenhuma métrica compatível foi detectada.",
            "no_fps" => "Nenhuma métrica de FPS foi detectada. O HWiNFO pode precisar de RTSS, PresentMon ou outra fonte de quadros.",
            "metric" => "Métrica",
            "group" => "Grupo",
            "avg" => "Média",
            "min" => "Min",
            "max" => "Max",
            "last" => "Último",
            "description" => "Descrição",
            "min_valid_fps" => "FPS mínimo válido",
            "max_valid_fps" => "FPS máximo válido",
            "fps_correlation" => "Relações prováveis com FPS",
            "on" => "Ligado",
            "off" => "Desligado",
            "no_path" => "Sem caminho",
            "not_found" => "Não encontrado",
            "reading" => "Lendo",
            "error" => "Erro",
            "live_on" => "Observando alterações no CSV.",
            "live_off" => "Observação do CSV pausada.",
            "live_waiting_path" => "Leitura dinâmica ligada, aguardando um caminho de CSV válido.",
            "live_file_missing" => "Leitura dinâmica ligada, mas o CSV não foi encontrado.",
            "live_watching_file" => "Observando {0} por alterações.",
            "live_change_detected" => "Alteração detectada. Relendo CSV...",
            "live_error" => "Leitura dinâmica falhou. Confira o CSV e tente novamente.",
            "desc_fps" => "Taxa de quadros ou fonte de frames capturada durante o teste.",
            "desc_temperature" => "Sensor de temperatura. Os valores são convertidos ao selecionar Fahrenheit.",
            "desc_power" => "Sensor de potência ou energia. O HWiNFO pode expor sensores físicos, trilhos e agregados com nomes parecidos.",
            "desc_load" => "Sensor de carga ou utilização.",
            "desc_clock" => "Sensor de clock ou frequência.",
            "desc_memory" => "Sensor de memória ou VRAM.",
            "desc_other" => "Sensor numérico do HWiNFO mantido disponível para inspeção.",
            _ => key
        };
    }

    private static UIElement BuildSmallFooter(string glyph, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Padding = new Thickness(8, 4, 8, 4) };
        row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14, Opacity = 0.76 });
        row.Children.Add(new TextBlock { Text = text, FontSize = 12, Opacity = 0.78, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private UIElement BuildFramesSection()
    {
        var panel = BuildCardStack(T("frames"), T("frames_subtitle"));
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        controls.Children.Add(BuildNumberBox(T("min_valid_fps"), _fpsMinimum, value =>
        {
            _fpsMinimum = Math.Max(0, value);
            RenderMainSurface();
        }));
        controls.Children.Add(BuildNumberBox(T("max_valid_fps"), _fpsMaximum, value =>
        {
            _fpsMaximum = Math.Max(1, value);
            RenderMainSurface();
        }));
        panel.Children.Add(controls);

        var metrics = _report.Summaries
            .Where(metric => metric.Group == "FPS")
            .Select(metric => BuildFilteredSummary(metric.Name, _fpsMinimum, _fpsMaximum))
            .Where(metric => metric.Samples > 0)
            .ToList();

        if (metrics.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = T("no_fps"), Opacity = 0.72, TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            panel.Children.Add(BuildMetricTable(FilterMetrics(metrics)));
            panel.Children.Add(BuildChart(metrics[0].Name, _chartType));
            panel.Children.Add(BuildCorrelationTable(metrics[0].Name));
        }

        return BuildCard(panel);
    }

    private UIElement BuildCustomChartSection()
    {
        var panel = BuildCardStack(T("custom_chart"), T("custom_chart_subtitle"));
        var metrics = FilterMetrics(_report.Summaries).Take(4).ToList();
        if (metrics.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = T("no_metric"), Opacity = 0.72 });
        }
        else
        {
            panel.Children.Add(BuildMetricTable(metrics));
            foreach (var metric in metrics.Take(3))
            {
                panel.Children.Add(BuildChart(metric.Name, _chartType));
            }
        }

        return BuildCard(panel);
    }

    private UIElement BuildCompareSection()
    {
        var panel = BuildCardStack(T("compare"), T("compare_subtitle"));
        var input = new TextBox
        {
            Header = T("compare_path"),
            Text = _comparePath,
            PlaceholderText = T("csv_path"),
            CornerRadius = new CornerRadius(8),
            MinHeight = 34
        };
        input.TextChanged += (_, _) => _comparePath = input.Text;

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(BuildActionButton("\uE8E5", T("open_compare"), async (_, _) =>
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, _hwnd);
            picker.FileTypeFilter.Add(".csv");
            picker.FileTypeFilter.Add(".CSV");
            var file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                _comparePath = file.Path;
                await LoadComparePathAsync();
                RenderMainSurface();
            }
        }, primary: true));
        actions.Children.Add(BuildActionButton("\uE72C", T("load_compare"), async (_, _) =>
        {
            _comparePath = input.Text;
            await LoadComparePathAsync();
            RenderMainSurface();
        }));

        panel.Children.Add(input);
        panel.Children.Add(actions);

        if (_compareReport.RowCount == 0)
        {
            panel.Children.Add(new TextBlock { Text = T("compare_empty"), Opacity = 0.72, TextWrapping = TextWrapping.Wrap });
            return BuildCard(panel);
        }

        var commonNames = _report.Summaries.Select(metric => metric.Name).Intersect(_compareReport.Summaries.Select(metric => metric.Name)).ToHashSet();
        var current = FilterMetrics(_report.Summaries.Where(metric => commonNames.Contains(metric.Name))).Take(18).ToList();
        if (current.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = T("no_metric"), Opacity = 0.72 });
            return BuildCard(panel);
        }

        var rows = current.Select(metric =>
        {
            var other = _compareReport.Summaries.First(item => item.Name == metric.Name);
            var avg = DisplayValue(metric.Name, metric.Average);
            var otherAvg = DisplayValue(other.Name, other.Average);
            return new[]
            {
                metric.Name,
                LocalizeGroup(metric.Group),
                avg.ToString("N1"),
                otherAvg.ToString("N1"),
                (avg - otherAvg).ToString("+0.0;-0.0;0.0"),
                DisplayValue(metric.Name, metric.P95).ToString("N1"),
                DisplayValue(other.Name, other.P95).ToString("N1")
            };
        });
        panel.Children.Add(BuildSimpleTable([T("metric"), T("group"), T("current"), T("compare"), "Δ", "P95 A", "P95 B"], rows));
        panel.Children.Add(BuildChart(current[0].Name, _chartType));
        return BuildCard(panel);
    }

    private UIElement BuildGlossarySection()
    {
        var panel = BuildCardStack(T("glossary"), T("glossary_subtitle"));
        var rows = FilterMetrics(_report.Summaries).Take(64).Select(metric => new[]
        {
            metric.Name,
            LocalizeGroup(metric.Group),
            DescribeMetric(metric.Name),
            metric.Samples.ToString("N0")
        });
        panel.Children.Add(BuildSimpleTable([T("metric"), T("group"), T("description"), "N"], rows));
        return BuildCard(panel);
    }

    private UIElement BuildInstallSection()
    {
        var panel = BuildCardStack(T("install"), T("install_subtitle"));
        InstallStateText = new TextBlock
        {
            Text = GetInstallStateText(),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.84
        };
        panel.Children.Add(BuildInstallStateRow());

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        actions.Children.Add(BuildActionButton("\uE896", T("install_action"), async (_, _) =>
            await RunPackageScriptAsync("install.ps1", [ "-CreateDesktopShortcut" ], T("install_action")), primary: true));
        actions.Children.Add(BuildActionButton("\uE895", T("update_action"), async (_, _) =>
            await RunPackageScriptAsync("install.ps1", [ "-CreateDesktopShortcut" ], T("update_action"))));
        actions.Children.Add(BuildActionButton("\uE90F", T("repair_action"), async (_, _) =>
            await RunPackageScriptAsync("install.ps1", [ "-CreateDesktopShortcut" ], T("repair_action"))));
        actions.Children.Add(BuildActionButton("\uE74D", T("uninstall_action"), async (_, _) =>
            await RunPackageScriptAsync("uninstall.ps1", IsRunningFromInstallDir() ? [ "-StopRunning" ] : [], T("uninstall_action"))));
        panel.Children.Add(actions);

        panel.Children.Add(new TextBlock
        {
            Text = T("install_help"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.66
        });
        return BuildCard(panel);
    }

    private UIElement BuildInstallStateRow()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Padding = new Thickness(0, 2, 0, 4)
        };
        row.Children.Add(new FontIcon
        {
            Glyph = File.Exists(GetInstalledExePath()) ? "\uE930" : "\uE7B8",
            FontSize = 18,
            Foreground = AccentBrush()
        });
        row.Children.Add(InstallStateText);
        return row;
    }

    private string GetInstallStateText()
    {
        var installDir = GetInstallDir();
        if (File.Exists(GetInstalledExePath()) && IsRunningFromInstallDir())
        {
            return $"{T("install_state_installed_running")} {installDir}";
        }

        if (File.Exists(GetInstalledExePath()))
        {
            return $"{T("install_state_installed")} {installDir}";
        }

        return T("install_state_portable");
    }

    private async Task RunPackageScriptAsync(string scriptName, string[] arguments, string actionName)
    {
        var scriptPath = System.IO.Path.Combine(AppContext.BaseDirectory, scriptName);
        if (!File.Exists(scriptPath))
        {
            SetStatus(T("not_found"), scriptPath, StatusKind.Error);
            return;
        }

        try
        {
            SetStatus(actionName, T("running"), StatusKind.Info);
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                SetStatus(T("error"), T("install_process_error"), StatusKind.Error);
                return;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                SetStatus(T("error"), string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim(), StatusKind.Error);
                return;
            }

            if (InstallStateText is not null)
            {
                InstallStateText.Text = GetInstallStateText();
            }
            SetStatus(actionName, T("done"), StatusKind.Success);
        }
        catch (Exception ex)
        {
            App.LogCrash(ex);
            SetStatus(T("error"), ex.Message, StatusKind.Error);
        }
    }

    private static string GetInstallDir()
    {
        return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Telemetry Lab");
    }

    private static string GetInstalledExePath()
    {
        return System.IO.Path.Combine(GetInstallDir(), "TelemetryLab.WinUI.exe");
    }

    private static bool IsRunningFromInstallDir()
    {
        var currentDir = System.IO.Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\', '/');
        var installDir = System.IO.Path.GetFullPath(GetInstallDir()).TrimEnd('\\', '/');
        return string.Equals(currentDir, installDir, StringComparison.OrdinalIgnoreCase);
    }

    private UIElement BuildCorrelationTable(string fpsMetric)
    {
        if (!_report.Numeric.TryGetValue(fpsMetric, out var fpsSeries))
        {
            return new TextBlock();
        }

        var fps = fpsSeries.Select(value => value.HasValue ? value.Value : double.NaN).ToArray();
        var rows = _report.Summaries
            .Where(metric => metric.Name != fpsMetric && metric.Group is "Potência" or "Temperatura" or "Carga" or "Frequencia" or "Memoria")
            .Select(metric => new { metric, corr = Correlation(fps, _report.Numeric[metric.Name]) })
            .Where(item => !double.IsNaN(item.corr))
            .OrderByDescending(item => Math.Abs(item.corr))
            .Take(8)
            .Select(item => new[] { item.metric.Name, LocalizeGroup(item.metric.Group), item.corr.ToString("N2") });

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = T("fps_correlation"), FontSize = 14, FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 } });
        stack.Children.Add(BuildSimpleTable([T("metric"), T("group"), "r"], rows));
        return stack;
    }

    private UIElement BuildNumberBox(string header, double value, Action<double> onChanged)
    {
        var box = new TextBox
        {
            Header = header,
            Text = value.ToString("N0"),
            CornerRadius = new CornerRadius(8),
            Width = 180
        };
        void Apply()
        {
            if (double.TryParse(box.Text, out var parsed))
            {
                onChanged(parsed);
            }
        }

        box.LostFocus += (_, _) => Apply();
        box.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Enter)
            {
                Apply();
            }
        };
        return box;
    }

    private UIElement BuildSimpleTable(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var stack = new StackPanel { Spacing = 1 };
        stack.Children.Add(BuildGridRow(headers, true));
        foreach (var row in rows)
        {
            stack.Children.Add(BuildGridRow(row, false));
        }
        return stack;
    }

    private async void OpenCsv_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, _hwnd);
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".CSV");
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            PathBox.Text = file.Path;
            await LoadCurrentPathAsync();
        }
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        await LoadCurrentPathAsync();
    }

    private async Task LoadCurrentPathAsync()
    {
        if (_loading)
        {
            return;
        }

        var path = PathBox.Text.Trim();
        _currentPath = path;
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus(T("no_path"), T("choose_csv"), StatusKind.Error);
            return;
        }

        if (!File.Exists(path))
        {
            SetStatus(T("not_found"), path, StatusKind.Error);
            return;
        }

        try
        {
            _loading = true;
            SetStatus(T("reading"), System.IO.Path.GetFileName(path), StatusKind.Info);
            _report = await _service.LoadAsync(path);
            var info = new FileInfo(path);
            _lastLoadedWriteTicks = info.LastWriteTimeUtc.Ticks;
            _lastLoadedSize = info.Length;
            RenderMainSurface();
            SetStatus(T("loaded"), $"{_report.RowCount:N0} {T("samples").ToLowerInvariant()}.", StatusKind.Success);
            if (_liveReload)
            {
                SetLiveReloadState(string.Format(T("live_watching_file"), System.IO.Path.GetFileName(path)));
            }
        }
        catch (Exception ex)
        {
            App.LogCrash(ex);
            SetStatus(T("error"), ex.Message, StatusKind.Error);
            if (_liveReload)
            {
                SetLiveReloadState(T("live_error"));
            }
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadComparePathAsync()
    {
        var path = _comparePath.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            SetStatus(T("not_found"), path, StatusKind.Error);
            return;
        }

        try
        {
            SetStatus(T("reading"), System.IO.Path.GetFileName(path), StatusKind.Info);
            _compareReport = await _service.LoadAsync(path);
            SetStatus(T("loaded"), $"{_compareReport.RowCount:N0} {T("samples").ToLowerInvariant()}.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            App.LogCrash(ex);
            SetStatus(T("error"), ex.Message, StatusKind.Error);
        }
    }

    private void ConfigureKeyboard()
    {
        var accelerator = new KeyboardAccelerator { Key = VirtualKey.F11 };
        accelerator.Invoked += (_, args) =>
        {
            ToggleFullscreen();
            args.Handled = true;
        };
        RootGrid.KeyboardAccelerators.Add(accelerator);
    }

    private void ToggleFullscreen()
    {
        try
        {
            _fullscreen = !_fullscreen;
            AppWindow.SetPresenter(_fullscreen ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Default);
        }
        catch (Exception ex)
        {
            App.LogInfo("Fullscreen fallback: " + ex.Message);
        }
    }

    private void ConfigureLiveReload()
    {
        _liveReloadTimer.Tick += async (_, _) =>
        {
            if (!_liveReload)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentPath))
            {
                SetLiveReloadState(T("live_waiting_path"));
                return;
            }

            if (!File.Exists(_currentPath))
            {
                SetLiveReloadState(T("live_file_missing"));
                return;
            }

            if (_loading)
            {
                return;
            }

            var info = new FileInfo(_currentPath);
            SetLiveReloadState(string.Format(T("live_watching_file"), info.Name));
            if (info.LastWriteTimeUtc.Ticks != _lastLoadedWriteTicks || info.Length != _lastLoadedSize)
            {
                SetLiveReloadState(T("live_change_detected"));
                await LoadCurrentPathAsync();
            }
        };
    }

    private void UpdateLiveReloadTimer()
    {
        if (_liveReload)
        {
            _currentPath = PathBox.Text;
            _liveReloadTimer.Start();
            var state = GetLiveReloadState();
            SetLiveReloadState(state);
            SetStatus(T("live_reload"), state, StatusKind.Info);
        }
        else
        {
            _liveReloadTimer.Stop();
            SetLiveReloadState(T("live_off"));
            SetStatus(T("live_reload"), T("live_off"), StatusKind.Info);
        }
    }

    private string GetLiveReloadState()
    {
        if (!_liveReload)
        {
            return T("live_off");
        }

        var path = string.IsNullOrWhiteSpace(_currentPath) ? PathBox?.Text?.Trim() ?? string.Empty : _currentPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return T("live_waiting_path");
        }

        if (!File.Exists(path))
        {
            return T("live_file_missing");
        }

        return string.Format(T("live_watching_file"), System.IO.Path.GetFileName(path));
    }

    private void SetLiveReloadState(string message)
    {
        _liveReloadState = message;
        if (LiveReloadStateText is not null)
        {
            LiveReloadStateText.Text = string.IsNullOrWhiteSpace(_liveReloadState) ? GetLiveReloadState() : _liveReloadState;
        }
    }

    private void RebuildSidebar()
    {
        _currentPath = PathBox.Text;
        var newSidebar = BuildSidebar();
        Sidebar.Children.Clear();
        while (newSidebar.Children.Count > 0)
        {
            var child = newSidebar.Children[0];
            newSidebar.Children.Remove(child);
            Sidebar.Children.Add(child);
        }
    }

    private void InitializeWindowChrome()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        try
        {
            AppWindow.Title = "Telemetry Lab";
            AppWindow.Resize(new SizeInt32(1180, 820));
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                AppWindow.TitleBar.ButtonHoverBackgroundColor = IsLightTheme
                    ? Color.FromArgb(0x20, 0x00, 0x00, 0x00)
                    : Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF);
                AppWindow.TitleBar.ButtonPressedBackgroundColor = IsLightTheme
                    ? Color.FromArgb(0x30, 0x00, 0x00, 0x00)
                    : Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF);
            }
        }
        catch (Exception ex)
        {
            App.LogInfo("Window chrome fallback: " + ex.Message);
        }
    }

    private void TryApplyBackdrop()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("TELEMETRY_LAB_WINUI_BACKDROP"), "0", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }
        catch
        {
            try
            {
                SystemBackdrop = new MicaBackdrop();
            }
            catch
            {
            }
        }
    }

    private void SetStatus(string title, string message, StatusKind kind)
    {
        if (StatusText is null || StatusIcon is null || StatusPanel is null)
        {
            return;
        }

        StatusText.Text = $"{title}: {message}";
        StatusIcon.Glyph = kind switch
        {
            StatusKind.Success => "\uE73E",
            StatusKind.Error => "\uE783",
            _ => "\uE895"
        };
        StatusIcon.Foreground = new SolidColorBrush(kind switch
        {
            StatusKind.Success => Color.FromArgb(0xFF, 0x0E, 0x7A, 0x0D),
            StatusKind.Error => Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C),
            _ => AccentColor()
        });
        StatusPanel.Background = new SolidColorBrush(kind switch
        {
            StatusKind.Success => Color.FromArgb(0x24, 0x10, 0x7C, 0x10),
            StatusKind.Error => Color.FromArgb(0x24, 0xC4, 0x2B, 0x1C),
            _ => WithAlpha(AccentColor(), 0x2A)
        });
    }

    private static SolidColorBrush ResolvePageBackground()
    {
        return new SolidColorBrush(IsLightTheme
            ? Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(0x18, 0x00, 0x00, 0x00));
    }

    private static SolidColorBrush SubtleBorderBrush()
    {
        return new SolidColorBrush(IsLightTheme
            ? Color.FromArgb(0x42, 0xC8, 0xBE, 0xB4)
            : Color.FromArgb(0x44, 0x78, 0x78, 0x78));
    }

    private static SolidColorBrush TransparentBrush() => new(Color.FromArgb(0x00, 0x00, 0x00, 0x00));

    private static SolidColorBrush AccentBrush() => new(AccentColor());

    private static SolidColorBrush AccentLayerBrush(byte alpha) => new(WithAlpha(AccentColor(), alpha));

    private static SolidColorBrush LayerBrush(byte alpha)
    {
        return new SolidColorBrush(IsLightTheme
            ? Color.FromArgb(alpha, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(alpha, 0x00, 0x00, 0x00));
    }

    private static Color AccentColor()
    {
        try
        {
            return new UISettings().GetColorValue(UIColorType.Accent);
        }
        catch
        {
            return Color.FromArgb(0xFF, 0x00, 0x78, 0xD4);
        }
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static bool IsLightTheme
    {
        get
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return !Equals(key?.GetValue("AppsUseLightTheme"), 0);
            }
            catch
            {
                return true;
            }
        }
    }

    private enum StatusKind
    {
        Info,
        Success,
        Error
    }
}
