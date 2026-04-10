using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WaysToSnooze.Zolak.Pages;

/// <summary>
/// States management page: configuration table + animated state cards.
/// Single scrollable area for the entire page.
/// </summary>
public partial class StatesPage : Page
{
    private readonly PetFSM _fsm;
    private readonly ZolakConfig _config;
    private readonly List<DispatcherTimer> _cardTimers = new();
    private readonly string[] _hoverOptions = { "None", "On Mouse Enter", "After Mouse Leave", "On Inactivity" };

    public StatesPage(PetFSM fsm, ZolakConfig config)
    {
        _fsm = fsm;
        _config = config;
        InitializeComponent();
        Loaded += (_, _) => BuildUI();
        Unloaded += (_, _) => StopAllTimers();
    }

    private void BuildUI()
    {
        BuildTable();
        BuildCards();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CONFIGURATION TABLE
    // ═══════════════════════════════════════════════════════════════════

    private void BuildTable()
    {
        TableRowsPanel.Children.Clear();
        var states = _config.States.OrderBy(s => s.Order).ToList();

        for (int i = 0; i < states.Count; i++)
        {
            var sc = states[i];
            bool isAlt = i % 2 == 1;

            // Only disable durations for internally-controlled states (Angry=Enter, Bored=Inactivity)
            bool durationDisabled = sc.HoverTrigger is "Enter" or "Inactivity";

            var row = new Border
            {
                Background = isAlt
                    ? (Brush)FindResource("TableRowAlt")
                    : Brushes.Transparent,
                Padding = new Thickness(12, 5, 12, 5),
                BorderBrush = (Brush)FindResource("BorderColor"),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });

            // ── Column 0: State Name ──
            var nameBlock = new TextBlock
            {
                Text = sc.Name,
                Foreground = (Brush)FindResource("TextPrimary"),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.Medium
            };
            Grid.SetColumn(nameBlock, 0);
            grid.Children.Add(nameBlock);

            // ── Column 1: Chance % ──
            if (!sc.IsExtraordinary)
            {
                var chanceBox = new TextBox
                {
                    Text = (sc.Weight * 100).ToString("F0"),
                    Width = 44,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Style = (Style)FindResource("VsTextBox"),
                    Tag = sc,
                    FontSize = 11,
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                chanceBox.LostFocus += ChanceBox_LostFocus;
                Grid.SetColumn(chanceBox, 1);
                grid.Children.Add(chanceBox);
            }
            else
            {
                var dashLabel = new TextBlock
                {
                    Text = "—",
                    Foreground = (Brush)FindResource("DisabledForeground"),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12
                };
                Grid.SetColumn(dashLabel, 1);
                grid.Children.Add(dashLabel);
            }

            // ── Column 2: Min Duration (Slider + TextBox) ──
            var minPanel = CreateSliderInput(sc.MinDuration, 0.5, 30.0, !durationDisabled,
                val => { sc.MinDuration = val; SaveConfig(); });
            Grid.SetColumn(minPanel, 2);
            grid.Children.Add(minPanel);

            // ── Column 3: Max Duration (Slider + TextBox) ──
            var maxPanel = CreateSliderInput(sc.MaxDuration, 0.5, 99.0, !durationDisabled,
                val => { sc.MaxDuration = val; SaveConfig(); });
            Grid.SetColumn(maxPanel, 3);
            grid.Children.Add(maxPanel);

            // ── Column 4: Hover Effect ──
            var hoverCombo = new ComboBox
            {
                Width = 115,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 11,
                Style = (Style)FindResource("VsComboBox"),
                Tag = sc
            };
            foreach (var opt in _hoverOptions)
                hoverCombo.Items.Add(opt);

            hoverCombo.SelectedItem = sc.HoverTrigger switch
            {
                "Enter" => "On Mouse Enter",
                "Leave" => "After Mouse Leave",
                "Inactivity" => "On Inactivity",
                _ => "None"
            };
            hoverCombo.SelectionChanged += HoverCombo_SelectionChanged;
            Grid.SetColumn(hoverCombo, 4);
            grid.Children.Add(hoverCombo);

            // ── Column 5: Edit Pencil (ALL states get pencil) ──
            var editBtn = new Button
            {
                Style = (Style)FindResource("VsGhostButton"),
                ToolTip = "Edit frames",
                Tag = sc,
                Padding = new Thickness(4),
                VerticalAlignment = VerticalAlignment.Center
            };
            var pencilPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M20.71,7.04C21.1,6.65 21.1,6 20.71,5.63L18.37,3.29C18,2.9 17.35,2.9 16.96,3.29L15.12,5.12L18.87,8.87M3,17.25V21H6.75L17.81,9.93L14.06,6.18L3,17.25Z"),
                Width = 13, Height = 13,
                Stretch = Stretch.Uniform,
                Fill = (Brush)FindResource("TextSecondary")
            };
            editBtn.Content = pencilPath;
            editBtn.Click += EditBtn_Click;
            Grid.SetColumn(editBtn, 5);
            grid.Children.Add(editBtn);

            row.Child = grid;
            TableRowsPanel.Children.Add(row);
        }
    }

