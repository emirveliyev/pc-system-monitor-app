using pc_system_monitor_app.Controls;
using pc_system_monitor_app.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls;

namespace pc_system_monitor_app
{
    public partial class PinnedWindow : Window
    {
        private readonly HardwareReader reader;
        private readonly DispatcherTimer timer;
        private readonly List<string> sensors;

        private const int MaxPins = 3;
        private readonly TimeSpan hintResetDelay = TimeSpan.FromSeconds(1.8);

        public PinnedWindow(HardwareReader hwReader, List<string> sensorsToPin)
        {
            InitializeComponent();

            reader = hwReader ?? throw new ArgumentNullException(nameof(hwReader));
            sensors = sensorsToPin ?? new List<string>();

            BuildControlsFromList();

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += Timer_Tick;
            timer.Start();

            MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
        }

        private void BuildControlsFromList()
        {
            PinsHost.Children.Clear();

            foreach (var s in sensors)
            {
                AddPinnedControlInternal(s, animate: false);
            }

            UpdateHintVisibility();
        }

        private void UpdateHintVisibility()
        {
            if (PinsHost.Children.Count == 0)
            {
                PinsHost.Visibility = Visibility.Collapsed;
                EmptyHintPanel.Visibility = Visibility.Visible;
            }
            else
            {
                PinsHost.Visibility = Visibility.Visible;
                EmptyHintPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void AddPinnedControl(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            if (sensors.Contains(s)) return;

            if (sensors.Count >= MaxPins)
            {
                ShowTemporaryHint($"Максимум {MaxPins} датчика");
                return;
            }

            sensors.Add(s);
            AddPinnedControlInternal(s, animate: true);
            UpdateHintVisibility();
        }

        private void AddPinnedControlInternal(string s, bool animate)
        {
            if (PinsHost.Children.OfType<CircularProgress>().Any(c => (c.Tag as string) == s)) return;

            var cp = new CircularProgress
            {
                Width = 96,
                Height = 96,
                Maximum = 100,
                Tag = s,
                Margin = new Thickness(6),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            cp.SetAvgText(string.Empty);

            switch (s.ToUpperInvariant())
            {
                case "CPU":
                    cp.Label = "CPU";
                    cp.AccentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 138, 0));
                    break;
                case "RAM":
                    cp.Label = "RAM";
                    cp.AccentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 81, 181));
                    break;
                case "GPU":
                    cp.Label = "GPU";
                    cp.AccentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
                    break;
                default:
                    cp.Label = s;
                    cp.AccentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120));
                    break;
            }

            cp.ToolTip = $"{cp.Label} — правый клик, чтобы открепить";

            cp.MouseRightButtonUp += (sender, e) =>
            {
                RemovePinnedControl((sender as CircularProgress)?.Tag as string);
            };

            cp.MouseDoubleClick += (sender, e) =>
            {
                var el = sender as CircularProgress;
                if (el == null) return;
                var anim = new DoubleAnimation(1.0, 0.85, TimeSpan.FromMilliseconds(120)) { AutoReverse = true };
                el.BeginAnimation(OpacityProperty, anim);
            };

            PinsHost.Children.Add(cp);

            if (animate)
            {
                var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220));
                cp.BeginAnimation(OpacityProperty, fade);
            }
        }

        private void RemovePinnedControl(string? tag)
        {
            if (string.IsNullOrEmpty(tag)) return;

            var control = PinsHost.Children.OfType<CircularProgress>().FirstOrDefault(c => (c.Tag as string) == tag);
            if (control != null)
            {
                var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180));
                fade.Completed += (s, e) =>
                {
                    PinsHost.Children.Remove(control);
                    sensors.Remove(tag);
                    UpdateHintVisibility();
                };
                control.BeginAnimation(OpacityProperty, fade);
            }
        }

        private void Window_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.StringFormat))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                DropHint.Text = "Отпустите, чтобы закрепить";
                OuterBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255));
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        private void Window_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            DropHint.Text = "Перетащите сюда датчик (CPU / RAM / GPU)";
            OuterBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
        }

        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            OuterBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;

            try
            {
                if (e.Data.GetDataPresent(System.Windows.DataFormats.StringFormat))
                {
                    var key = e.Data.GetData(System.Windows.DataFormats.StringFormat) as string;
                    if (!string.IsNullOrEmpty(key))
                    {
                        AddPinnedControl(key);
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                ResetHintAfterDelay();
            }
        }

        private async void ResetHintAfterDelay()
        {
            await System.Threading.Tasks.Task.Delay(hintResetDelay);
            Dispatcher.Invoke(() =>
            {
                if (PinsHost.Children.Count == 0)
                {
                    DropHint.Text = "Перетащите сюда датчик (CPU / RAM / GPU)";
                }
            });
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var cps = PinsHost.Children.OfType<CircularProgress>().ToList();
                for (int i = 0; i < cps.Count; i++)
                {
                    var label = (cps[i].Tag as string) ?? cps[i].Label ?? "";
                    if (label == "CPU")
                    {
                        var v = reader.GetCpuLoad() ?? 0;
                        cps[i].Value = v;
                    }
                    else if (label == "RAM")
                    {
                        var r = reader.GetRamInfo();
                        cps[i].Value = r.usedPercent ?? 0;
                    }
                    else if (label == "GPU")
                    {
                        var g = reader.GetGpuDetails();
                        cps[i].Value = g.LoadPercent ?? 0;
                    }
                }
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            try { timer?.Stop(); } catch { }
            base.OnClosed(e);
        }

        private void ShowTemporaryHint(string text)
        {
            DropHint.Text = text;

            var anim = new ColorAnimation
            {
                From = System.Windows.Media.Color.FromArgb(220, 255, 255, 255),
                To = System.Windows.Media.Colors.Transparent,
                Duration = TimeSpan.FromMilliseconds(700)
            };

            var brush = new SolidColorBrush(System.Windows.Media.Colors.Transparent);
            OuterBorder.BorderBrush = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);

            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(1100);
                Dispatcher.Invoke(() =>
                {
                    if (PinsHost.Children.Count == 0)
                        DropHint.Text = "Перетащите сюда датчик (CPU / RAM / GPU)";
                    else
                        DropHint.Text = string.Empty;
                });
            });
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                Close();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
