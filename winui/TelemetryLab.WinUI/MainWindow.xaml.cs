// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gabriel Ferreira
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TelemetryLab.WinUI.Telemetry;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

namespace TelemetryLab.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly CsvTelemetryService _service = new();
    private readonly IntPtr _hwnd;
    private TelemetryReport _report = TelemetryReport.Empty;
    private string _section = "Relatório";
    private string _currentPath = string.Empty;

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
        SetStatus("Pronto", "Escolha um CSV do HWiNFO para começar.", StatusKind.Info);
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
        Grid.SetRow(Sidebar, 1);
        RootGrid.Children.Add(Sidebar);

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
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x78, 0xD4)),
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
                ? Color.FromArgb(0x72, 0xF4, 0xEE, 0xE6)
                : Color.FromArgb(0x78, 0x20, 0x20, 0x20))
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
            Text = "Dashboard nativo para logs HWiNFO",
            FontSize = 12,
            Opacity = 0.68,
            Margin = new Thickness(4, -10, 0, 2)
        });

        PathBox = new TextBox
        {
            PlaceholderText = "Caminho do CSV",
            CornerRadius = new CornerRadius(8),
            MinHeight = 36,
            Text = _currentPath
        };
        side.Children.Add(PathBox);

        var openActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        openActions.Children.Add(BuildActionButton("\uE8E5", "Abrir", OpenCsv_Click, primary: true));
        openActions.Children.Add(BuildActionButton("\uE72C", "Reler", Reload_Click));
        side.Children.Add(openActions);

        side.Children.Add(BuildNavItem("\uE9D2", "Relatório"));
        side.Children.Add(BuildNavItem("\uE945", "Potência"));
        side.Children.Add(BuildNavItem("\uE9CA", "Temperaturas"));
        side.Children.Add(BuildNavItem("\uE7C1", "Quadros"));
        side.Children.Add(BuildNavItem("\uE8A7", "Dados"));

        side.Children.Add(new Border { Height = 1, Background = SubtleBorderBrush(), Margin = new Thickness(0, 8, 0, 0) });
        side.Children.Add(BuildSmallFooter("\uE946", "GPL-3.0-or-later"));
        side.Children.Add(BuildSmallFooter("\uE713", "WinUI 3 prototype"));
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
            Text = string.IsNullOrWhiteSpace(_report.Source) ? "Escolha um CSV para iniciar." : _report.Source,
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
                MainSurface.Children.Add(BuildMetricSection("Potência", "Sensores de energia e consumo priorizados."));
                break;
            case "Temperaturas":
                MainSurface.Children.Add(BuildMetricSection("Temperatura", "Temperaturas principais e hotspots."));
                break;
            case "Quadros":
                MainSurface.Children.Add(BuildMetricSection("FPS", "Taxa de quadros detectada no log."));
                break;
            case "Dados":
                MainSurface.Children.Add(BuildDataSection());
                break;
            default:
                MainSurface.Children.Add(BuildOverviewSection());
                break;
        }

        SetStatus("Carregado", $"{_report.RowCount:N0} amostras, {_report.SensorCount:N0} sensores.", StatusKind.Success);
    }

    private UIElement BuildEmptyState()
    {
        var panel = BuildCardStack("Nenhum relatório carregado", "Abra um CSV do HWiNFO ou cole um caminho acessível no campo lateral.");
        panel.Children.Add(new TextBlock
        {
            Text = "O app nativo ainda está em fase de shell funcional. A leitura de CSV, resumo e gráficos básicos já ficam fora do Streamlit.",
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
            BuildKpiCard("Amostras", _report.RowCount.ToString("N0"), "\uE8EF"),
            BuildKpiCard("Sensores", _report.SensorCount.ToString("N0"), "\uE8A5"),
            BuildKpiCard("Potência", _report.Summaries.Count(m => m.Group == "Potência").ToString("N0"), "\uE945"),
            BuildKpiCard("Temperatura", _report.Summaries.Count(m => m.Group == "Temperatura").ToString("N0"), "\uE9CA")
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
        var panel = BuildCardStack("Visao geral", "Resumo curado das familias principais.");
        panel.Children.Add(BuildMetricTable(_report.Summaries.Take(10)));
        var first = _report.Summaries.FirstOrDefault();
        if (first is not null)
        {
            panel.Children.Add(BuildChart(first.Name));
        }

        return BuildCard(panel);
    }

    private UIElement BuildMetricSection(string group, string subtitle)
    {
        var metrics = _service.CuratedMetrics(_report, group, limit: 10);
        var panel = BuildCardStack(group, subtitle);
        if (metrics.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "Nenhuma metrica compativel foi detectada.", Opacity = 0.72 });
        }
        else
        {
            panel.Children.Add(BuildMetricTable(metrics));
            panel.Children.Add(BuildChart(metrics[0].Name));
        }

        return BuildCard(panel);
    }

    private UIElement BuildDataSection()
    {
        var panel = BuildCardStack("Dados", "Colunas numericas e grupos detectados.");
        panel.Children.Add(BuildMetricTable(_report.Summaries.Take(28)));
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

    private static UIElement BuildMetricHeader()
    {
        return BuildGridRow(["Metrica", "Grupo", "Media", "P95", "Max"], true);
    }

    private static UIElement BuildMetricRow(MetricSummary metric)
    {
        return BuildGridRow([
            metric.Name,
            metric.Group,
            metric.Average.ToString("N1"),
            metric.P95.ToString("N1"),
            metric.Maximum.ToString("N1")
        ], false);
    }

    private static UIElement BuildGridRow(IReadOnlyList<string> values, bool header)
    {
        var row = new Grid
        {
            MinHeight = header ? 34 : 38,
            Padding = new Thickness(8, 4, 8, 4),
            ColumnSpacing = 10,
            Background = header ? new SolidColorBrush(Color.FromArgb(0x16, 0x80, 0x80, 0x80)) : TransparentBrush(),
            CornerRadius = new CornerRadius(header ? 6 : 0)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.7, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });

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

    private UIElement BuildChart(string metricName)
    {
        var values = _report.Numeric.TryGetValue(metricName, out var series)
            ? series.Where(v => v.HasValue).Select(v => v!.Value).TakeLast(400).ToArray()
            : Array.Empty<double>();
        var canvas = new Canvas
        {
            Height = 220,
            MinWidth = 600,
            Background = new SolidColorBrush(IsLightTheme ? Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x38, 0x10, 0x10, 0x10))
        };
        canvas.Loaded += (_, _) => DrawChart(canvas, values);
        canvas.SizeChanged += (_, _) => DrawChart(canvas, values);

        var label = new TextBlock
        {
            Text = metricName,
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

    private static void DrawChart(Canvas canvas, IReadOnlyList<double> values)
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
        var points = new PointCollection();
        for (var i = 0; i < values.Count; i++)
        {
            var x = values.Count == 1 ? 0 : i * width / (values.Count - 1);
            var y = height - ((values[i] - min) / span * (height - 18)) - 9;
            points.Add(new Windows.Foundation.Point(x, y));
        }

        canvas.Children.Add(new Polyline
        {
            Points = points,
            StrokeThickness = 2.4,
            Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x78, 0xD4))
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
                ? Color.FromArgb(0xC4, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0xC0, 0x2C, 0x2C, 0x2C)),
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

    private UIElement BuildNavItem(string glyph, string text)
    {
        var selected = string.Equals(_section, text, StringComparison.OrdinalIgnoreCase);
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 0, 10, 0),
            MinHeight = 38,
            CornerRadius = new CornerRadius(6),
            Background = selected
                ? new SolidColorBrush(IsLightTheme ? Color.FromArgb(0x84, 0xEA, 0xE3, 0xDC) : Color.FromArgb(0x72, 0x3B, 0x3B, 0x3B))
                : TransparentBrush(),
            BorderBrush = TransparentBrush(),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new FontIcon { Glyph = glyph, FontSize = 16, Foreground = selected ? new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x78, 0xD4)) : null },
                    new TextBlock { Text = text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        button.Click += (_, _) =>
        {
            _currentPath = PathBox.Text;
            _section = text;
            var newSidebar = BuildSidebar();
            Sidebar.Children.Clear();
            while (newSidebar.Children.Count > 0)
            {
                var child = newSidebar.Children[0];
                newSidebar.Children.Remove(child);
                Sidebar.Children.Add(child);
            }
            RenderMainSurface();
        };
        return button;
    }

    private static UIElement BuildSmallFooter(string glyph, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Padding = new Thickness(8, 4, 8, 4) };
        row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14, Opacity = 0.76 });
        row.Children.Add(new TextBlock { Text = text, FontSize = 12, Opacity = 0.78, VerticalAlignment = VerticalAlignment.Center });
        return row;
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
        var path = PathBox.Text.Trim();
        _currentPath = path;
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Sem caminho", "Escolha ou cole um CSV primeiro.", StatusKind.Error);
            return;
        }

        if (!File.Exists(path))
        {
            SetStatus("Não encontrado", path, StatusKind.Error);
            return;
        }

        try
        {
            SetStatus("Lendo", System.IO.Path.GetFileName(path), StatusKind.Info);
            _report = await _service.LoadAsync(path);
            RenderMainSurface();
        }
        catch (Exception ex)
        {
            App.LogCrash(ex);
            SetStatus("Erro", ex.Message, StatusKind.Error);
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
            SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            try
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
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
            _ => Color.FromArgb(0xFF, 0x00, 0x78, 0xD4)
        });
        StatusPanel.Background = new SolidColorBrush(kind switch
        {
            StatusKind.Success => Color.FromArgb(0x24, 0x10, 0x7C, 0x10),
            StatusKind.Error => Color.FromArgb(0x24, 0xC4, 0x2B, 0x1C),
            _ => Color.FromArgb(0x22, 0x00, 0x78, 0xD4)
        });
    }

    private static SolidColorBrush ResolvePageBackground()
    {
        return new SolidColorBrush(IsLightTheme
            ? Color.FromArgb(0xE8, 0xF8, 0xF4, 0xEF)
            : Color.FromArgb(0xE8, 0x20, 0x20, 0x20));
    }

    private static SolidColorBrush SubtleBorderBrush()
    {
        return new SolidColorBrush(IsLightTheme
            ? Color.FromArgb(0x42, 0xC8, 0xBE, 0xB4)
            : Color.FromArgb(0x44, 0x78, 0x78, 0x78));
    }

    private static SolidColorBrush TransparentBrush() => new(Color.FromArgb(0x00, 0x00, 0x00, 0x00));

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
