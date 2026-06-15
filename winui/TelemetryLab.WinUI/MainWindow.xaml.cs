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
    private string _metricSearch = string.Empty;
    private double _fpsMinimum = 30;
    private double _fpsMaximum = 1000;
    private long _lastLoadedWriteTicks;
    private long _lastLoadedSize;
    private bool _liveReload;
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

        side.Children.Add(BuildNavItem("\uE9D2", "Relatório", T("report")));
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

    private UIElement BuildOverviewSection()
    {
        var panel = BuildCardStack(T("overview"), T("overview_subtitle"));
        var metrics = FilterMetrics(_report.Summaries).Take(12).ToList();
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
        var metrics = FilterMetrics(_service.CuratedMetrics(_report, group, limit: 16)).ToList();
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

    private UIElement BuildDataSection()
    {
        var panel = BuildCardStack(T("data"), T("data_subtitle"));
        panel.Children.Add(BuildMetricTable(FilterMetrics(_report.Summaries).Take(42)));
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
        var canvas = new Canvas
        {
            Height = 220,
            MinWidth = 600,
            Background = LayerBrush(IsLightTheme ? (byte)0x50 : (byte)0x34)
        };
        canvas.Loaded += (_, _) => DrawChart(canvas, values, chartType);
        canvas.SizeChanged += (_, _) => DrawChart(canvas, values, chartType);

        var label = new TextBlock
        {
            Text = $"{metricName} · {TranslateChartType(chartType)}",
            FontSize = 12,
            Opacity = 0.68,
            Margin = new Thickness(0, 8, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(label);
        stack.Children.Add(new Border { CornerRadius = new CornerRadius(8), Child = canvas, Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, 2000, 220) } });
        return stack;
    }

    private static void DrawChart(Canvas canvas, IReadOnlyList<double> values, string chartType)
    {
        canvas.Children.Clear();
        if (values.Count < 2 || canvas.ActualWidth <= 0)
        {
            return;
        }

        var width = canvas.ActualWidth;
        var height = canvas.Height;
        var min = values.Min();
        var max = values.Max();
        var span = Math.Max(0.0001, max - min);

        if (chartType == "Heatmap")
        {
            var cellWidth = Math.Max(2, width / values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                var intensity = (values[i] - min) / span;
                var rect = new Rectangle
                {
                    Width = Math.Ceiling(cellWidth),
                    Height = height,
                    Fill = new SolidColorBrush(WithAlpha(AccentColor(), (byte)(0x28 + intensity * 0xB8)))
                };
                Canvas.SetLeft(rect, i * cellWidth);
                Canvas.SetTop(rect, 0);
                canvas.Children.Add(rect);
            }

            return;
        }

        if (chartType == "Barras")
        {
            var barWidth = Math.Max(1, width / values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                var normalized = (values[i] - min) / span;
                var barHeight = Math.Max(1, normalized * (height - 18));
                var rect = new Rectangle
                {
                    Width = Math.Max(1, barWidth - 1),
                    Height = barHeight,
                    Fill = AccentLayerBrush(0xB0)
                };
                Canvas.SetLeft(rect, i * barWidth);
                Canvas.SetTop(rect, height - barHeight - 6);
                canvas.Children.Add(rect);
            }

            return;
        }

        var points = new PointCollection();
        for (var i = 0; i < values.Count; i++)
        {
            var x = values.Count == 1 ? 0 : i * width / (values.Count - 1);
            var y = height - ((values[i] - min) / span * (height - 18)) - 9;
            points.Add(new Windows.Foundation.Point(x, y));
        }

        if (chartType == "Área")
        {
            var area = new Polygon
            {
                Fill = AccentLayerBrush(0x4C),
                StrokeThickness = 0
            };
            area.Points.Add(new Windows.Foundation.Point(0, height));
            foreach (var point in points)
            {
                area.Points.Add(point);
            }
            area.Points.Add(new Windows.Foundation.Point(width, height));
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

    private double DisplayValue(string metricName, double value)
    {
        return _temperatureUnit == "F" && CsvTelemetryService.IsTemperatureMetric(metricName)
            ? value * 9 / 5 + 32
            : value;
    }

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
        }
        catch (Exception ex)
        {
            App.LogCrash(ex);
            SetStatus(T("error"), ex.Message, StatusKind.Error);
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
            if (!_liveReload || string.IsNullOrWhiteSpace(_currentPath) || !File.Exists(_currentPath) || _loading)
            {
                return;
            }

            var info = new FileInfo(_currentPath);
            if (info.LastWriteTimeUtc.Ticks != _lastLoadedWriteTicks || info.Length != _lastLoadedSize)
            {
                await LoadCurrentPathAsync();
            }
        };
    }

    private void UpdateLiveReloadTimer()
    {
        if (_liveReload)
        {
            _liveReloadTimer.Start();
            SetStatus(T("live_reload"), T("live_on"), StatusKind.Info);
        }
        else
        {
            _liveReloadTimer.Stop();
            SetStatus(T("live_reload"), T("live_off"), StatusKind.Info);
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
