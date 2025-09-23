using System;
using System.Drawing;
using System.Windows.Forms;

namespace InternetSpeedMonitor.Infrastructure
{
	public sealed class TrayService : IDisposable
	{
		private readonly NotifyIcon _notifyIcon;
		public TrayService(Icon icon, string tooltip)
		{
			_notifyIcon = new NotifyIcon { Icon = icon, Visible = false, Text = tooltip };
		}

		public void SetIcon(Icon icon) => _notifyIcon.Icon = icon;
		public void SetTooltip(string text) => _notifyIcon.Text = text;
		public void Show() => _notifyIcon.Visible = true;
		public void Hide() => _notifyIcon.Visible = false;
		public ContextMenuStrip? ContextMenu { get => _notifyIcon.ContextMenuStrip; set => _notifyIcon.ContextMenuStrip = value; }

		public void Dispose()
		{
			_notifyIcon.Visible = false;
			_notifyIcon.Dispose();
		}
	}
}


