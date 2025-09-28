using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace InternetSpeedMonitor
{
    /// <summary>
    /// Click-through always-on-top overlay window displaying live speeds.
    /// Centered content, configurable colors/fonts, position anchors, and size-aware positioning.
    /// </summary>
    public class OverlayWindow : Window
    {
        private TextBlock _prefixUpload;
        private TextBlock _valueUpload;
        private TextBlock _separator;
        private TextBlock _prefixDownload;
        private TextBlock _valueDownload;
		private Border _containerBorder;
		private string _currentPosition = "Top Right";
        private double _currentFontSize = 14;

        public OverlayWindow()
        {
			// Window properties
			SizeToContent = SizeToContent.WidthAndHeight;
			MinWidth = 200;
			MinHeight = 40;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

			// Semi-transparent background with rounded corners
			_containerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)), // #AA000000
                CornerRadius = new CornerRadius(8),
				Padding = new Thickness(8),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch
            };

            // Fixed layout to avoid width jumping when numbers change
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _prefixUpload = new TextBlock { Text = "UP", Foreground = Brushes.White, FontSize = _currentFontSize, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,0), VerticalAlignment = VerticalAlignment.Center };
            _valueUpload = new TextBlock { Text = "0", Foreground = Brushes.White, FontSize = _currentFontSize, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0) };
            _separator = new TextBlock { Text = "|", Foreground = Brushes.White, FontSize = _currentFontSize, FontWeight = FontWeights.Bold, Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
            _prefixDownload = new TextBlock { Text = "DOWN", Foreground = Brushes.White, FontSize = _currentFontSize, FontWeight = FontWeights.Bold, Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
            _valueDownload = new TextBlock { Text = "0", Foreground = Brushes.White, FontSize = _currentFontSize, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0) };

            row.Children.Add(_prefixUpload);
            row.Children.Add(_valueUpload);
            row.Children.Add(_separator);
            row.Children.Add(_prefixDownload);
            row.Children.Add(_valueDownload);

            _containerBorder.Child = row;
			Content = _containerBorder;

            // Apply initial proportional widths/margins
            ApplyTypographyLayout();

			Loaded += OverlayWindow_Loaded;
			SizeChanged += (s, e) => SetPosition(_currentPosition);
        }

        private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
			// Anchor to the current position with correct content size
			SetPosition(_currentPosition);

            // Make window click-through
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = NativeMethods.GetWindowLong(hwnd, -20);
            NativeMethods.SetWindowLong(hwnd, -20, extendedStyle | 0x20); // WS_EX_TRANSPARENT
        }

        public void UpdateSpeed(string upload, string download)
        {
            _valueUpload.Text = upload;
            _valueDownload.Text = download;
        }

        public void SetTextColor(Color color)
        {
            var brush = new SolidColorBrush(color);
            _prefixUpload.Foreground = brush;
            _valueUpload.Foreground = brush;
            _separator.Foreground = brush;
            _prefixDownload.Foreground = brush;
            _valueDownload.Foreground = brush;
        }

		public void SetTransparentBackground(bool transparent)
		{
			_containerBorder.Background = transparent
				? Brushes.Transparent
				: new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
		}

		public void SetBackgroundColor(Color color)
		{
			// Keep some translucency for readability
			_containerBorder.Background = new SolidColorBrush(Color.FromArgb(170, color.R, color.G, color.B));
		}

		public void SetPosition(string position)
		{
			const int marginRight = 20;
			const int marginLeft = 20;
			const int marginTop = 20;
			const int marginBottom = 20;

			var wa = SystemParameters.WorkArea; // accounts for taskbar
			double width = (ActualWidth > 0 ? ActualWidth : Width);
			double height = (ActualHeight > 0 ? ActualHeight : Height);

			switch (position)
			{
				case "Top Left":
				Left = wa.Left + marginLeft;
				Top = wa.Top + marginTop;
					break;
				case "Top Center":
				Left = wa.Left + (wa.Width - width) / 2.0;
				Top = wa.Top + marginTop;
					break;
				case "Top Right":
				Left = wa.Right - width - marginRight;
				Top = wa.Top + marginTop;
					break;
				case "Bottom Left":
				Left = wa.Left + marginLeft;
				Top = wa.Bottom - height - marginBottom;
					break;
				case "Bottom Center":
				Left = wa.Left + (wa.Width - width) / 2.0;
				Top = wa.Bottom - height - marginBottom;
					break;
				case "Bottom Right":
				Left = wa.Right - width - marginRight;
				Top = wa.Bottom - height - marginBottom;
					break;
			}
			_currentPosition = position;
		}

        public void SetFontFamily(FontFamily family)
        {
            _prefixUpload.FontFamily = family;
            _valueUpload.FontFamily = family;
            _separator.FontFamily = family;
            _prefixDownload.FontFamily = family;
            _valueDownload.FontFamily = family;
            ApplyTypographyLayout();
            SetPosition(_currentPosition);
        }

        public void SetFontSize(double size)
        {
            _currentFontSize = size;
            _prefixUpload.FontSize = size;
            _valueUpload.FontSize = size;
            _separator.FontSize = size;
            _prefixDownload.FontSize = size;
            _valueDownload.FontSize = size;
            ApplyTypographyLayout();
            SetPosition(_currentPosition);
        }

		public void SetFontVariant(string variant)
		{
			// Reset defaults
            _prefixUpload.FontWeight = FontWeights.Normal;
            _valueUpload.FontWeight = FontWeights.Normal;
            _separator.FontWeight = FontWeights.Normal;
            _prefixDownload.FontWeight = FontWeights.Normal;
            _valueDownload.FontWeight = FontWeights.Normal;
            _prefixUpload.FontStyle = FontStyles.Normal;
            _valueUpload.FontStyle = FontStyles.Normal;
            _separator.FontStyle = FontStyles.Normal;
            _prefixDownload.FontStyle = FontStyles.Normal;
            _valueDownload.FontStyle = FontStyles.Normal;
            System.Windows.Media.TextOptions.SetTextFormattingMode(_containerBorder, System.Windows.Media.TextFormattingMode.Ideal);
            System.Windows.Media.TextOptions.SetTextRenderingMode(_containerBorder, System.Windows.Media.TextRenderingMode.ClearType);

			switch (variant)
			{
                case "Bold":
                    _prefixUpload.FontWeight = FontWeights.Bold;
                    _valueUpload.FontWeight = FontWeights.Bold;
                    _separator.FontWeight = FontWeights.Bold;
                    _prefixDownload.FontWeight = FontWeights.Bold;
                    _valueDownload.FontWeight = FontWeights.Bold;
					break;
				case "Italic":
                    _prefixUpload.FontStyle = FontStyles.Italic;
                    _valueUpload.FontStyle = FontStyles.Italic;
                    _separator.FontStyle = FontStyles.Italic;
                    _prefixDownload.FontStyle = FontStyles.Italic;
                    _valueDownload.FontStyle = FontStyles.Italic;
					break;
				case "Bold Italic":
                    _prefixUpload.FontWeight = FontWeights.Bold;
                    _valueUpload.FontWeight = FontWeights.Bold;
                    _separator.FontWeight = FontWeights.Bold;
                    _prefixDownload.FontWeight = FontWeights.Bold;
                    _valueDownload.FontWeight = FontWeights.Bold;
                    _prefixUpload.FontStyle = FontStyles.Italic;
                    _valueUpload.FontStyle = FontStyles.Italic;
                    _separator.FontStyle = FontStyles.Italic;
                    _prefixDownload.FontStyle = FontStyles.Italic;
                    _valueDownload.FontStyle = FontStyles.Italic;
					break;
				case "Sharp":
					// Use aliased rendering for a sharper look
                    System.Windows.Media.TextOptions.SetTextFormattingMode(_containerBorder, System.Windows.Media.TextFormattingMode.Display);
                    System.Windows.Media.TextOptions.SetTextRenderingMode(_containerBorder, System.Windows.Media.TextRenderingMode.Aliased);
					break;
				default:
					// Regular
					break;
			}
            ApplyTypographyLayout();
			SetPosition(_currentPosition);
		}

        private void ApplyTypographyLayout()
        {
            // Tight, constant gaps; allow natural width so values can grow/shrink with digits
            double spacing = 4;

            _prefixUpload.Margin = new Thickness(spacing, 0, spacing, 0);
            _valueUpload.Margin = new Thickness(spacing, 0, spacing, 0);
            _separator.Margin = new Thickness(spacing, 0, spacing, 0);
            _prefixDownload.Margin = new Thickness(spacing, 0, spacing, 0);
            _valueDownload.Margin = new Thickness(spacing, 0, spacing, 0);

            _valueUpload.Width = double.NaN;
            _valueDownload.Width = double.NaN;
            _separator.Width = double.NaN;
        }
    }
}
