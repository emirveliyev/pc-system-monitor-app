using pc_system_monitor_app.Controls;
using pc_system_monitor_app.Services;
using pc_system_monitor_app.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace pc_system_monitor_app
{
    public partial class MainWindow : Window
    {
        private HardwareReader? reader;
        private DispatcherTimer? timer;
        private Queue<double> cpuHistory = new Queue<double>();
        private Queue<double> ramHistory = new Queue<double>();
        private Queue<double> gpuHistory = new Queue<double>();
        private bool gotData = false;
        private WinForms.NotifyIcon? notifyIcon;
        private PinnedWindow? pinnedWindow;

        private System.Windows.Point dragStartPoint;
        private bool isMouseDownOnCircle = false;
        private CircularProgress? activeDragCircle = null;

        private bool isDragging = false;
        private DragPreviewWindow? dragPreview;
        private DispatcherTimer? dragPreviewTimer;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                reader = new HardwareReader();
                SetupNotifyIcon();
                InitCircles();
                AttachDragHandlers();
                timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += Timer_Tick;
                timer.Start();
                StatusText.Text = "Инициализация датчиков...";
                StatusLine.Text = "Ожидание данных";
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex, "Ошибка в конструкторе MainWindow");
                System.Windows.MessageBox.Show($"Ошибка при старте. Лог: {Logger.GetLogPath()}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void SetupNotifyIcon()
        {
            notifyIcon = new WinForms.NotifyIcon();
            notifyIcon.Icon = Drawing.SystemIcons.Application;
            notifyIcon.Text = "Мониторинг Систем";
            notifyIcon.Visible = false;
            notifyIcon.Click += NotifyIcon_Click;
            var menu = new WinForms.ContextMenuStrip();

            var showItem = new WinForms.ToolStripMenuItem("Показать");
            showItem.Click += (s, e) => ShowFromTray();

            var bgItem = new WinForms.ToolStripMenuItem("Фоновый режим");
            bgItem.CheckOnClick = true;
            bgItem.CheckedChanged += (s, e) =>
            {
                var item = s as WinForms.ToolStripMenuItem;
                if (item != null && item.Checked)
                {
                    notifyIcon!.Visible = true;
                    Hide();
                }
            };

            var exitItem = new WinForms.ToolStripMenuItem("Выход");
            exitItem.Click += (s, e) => CloseApp();

            menu.Items.Add(showItem);
            menu.Items.Add(bgItem);
            menu.Items.Add(exitItem);
            notifyIcon.ContextMenuStrip = menu;
        }

        private void InitCircles()
        {
            CpuCircle.Label = "CPU";
            CpuCircle.SetAvgText("AVG: —");
            CpuCircle.Value = 0;
            CpuCircle.AccentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 138, 0));
            RamCircle.Label = "RAM";
            RamCircle.SetAvgText("AVG: —");
            RamCircle.Value = 0;
            RamCircle.AccentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 81, 181));
            GpuCircle.Label = "GPU";
            GpuCircle.SetAvgText("AVG: —");
            GpuCircle.Value = 0;
            GpuCircle.AccentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
        }

        private void AttachDragHandlers()
        {
            CpuCircle.PreviewMouseLeftButtonDown += Circle_PreviewMouseLeftButtonDown;
            RamCircle.PreviewMouseLeftButtonDown += Circle_PreviewMouseLeftButtonDown;
            GpuCircle.PreviewMouseLeftButtonDown += Circle_PreviewMouseLeftButtonDown;

            CpuCircle.PreviewMouseMove += Circle_PreviewMouseMove;
            RamCircle.PreviewMouseMove += Circle_PreviewMouseMove;
            GpuCircle.PreviewMouseMove += Circle_PreviewMouseMove;

            CpuCircle.PreviewMouseLeftButtonUp += Circle_PreviewMouseLeftButtonUp;
            RamCircle.PreviewMouseLeftButtonUp += Circle_PreviewMouseLeftButtonUp;
            GpuCircle.PreviewMouseLeftButtonUp += Circle_PreviewMouseLeftButtonUp;
        }

        private void Circle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            activeDragCircle = sender as CircularProgress;
            if (activeDragCircle == null) return;
            isMouseDownOnCircle = true;
            dragStartPoint = e.GetPosition(this);
            Keyboard.Focus(this);
            e.Handled = true;
        }

        private void Circle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isMouseDownOnCircle = false;
            activeDragCircle = null;
            if (isDragging)
            {
                StopDragPreview();
            }
        }

        private void Circle_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!isMouseDownOnCircle || activeDragCircle == null || isDragging) return;
            var current = e.GetPosition(this);
            var diff = (current - dragStartPoint);
            if (Math.Abs(diff.X) > 6 || Math.Abs(diff.Y) > 6)
            {
                StartDrag(activeDragCircle);
                isMouseDownOnCircle = false;
                activeDragCircle = null;
            }
        }

        private void StartDrag(CircularProgress circle)
        {
            try
            {
                isDragging = true;
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Hand;
                var key = (circle.Tag as string) ?? circle.Label ?? "SENSOR";
                dragPreview = new DragPreviewWindow(key, circle.Value, circle.AccentBrush);
                dragPreview.Show();

                dragPreviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                dragPreviewTimer.Tick += DragPreviewTimer_Tick;
                dragPreviewTimer.Start();

                EnsurePinnedWindowShownNearMouse();

                var dataObj = new System.Windows.DataObject(System.Windows.DataFormats.StringFormat, key);
                try
                {
                    System.Windows.DragDrop.DoDragDrop(circle, dataObj, System.Windows.DragDropEffects.Copy);
                }
                catch { }

                StopDragPreview();
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex, "StartDrag failed");
                StopDragPreview();
            }
        }

        private void DragPreviewTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var mp = WinForms.Control.MousePosition;
                if (dragPreview != null)
                {
                    dragPreview.Left = mp.X + 12;
                    dragPreview.Top = mp.Y + 12;
                }
            }
            catch { }
        }

        private void StopDragPreview()
        {
            try { dragPreviewTimer?.Stop(); } catch { }
            try { dragPreview?.Close(); } catch { }
            dragPreviewTimer = null;
            dragPreview = null;
            isDragging = false;
            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        private void EnsurePinnedWindowShownNearMouse()
        {
            try
            {
                if (pinnedWindow == null || !pinnedWindow.IsVisible)
                {
                    pinnedWindow = new PinnedWindow(reader!, new List<string>());
                    var mp = WinForms.Control.MousePosition;
                    pinnedWindow.Left = mp.X - (pinnedWindow.Width / 2);
                    pinnedWindow.Top = mp.Y - (pinnedWindow.Height / 2);
                    pinnedWindow.Show();
                }
                else
                {
                    pinnedWindow.Activate();
                }
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex, "Не удалось показать окно закреплений");
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var cpuLoad = reader!.GetCpuLoad() ?? 0;
                var cpuInfo = reader!.GetCpuDetails();
                var ram = reader!.GetRamInfo();
                var gpu = reader!.GetGpuDetails();
                Push(cpuHistory, cpuLoad, 30);
                Push(ramHistory, ram.usedPercent ?? 0, 30);
                Push(gpuHistory, gpu.LoadPercent ?? 0, 30);
                double cpuAvg = cpuHistory.Count > 0 ? Math.Round(cpuHistory.Average(), 1) : 0;
                double ramAvg = ramHistory.Count > 0 ? Math.Round(ramHistory.Average(), 1) : 0;
                double gpuAvg = gpuHistory.Count > 0 ? Math.Round(gpuHistory.Average(), 1) : 0;
                CpuCircle.Value = cpuLoad;
                CpuCircle.SetAvgText($"AVG: {cpuAvg}%");
                RamCircle.Value = ram.usedPercent ?? 0;
                RamCircle.SetAvgText($"AVG: {ramAvg}%");
                GpuCircle.Value = gpu.LoadPercent ?? 0;
                GpuCircle.SetAvgText($"AVG: {gpuAvg}%");
                CpuInfoText.Text = $"Процессор: {cpuInfo.Name}";
                CpuSpecText.Text = $"Ядра: {cpuInfo.Cores}  Потоки: {cpuInfo.Threads}  Частота: {cpuInfo.MaxClockMHz} MHz";
                RamInfoText.Text = $"ОЗУ: {(ram.usedPercent.HasValue ? $"{ram.usedPercent:F1}%" : "нет данных")}";
                RamSpecText.Text = $"Всего: {ram.totalMB:F0} MB  Свободно: {ram.freeMB:F0} MB";
                GpuInfoText.Text = $"GPU: {gpu.Name ?? "—"}";
                GpuSpecText.Text = $"Память: {gpu.AdapterRamMB:F0} MB  Драйвер: {gpu.DriverVersion ?? "—"}";
                AvgText.Text = $"CPU {cpuAvg}%   RAM {ramAvg}%   GPU {gpuAvg}%";
                if (!gotData && (cpuLoad > 0 || (ram.usedPercent ?? 0) > 0 || (gpu.LoadPercent ?? 0) > 0))
                {
                    gotData = true;
                    StatusText.Text = "Данные получены";
                    StatusLine.Text = "Мониторинг работает";
                }
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex, "Ошибка в Timer_Tick");
                try { timer?.Stop(); } catch { }
                StatusText.Text = "Ошибка сбора данных";
                StatusLine.Text = "Смотрите лог";
                System.Windows.MessageBox.Show($"Ошибка при сборе данных. Лог: {Logger.GetLogPath()}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Push(Queue<double> q, double v, int max)
        {
            q.Enqueue(v);
            while (q.Count > max) q.Dequeue();
        }

        private void BtnTray_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                notifyIcon!.Visible = true;
                Hide();
                notifyIcon!.ShowBalloonTip(1000, "Мониторинг Систем", "Программа свернута в трей", WinForms.ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex, "Не удалось свернуть в трей");
            }
        }

        private void BtnShowPinned_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (pinnedWindow == null || !pinnedWindow.IsVisible)
                {
                    pinnedWindow = new PinnedWindow(reader!, new List<string>());
                    pinnedWindow.Show();
                }
                else
                {
                    pinnedWindow.Activate();
                }
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex, "Не удалось открыть окно закреплений");
            }
        }

        private void NotifyIcon_Click(object? sender, EventArgs e)
        {
            ShowFromTray();
        }

        private void ShowFromTray()
        {
            try
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                if (notifyIcon != null) notifyIcon.Visible = false;
            }
            catch { }
        }

        private void CloseApp()
        {
            try { pinnedWindow?.Close(); } catch { }
            try { notifyIcon?.Dispose(); } catch { }
            try { reader?.Dispose(); } catch { }
            System.Windows.Application.Current.Shutdown();
        }

        private void Author_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex, "Не удалось открыть ссылку автора");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try { dragPreviewTimer?.Stop(); } catch { }
            try { dragPreview?.Close(); } catch { }
            try { notifyIcon?.Dispose(); } catch { }
            try { pinnedWindow?.Close(); } catch { }
            try { reader?.Dispose(); } catch { }
            base.OnClosed(e);
        }

        private class DragPreviewWindow : Window
        {
            public DragPreviewWindow(string label, double value, System.Windows.Media.Brush? accent)
            {
                AllowsTransparency = true;
                WindowStyle = WindowStyle.None;
                ShowInTaskbar = false;
                Topmost = true;
                Width = 160;
                Height = 64;
                Background = System.Windows.Media.Brushes.Transparent;
                var border = new System.Windows.Controls.Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 18, 18, 20)),
                    Padding = new Thickness(10),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 3, Opacity = 0.45 }
                };
                var sp = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var ellipse = new System.Windows.Shapes.Ellipse
                {
                    Width = 48,
                    Height = 48,
                    Fill = accent ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var txtPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical, Margin = new Thickness(10, 0, 0, 0) };
                var t1 = new System.Windows.Controls.TextBlock { Text = label, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White };
                var t2 = new System.Windows.Controls.TextBlock { Text = $"{value:F0}%", Opacity = 0.95, FontSize = 12, Foreground = System.Windows.Media.Brushes.White };
                txtPanel.Children.Add(t1);
                txtPanel.Children.Add(t2);
                sp.Children.Add(ellipse);
                sp.Children.Add(txtPanel);
                border.Child = sp;
                Content = border;
            }
        }
    }
}