    private StackPanel CreateSliderInput(double value, double min, double max, bool enabled, Action<double> onChange)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var slider = new Slider
        {
            Width = 70,
            Minimum = min,
            Maximum = max,
            Value = value,
            IsEnabled = enabled,
            Style = (Style)FindResource("VsSlider"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var textBox = new TextBox
        {
            Text = value.ToString("F1"),
            Width = 38,
            IsEnabled = enabled,
            Style = (Style)FindResource("VsTextBox"),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            FontSize = 10
        };

        slider.ValueChanged += (_, e) =>
        {
            textBox.Text = e.NewValue.ToString("F1");
            onChange(e.NewValue);
        };

        textBox.LostFocus += (_, _) =>
        {
            if (double.TryParse(textBox.Text, out double parsed))
            {
                parsed = Math.Clamp(parsed, min, max);
                slider.Value = parsed;
                textBox.Text = parsed.ToString("F1");
                onChange(parsed);
            }
            else
            {
                textBox.Text = slider.Value.ToString("F1");
            }
        };

        panel.Children.Add(slider);
        panel.Children.Add(textBox);
        return panel;
    }

    private void ChanceBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is StateConfig sc)
        {
            if (double.TryParse(tb.Text, out double pct))
            {
                sc.Weight = Math.Clamp(pct, 0, 100) / 100.0;
                tb.Text = (sc.Weight * 100).ToString("F0");
                SaveConfig();
            }
            else
            {
                tb.Text = (sc.Weight * 100).ToString("F0");
            }
        }
    }

    private void HoverCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.Tag is StateConfig sc)
        {
            string? selected = cb.SelectedItem as string;
            string? trigger = selected switch
            {
                "On Mouse Enter" => "Enter",
                "After Mouse Leave" => "Leave",
                "On Inactivity" => "Inactivity",
                _ => null
            };

            if (trigger != null)
            {
                foreach (var other in _config.States)
                {
                    if (other != sc && other.HoverTrigger == trigger)
                        other.HoverTrigger = null;
                }
            }

            sc.HoverTrigger = trigger;
            SaveConfig();
            BuildTable();
        }
    }

    private void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is StateConfig sc)
        {
            var cpWindow = Window.GetWindow(this) as ControlPanelWindow;
            cpWindow?.NavigateToPage(new StateEditorPage(_fsm, _config, sc.Name));
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  STATE CARDS
    // ═══════════════════════════════════════════════════════════════════

    private void BuildCards()
    {
        CardsPanel.Children.Clear();
        StopAllTimers();

        var states = _config.States.OrderBy(s => s.Order).ToList();

        foreach (var sc in states)
        {
            var frames = AssetManager.Instance.GetFrames(sc.Name);
            if (frames.Count == 0) continue;

            var card = new Border
            {
                Style = (Style)FindResource("StateCard"),
                Width = 120,
                Height = 150,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var cardStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var spriteImage = new Image
            {
                Width = 56,
                Height = 56,
                Source = frames[0],
                RenderTransformOrigin = new Point(0.5, 0.5),
                Margin = new Thickness(0, 0, 0, 8)
            };

            int frameIndex = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            timer.Tick += (_, _) =>
            {
                frameIndex = (frameIndex + 1) % frames.Count;
                spriteImage.Source = frames[frameIndex];
            };
            timer.Start();
            _cardTimers.Add(timer);

            var nameLabel = new TextBlock
            {
                Text = sc.Name,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };

            string badgeText = sc.IsExtraordinary
                ? (sc.HoverTrigger ?? "Special")
                : $"{sc.Weight * 100:F0}%";
            var badge = new Border
            {
                Background = sc.IsExtraordinary
                    ? (Brush)FindResource("WarningColor")
                    : (Brush)FindResource("AccentColor"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 9,
                Foreground = (Brush)FindResource("TextOnAccent")
            };

            var scaleTransform = new ScaleTransform(1, 1);
            card.RenderTransform = scaleTransform;
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            card.MouseEnter += (_, _) =>
            {
                var anim = new DoubleAnimation(1.05, TimeSpan.FromMilliseconds(150))
                { EasingFunction = new QuadraticEase() };
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };
            card.MouseLeave += (_, _) =>
            {
                var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150))
                { EasingFunction = new QuadraticEase() };
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };

            cardStack.Children.Add(spriteImage);
            cardStack.Children.Add(nameLabel);
            cardStack.Children.Add(badge);
            card.Child = cardStack;
            CardsPanel.Children.Add(card);
        }
    }

    private void StopAllTimers()
    {
        foreach (var t in _cardTimers)
            t.Stop();
        _cardTimers.Clear();
    }

    private void SaveConfig()
    {
        ConfigManager.Save(_config);
        _fsm.LoadConfig(_config);
    }
}
