using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfGroupBox = System.Windows.Controls.GroupBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace InternetSpeedMonitor
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private NotifyIcon? _notifyIcon;
        private DispatcherTimer? _timer;
        private readonly Dictionary<string, long> _previousBytesReceived = new();
        private readonly Dictionary<string, long> _previousBytesSent = new();
        private DateTime _lastUpdate = DateTime.Now;
        private OverlayWindow? _overlay;
		private UserSettings _settings = SettingsService.Load();
		private WpfComboBox? _textColorCombo;
		private WpfComboBox? _bgColorCombo;
		private WpfComboBox? _positionCombo;
		private WpfComboBox? _fontFamilyCombo;
		private WpfComboBox? _fontSizeCombo;
		private WpfComboBox? _fontVariantCombo;
        private InternetSpeedMonitor.Services.INetworkSpeedService _speedService = new InternetSpeedMonitor.Services.NetworkInterfaceSpeedService();

        // Properties for data binding
        private string _downloadSpeed = "0 KB/s";
        private string _uploadSpeed = "0 KB/s";
        private string _selectedUnit = "kB/s";
        private string _connectionInfo = "No connection detected";
        private string _adapterInfo = "Detecting adapters...";
        private bool _autoUnitSwitching = true;
		private bool _overlayEnabled = true;

        public string DownloadSpeed
        {
            get => _downloadSpeed;
            set
            {
                _downloadSpeed = value;
                OnPropertyChanged(nameof(DownloadSpeed));
            }
        }

        public string UploadSpeed
        {
            get => _uploadSpeed;
            set
            {
                _uploadSpeed = value;
                OnPropertyChanged(nameof(UploadSpeed));
            }
        }

        public string SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                _selectedUnit = value;
                OnPropertyChanged(nameof(SelectedUnit));
            }
        }

        public string ConnectionInfo
        {
            get => _connectionInfo;
            set
            {
                _connectionInfo = value;
                OnPropertyChanged(nameof(ConnectionInfo));
            }
        }

        public string AdapterInfo
        {
            get => _adapterInfo;
            set
            {
                _adapterInfo = value;
                OnPropertyChanged(nameof(AdapterInfo));
            }
        }

        public List<string> AvailableUnits { get; } = new() { "B/s", "kB/s", "MB/s", "Mb/s" };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            InitializeSystemTray();
            InitializeTimer();
            UpdateConnectionInfo();

			// Launch overlay (default from settings)
			_overlayEnabled = _settings.OverlayEnabled;
			_overlay = new OverlayWindow();
			ApplyOverlaySettingsToWindow();
			if (_overlayEnabled)
			{
				_overlay.Show();
			}
        }

        private void InitializeComponent()
        {
            // Configure primary window
            ConfigureWindowShell();

            // Main grid with header/content/footer rows
            var mainGrid = CreateMainGrid();

            // Header
            var headerPanel = BuildHeader();
            Grid.SetRow(headerPanel, 0);
            mainGrid.Children.Add(headerPanel);

            // Scrollable content area
            var contentPanel = BuildContent();
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0),
                Content = contentPanel
            };
            Grid.SetRow(scrollViewer, 1);
            mainGrid.Children.Add(scrollViewer);

            // Footer buttons
            var footer = BuildFooterButtons();
            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            Content = mainGrid;
        }

        private void ConfigureWindowShell()
        {
            Title = "Internet Speed Monitor";
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var resourcesIconPath = System.IO.Path.Combine(baseDir, "Resources", "icon.ico");
                var iconPath = resourcesIconPath;
                if (System.IO.File.Exists(iconPath))
                {
                    using var fs = System.IO.File.OpenRead(iconPath);
                    var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(fs, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                    Icon = decoder.Frames[0];
                }
                else
                {
                    Icon = CreateAppIcon();
                }
            }
            catch { }
        }

        private Grid CreateMainGrid()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            return mainGrid;
        }

        private StackPanel BuildHeader()
        {
            var headerPanel = new StackPanel
            {
                Orientation = WpfOrientation.Horizontal,
                Margin = new Thickness(10),
                HorizontalAlignment = WpfHorizontalAlignment.Center,
            };
            headerPanel.Children.Add(new TextBlock
            {
                Text = "Internet Speed Monitor",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.DarkBlue),
            });
            return headerPanel;
        }

        private StackPanel BuildContent()
        {
            var contentPanel = new StackPanel
            {
                Margin = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Top,
            };
            contentPanel.Children.Add(BuildSpeedGroup());
            contentPanel.Children.Add(BuildUnitGroup());
            contentPanel.Children.Add(BuildOverlayGroup());
            contentPanel.Children.Add(BuildConnectionGroup());
            contentPanel.Children.Add(BuildAdaptersGroup());
            return contentPanel;
        }

        private WpfGroupBox BuildSpeedGroup()
        {
            var group = new WpfGroupBox { Header = "Current Speed", Margin = new Thickness(0, 10, 0, 10), Padding = new Thickness(15) };
            var stack = new StackPanel();

            var downloadPanel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            downloadPanel.Children.Add(new TextBlock { Text = "Download: ", FontWeight = FontWeights.SemiBold, Width = 80 });
            var downloadText = new TextBlock { FontSize = 16, Foreground = new SolidColorBrush(Colors.Green) };
            downloadText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DownloadSpeed"));
            downloadPanel.Children.Add(downloadText);
            stack.Children.Add(downloadPanel);

            var uploadPanel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            uploadPanel.Children.Add(new TextBlock { Text = "Upload: ", FontWeight = FontWeights.SemiBold, Width = 80 });
            var uploadText = new TextBlock { FontSize = 16, Foreground = new SolidColorBrush(Colors.Blue) };
            uploadText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("UploadSpeed"));
            uploadPanel.Children.Add(uploadText);
            stack.Children.Add(uploadPanel);

            group.Content = stack;
            return group;
        }

        private WpfGroupBox BuildUnitGroup()
        {
            var group = new WpfGroupBox { Header = "Display Unit", Margin = new Thickness(0, 10, 0, 10), Padding = new Thickness(15) };
            var stack = new StackPanel();
            var unitCombo = new WpfComboBox { ItemsSource = AvailableUnits, SelectedItem = SelectedUnit, Width = 100, HorizontalAlignment = WpfHorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10) };
            unitCombo.SelectionChanged += UnitCombo_SelectionChanged;
            stack.Children.Add(unitCombo);
            var autoSwitchCheckBox = new WpfCheckBox { Content = "Auto-switch units based on speed", IsChecked = _autoUnitSwitching, Margin = new Thickness(0, 5, 0, 0) };
            autoSwitchCheckBox.Checked += (s, e) => _autoUnitSwitching = true;
            autoSwitchCheckBox.Unchecked += (s, e) => _autoUnitSwitching = false;
            stack.Children.Add(autoSwitchCheckBox);
            group.Content = stack;
            return group;
        }

        private WpfGroupBox BuildOverlayGroup()
        {
            var group = new WpfGroupBox { Header = "Overlay", Margin = new Thickness(0, 10, 0, 10), Padding = new Thickness(15) };
            var stack = new StackPanel();

            // Toggle overlay
            var overlayCheckbox = new WpfCheckBox { Content = "Always-on-top display", IsChecked = _settings.OverlayEnabled, Margin = new Thickness(0, 0, 0, 0) };
            overlayCheckbox.Checked += (s, e) => { _overlayEnabled = true; if (_overlay == null) { _overlay = new OverlayWindow(); _overlay.Show(); } else { _overlay.Topmost = true; _overlay.Show(); } _settings.OverlayEnabled = true; SettingsService.Save(_settings); };
            overlayCheckbox.Unchecked += (s, e) => { _overlayEnabled = false; if (_overlay != null) { _overlay.Hide(); } _settings.OverlayEnabled = false; SettingsService.Save(_settings); };
            stack.Children.Add(overlayCheckbox);

            // Text color
            var textColorPanel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            textColorPanel.Children.Add(new TextBlock { Text = "Text color:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            _textColorCombo = new WpfComboBox { Width = 140, ItemsSource = new List<string> { "White", "Black", "Red", "Green", "Blue", "Yellow", "Cyan", "Magenta" } };
            _textColorCombo.SelectionChanged += (s, e) => { if (_overlay == null) return; var selected = _textColorCombo.SelectedItem as string; if (string.IsNullOrWhiteSpace(selected)) return; _overlay.SetTextColor(ParseColor(selected)); _settings.TextColor = selected; SettingsService.Save(_settings); };
            _textColorCombo.SelectedItem = _settings.TextColor;
            textColorPanel.Children.Add(_textColorCombo);
            stack.Children.Add(textColorPanel);

            // Background
            var bgColorPanel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            bgColorPanel.Children.Add(new TextBlock { Text = "Background:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var bgOptions = new List<string> { "No Background", "DarkGray", "Black", "White", "Red", "Green", "Blue", "Yellow", "Cyan", "Magenta" };
            _bgColorCombo = new WpfComboBox { Width = 160, ItemsSource = bgOptions };
            _bgColorCombo.SelectionChanged += (s, e) => { if (_overlay == null) return; var selected = _bgColorCombo.SelectedItem as string; if (string.IsNullOrWhiteSpace(selected)) return; if (selected == "No Background") { _overlay.SetTransparentBackground(true); } else { _overlay.SetTransparentBackground(false); _overlay.SetBackgroundColor(ParseColor(selected)); } _settings.BackgroundOption = selected; SettingsService.Save(_settings); };
            _bgColorCombo.SelectedItem = _settings.BackgroundOption;
            bgColorPanel.Children.Add(_bgColorCombo);
            stack.Children.Add(bgColorPanel);

            // Position
            var positionPanel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            positionPanel.Children.Add(new TextBlock { Text = "Position:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var positions = new List<string> { "Top Left", "Top Center", "Top Right", "Bottom Left", "Bottom Center", "Bottom Right" };
            _positionCombo = new WpfComboBox { Width = 160, ItemsSource = positions };
            _positionCombo.SelectionChanged += (s, e) => { if (_overlay == null) return; var selected = _positionCombo.SelectedItem as string; if (string.IsNullOrWhiteSpace(selected)) return; _overlay.SetPosition(selected); _settings.Position = selected; SettingsService.Save(_settings); };
            _positionCombo.SelectedItem = _settings.Position;
            positionPanel.Children.Add(_positionCombo);
            stack.Children.Add(positionPanel);

            // Font family
            var fontFamilyPanel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            fontFamilyPanel.Children.Add(new TextBlock { Text = "Font:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var installedFamilies = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(n => n).ToList();
            _fontFamilyCombo = new WpfComboBox { Width = 200, ItemsSource = installedFamilies };
            _fontFamilyCombo.SelectionChanged += (s, e) => { if (_overlay == null) return; var selected = _fontFamilyCombo.SelectedItem as string; if (string.IsNullOrWhiteSpace(selected)) return; _overlay.SetFontFamily(new System.Windows.Media.FontFamily(selected)); _settings.FontFamily = selected; SettingsService.Save(_settings); };
            _fontFamilyCombo.SelectedItem = _settings.FontFamily;
            fontFamilyPanel.Children.Add(_fontFamilyCombo);
            stack.Children.Add(fontFamilyPanel);

            // Font size
            var fontSizePanel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            fontSizePanel.Children.Add(new TextBlock { Text = "Size:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var sizes = new List<double> { 10, 12, 14, 16, 18, 20, 22 };
            _fontSizeCombo = new WpfComboBox { Width = 100, ItemsSource = sizes };
            _fontSizeCombo.SelectionChanged += (s, e) => { if (_overlay == null) return; if (_fontSizeCombo.SelectedItem is double size) { _overlay.SetFontSize(size); _settings.FontSize = size; SettingsService.Save(_settings); } };
            _fontSizeCombo.SelectedItem = _settings.FontSize;
            fontSizePanel.Children.Add(_fontSizeCombo);
            stack.Children.Add(fontSizePanel);

            // Font variant
            var fontVariantPanel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            fontVariantPanel.Children.Add(new TextBlock { Text = "Style:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var variants = new List<string> { "Regular", "Bold", "Italic", "Bold Italic", "Sharp" };
            _fontVariantCombo = new WpfComboBox { Width = 140, ItemsSource = variants };
            _fontVariantCombo.SelectionChanged += (s, e) => { if (_overlay == null) return; var selected = _fontVariantCombo.SelectedItem as string; if (string.IsNullOrWhiteSpace(selected)) return; _overlay.SetFontVariant(selected); _settings.FontVariant = selected; SettingsService.Save(_settings); };
            _fontVariantCombo.SelectedItem = _settings.FontVariant;
            fontVariantPanel.Children.Add(_fontVariantCombo);
            stack.Children.Add(fontVariantPanel);

            group.Content = stack;
            return group;
        }

        private WpfGroupBox BuildConnectionGroup()
        {
            var group = new WpfGroupBox { Header = "Connection Information", Margin = new Thickness(0, 10, 0, 10), Padding = new Thickness(15) };
            var stack = new StackPanel();
            var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
            text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("ConnectionInfo"));
            stack.Children.Add(text);
            group.Content = stack;
            return group;
        }

        private WpfGroupBox BuildAdaptersGroup()
        {
            var group = new WpfGroupBox { Header = "Active Network Adapters", Margin = new Thickness(0, 10, 0, 10), Padding = new Thickness(15) };
            var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
            text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("AdapterInfo"));
            group.Content = text;
            return group;
        }

		private StackPanel BuildFooterButtons()
        {
            var panel = new StackPanel { Orientation = WpfOrientation.Horizontal, HorizontalAlignment = WpfHorizontalAlignment.Center, Margin = new Thickness(10) };
            var minimizeButton = new WpfButton { Content = "Minimize to Tray", Width = 120, Height = 30, Margin = new Thickness(5), Background = new SolidColorBrush(Colors.LightBlue) };
            minimizeButton.Click += MinimizeToTray_Click;
            panel.Children.Add(minimizeButton);
			var githubButton = new WpfButton { Content = "Dev Github", Width = 110, Height = 30, Margin = new Thickness(5), Background = new SolidColorBrush(Colors.Green), Foreground = new SolidColorBrush(Colors.White) };
			var keepHoverStyle = new Style(typeof(WpfButton));
			var hoverTrigger = new System.Windows.Trigger { Property = IsMouseOverProperty, Value = true };
			hoverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, new SolidColorBrush(Colors.White)));
			hoverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(Colors.Green)));
			keepHoverStyle.Triggers.Add(hoverTrigger);
			githubButton.Style = keepHoverStyle;
			githubButton.Click += (s, e) => { try { Process.Start(new ProcessStartInfo("https://github.com/iamxeeshankhan") { UseShellExecute = true }); } catch { } };
            panel.Children.Add(githubButton);
            var exitButton = new WpfButton { Content = "Exit", Width = 80, Height = 30, Margin = new Thickness(5), Background = new SolidColorBrush(Colors.LightCoral) };
            exitButton.Click += Exit_Click;
            panel.Children.Add(exitButton);
            return panel;
        }

        private System.Windows.Media.ImageSource? CreateAppIcon()
        {
            try
            {
                // Create a simple icon programmatically
                var drawingGroup = new DrawingGroup();
                var geometryDrawing = new GeometryDrawing();
                geometryDrawing.Brush = new SolidColorBrush(Colors.Blue);
                geometryDrawing.Geometry = new RectangleGeometry(new Rect(0, 0, 16, 16));
                drawingGroup.Children.Add(geometryDrawing);
                return new DrawingImage(drawingGroup);
            }
            catch
            {
                return null;
            }
        }

        private void InitializeSystemTray()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Visible = false, // <-- when application is open, don't show in tray
                Text = "↓0 kB/s ↑0 kB/s",
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Show", null, (s, e) => ShowWindow());
            contextMenu.Items.Add("-"); // Separator
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());
            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private Icon LoadTrayIcon()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var resourcesIconPath = System.IO.Path.Combine(baseDir, "Resources", "icon.ico");
                var iconPath = resourcesIconPath;
                if (System.IO.File.Exists(iconPath))
                {
                    return new Icon(iconPath, 16, 16);
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        private Icon CreateSpeedTrayIcon(long bytesDownPerSec, long bytesUpPerSec)
        {
            // Render speeds onto a 16x16 icon with transparent background and larger text
            string shortDown =
                bytesDownPerSec >= 1_000_000
                    ? $"{bytesDownPerSec / 1_000_000}M"
                    : $"{Math.Max(1, bytesDownPerSec / 1_000)}K";
            string shortUp =
                bytesUpPerSec >= 1_000_000
                    ? $"{bytesUpPerSec / 1_000_000}M"
                    : $"{Math.Max(1, bytesUpPerSec / 1_000)}K";

            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                using var shadowBrush = new System.Drawing.SolidBrush(
                    System.Drawing.Color.FromArgb(180, 0, 0, 0)
                );
                using var font = new System.Drawing.Font(
                    "Segoe UI",
                    8.5f,
                    System.Drawing.FontStyle.Bold,
                    System.Drawing.GraphicsUnit.Pixel
                );

                // Draw shadow for better readability
                g.DrawString($"↓{shortDown}", font, shadowBrush, new System.Drawing.PointF(1, 1));
                g.DrawString($"↑{shortUp}", font, shadowBrush, new System.Drawing.PointF(1, 9));

                // Draw main text
                g.DrawString($"↓{shortDown}", font, textBrush, new System.Drawing.PointF(0, 0));
                g.DrawString($"↑{shortUp}", font, textBrush, new System.Drawing.PointF(0, 8));
            }
            var h = bmp.GetHicon();
            return System.Drawing.Icon.FromHandle(h);
        }

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateSpeedAsync();
        }

        private async void UpdateSpeedAsync()
        {
            try
            {
                var (down, up) = await _speedService.GetAggregateBytesPerSecondAsync();
                _lastUpdate = DateTime.Now;

                Dispatcher.Invoke(() =>
                {
                    DownloadSpeed = FormatSpeed(down, SelectedUnit);
                    UploadSpeed = FormatSpeed(up, SelectedUnit);

                    _overlay?.UpdateSpeed(UploadSpeed, DownloadSpeed);

                    if (_notifyIcon != null)
                    {
                        if (_notifyIcon.Icon == null)
                            _notifyIcon.Icon = LoadTrayIcon();
                        _notifyIcon.Visible = true;
                        _notifyIcon.Text = $"↓{DownloadSpeed} ↑{UploadSpeed}";
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    DownloadSpeed = "Error";
                    UploadSpeed = "Error";
                    _overlay?.UpdateSpeed("Err", "Err");
                });
                Debug.WriteLine($"Error updating speed: {ex.Message}");
            }
        }

        private string FormatSpeed(long bytesPerSecond, string unit)
        {
            if (_autoUnitSwitching)
            {
                return FormatSpeedWithAutoSwitching(bytesPerSecond);
            }

            return unit switch
            {
                "B/s" => $"{bytesPerSecond} B/s",
                // Use decimal units to match browsers and most download managers (kilo=1000)
                "kB/s" => $"{bytesPerSecond / 1000.0:F1} kB/s",
                "MB/s" => $"{bytesPerSecond / 1_000_000.0:F2} MB/s",
                // Megabits per second also decimal-based
                "Mb/s" => $"{(bytesPerSecond * 8) / 1_000_000.0:F2} Mb/s",
                _ => $"{bytesPerSecond / 1_000_000.0:F2} MB/s",
            };
        }

        private string FormatSpeedWithAutoSwitching(long bytesPerSecond)
        {
            // Auto-switch using decimal units (kB=1000 B, MB=1,000,000 B)
            if (bytesPerSecond >= 1_000_000)
            {
                return $"{bytesPerSecond / 1_000_000.0:F2} MB/s";
            }
            else if (bytesPerSecond >= 1_000)
            {
                return $"{bytesPerSecond / 1_000.0:F1} kB/s";
            }
            else
            {
                return $"{bytesPerSecond} B/s";
            }
        }

        private void UpdateConnectionInfo()
        {
            Task.Run(() =>
            {
                try
                {
                    var connections = new List<string>();
                    var adapters = new List<string>();

                    foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (
                            ni.OperationalStatus == OperationalStatus.Up
                            && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                        )
                        {
                            var connectionType = ni.NetworkInterfaceType.ToString();
                            var speed = ni.Speed > 0 ? $" ({ni.Speed / 1000000} Mbps)" : "";

                            adapters.Add($"• {ni.Name} - {connectionType}{speed}");

                            var ipProps = ni.GetIPProperties();
                            foreach (var ip in ipProps.UnicastAddresses)
                            {
                                if (
                                    ip.Address.AddressFamily
                                    == System.Net.Sockets.AddressFamily.InterNetwork
                                )
                                {
                                    connections.Add($"• {ni.Name}: {ip.Address}");
                                    break;
                                }
                            }
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        ConnectionInfo = connections.Any()
                            ? string.Join("\n", connections)
                            : "No active connections detected";

                        AdapterInfo = adapters.Any()
                            ? string.Join("\n", adapters)
                            : "No active adapters detected";
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        ConnectionInfo = $"Error retrieving connection info: {ex.Message}";
                        AdapterInfo = "Error retrieving adapter info";
                    });
                }
            });
        }

        private void UnitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is WpfComboBox combo && combo.SelectedItem != null)
            {
                SelectedUnit = combo.SelectedItem.ToString() ?? "KB/s";
            }
        }

        private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            MinimizeToTray();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        private void MinimizeToTray()
        {
            Hide();
            if (_notifyIcon != null)
            {
                if (_notifyIcon.Icon == null)
                    _notifyIcon.Icon = LoadTrayIcon(); // fallback so it’s never invisible

				_notifyIcon.Visible = true;
            }
        }

        private void ShowWindow()
        {
            BringToForeground();
        }

        public void BringToForeground()
        {
            // Show the window if it's hidden
            if (!IsVisible)
            {
                Show();
            }

            // Restore if minimized
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            // Bring to front and activate
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();

            // Hide tray icon when window is shown
            if (_notifyIcon != null)
                _notifyIcon.Visible = false;
        }

        private void ExitApplication()
        {
            _timer?.Stop();
            _notifyIcon?.Dispose();
            _overlay?.Close();
            System.Windows.Application.Current.Shutdown();
        }

		private System.Windows.Media.Color ParseColor(string name)
		{
			return name switch
			{
				"White" => Colors.White,
				"Black" => Colors.Black,
				"Red" => Colors.Red,
				"Green" => Colors.Green,
				"Blue" => Colors.Blue,
				"Yellow" => Colors.Yellow,
				"Cyan" => Colors.Cyan,
				"Magenta" => Colors.Magenta,
				"DarkGray" => System.Windows.Media.Color.FromRgb(48, 48, 48),
				_ => Colors.White,
			};
		}

		private void ApplyOverlaySettingsToWindow()
		{
			if (_overlay == null) return;
			// Text color
			_overlay.SetTextColor(ParseColor(_settings.TextColor));
			// Background
			if (_settings.BackgroundOption == "No Background")
			{
				_overlay.SetTransparentBackground(true);
			}
			else
			{
				_overlay.SetTransparentBackground(false);
				_overlay.SetBackgroundColor(ParseColor(_settings.BackgroundOption));
			}
			// Position
			_overlay.SetPosition(_settings.Position);
			// Font
			_overlay.SetFontFamily(new System.Windows.Media.FontFamily(_settings.FontFamily));
			_overlay.SetFontSize(_settings.FontSize);
			_overlay.SetFontVariant(_settings.FontVariant);

			// Reflect UI controls if created
			if (_textColorCombo != null) _textColorCombo.SelectedItem = _settings.TextColor;
			if (_bgColorCombo != null) _bgColorCombo.SelectedItem = _settings.BackgroundOption;
			if (_positionCombo != null) _positionCombo.SelectedItem = _settings.Position;
			if (_fontFamilyCombo != null) _fontFamilyCombo.SelectedItem = _settings.FontFamily;
			if (_fontSizeCombo != null) _fontSizeCombo.SelectedItem = _settings.FontSize;
			if (_fontVariantCombo != null) _fontVariantCombo.SelectedItem = _settings.FontVariant;
		}

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                MinimizeToTray();
            }
            base.OnStateChanged(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Stop timer and dispose tray/overlay when the window actually closes.
            _timer?.Stop();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            try
            {
                _overlay?.Close();
            }
            catch { }

            base.OnClosed(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Allow the window to close normally when user clicks X.
            // Cleanup will happen in OnClosed.
            base.OnClosing(e);
        }
    }
}
