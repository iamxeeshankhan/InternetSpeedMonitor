using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace InternetSpeedMonitor.Services
{
	public sealed class NetworkInterfaceSpeedService : INetworkSpeedService
	{
		private string? _activeInterfaceId;
		private long _previousTotalBytesReceived;
		private long _previousTotalBytesSent;
		private DateTime _lastSampleTimeUtc = DateTime.MinValue;
		private readonly object _sync = new object();
		// Exponential moving average to smooth short-term spikes
		private double _emaBytesDownPerSec;
		private double _emaBytesUpPerSec;
		private bool _emaInitialized;
		private long _lastReturnedDown;
		private long _lastReturnedUp;

		public Task<(long bytesDownPerSec, long bytesUpPerSec)> GetAggregateBytesPerSecondAsync()
		{
			return Task.Run(() =>
			{
				try
				{
					// Pick the single primary active interface (with default IPv4 gateway) among Ethernet/Wi‑Fi
					NetworkInterface? primary = null;
					foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
					{
						if (ni.OperationalStatus != OperationalStatus.Up)
							continue;
						if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
							continue;

						bool isRealAdapter =
							ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
							ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
						if (!isRealAdapter)
							continue;

						var name = (ni.Name ?? string.Empty);
						var desc = (ni.Description ?? string.Empty);
						if (name.Contains("Teredo", StringComparison.OrdinalIgnoreCase) ||
							name.Contains("isatap", StringComparison.OrdinalIgnoreCase) ||
							name.Contains("Loopback", StringComparison.OrdinalIgnoreCase) ||
							desc.Contains("Teredo", StringComparison.OrdinalIgnoreCase) ||
							desc.Contains("isatap", StringComparison.OrdinalIgnoreCase) ||
							desc.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) ||
							desc.Contains("HyperV", StringComparison.OrdinalIgnoreCase) ||
							desc.Contains("VMware", StringComparison.OrdinalIgnoreCase) ||
							desc.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase) ||
							desc.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
							desc.Contains("VPN", StringComparison.OrdinalIgnoreCase) ||
							desc.Contains("Npcap", StringComparison.OrdinalIgnoreCase) ||
							desc.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
							continue;

						try
						{
							var ipProps = ni.GetIPProperties();
							bool hasDefaultV4Gw = false;
							foreach (var gw in ipProps.GatewayAddresses)
							{
								if (gw?.Address != null && gw.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
								{
									hasDefaultV4Gw = true;
									break;
								}
							}
							if (hasDefaultV4Gw)
							{
								primary = ni;
								break; // Prefer the first that matches (usually the active one)
							}
						}
						catch { }
					}

					// Fallback: if none had default gateway, choose any real up adapter
					if (primary == null)
					{
						foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
						{
							if (ni.OperationalStatus != OperationalStatus.Up)
								continue;
							if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
								continue;
							if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
							{
								primary = ni;
								break;
							}
						}
					}

					long totalReceived = 0;
					long totalSent = 0;
					string? currentId = null;
					if (primary != null)
					{
						try
						{
							var stats = primary.GetIPStatistics();
							totalReceived = stats.BytesReceived;
							totalSent = stats.BytesSent;
							currentId = primary.Id;
						}
						catch { }
					}

					lock (_sync)
					{
						var nowUtc = DateTime.UtcNow;
						bool interfaceChanged = _activeInterfaceId != null && currentId != null && !string.Equals(_activeInterfaceId, currentId, StringComparison.OrdinalIgnoreCase);
						if (_lastSampleTimeUtc == DateTime.MinValue || interfaceChanged)
						{
							// First sample or interface changed – initialize and return zeros to avoid spikes
							_previousTotalBytesReceived = totalReceived;
							_previousTotalBytesSent = totalSent;
							_activeInterfaceId = currentId;
							_lastSampleTimeUtc = nowUtc;
							_emaBytesDownPerSec = 0;
							_emaBytesUpPerSec = 0;
							_emaInitialized = false;
							_lastReturnedDown = 0;
							_lastReturnedUp = 0;
							return (0L, 0L);
						}

						double seconds = Math.Max(0.1, (nowUtc - _lastSampleTimeUtc).TotalSeconds);
						long deltaRecv = totalReceived - _previousTotalBytesReceived;
						long deltaSent = totalSent - _previousTotalBytesSent;

						_previousTotalBytesReceived = totalReceived;
						_previousTotalBytesSent = totalSent;
						_lastSampleTimeUtc = nowUtc;

						// Guard against counter resets (e.g., adapter resets) resulting in negatives
						if (deltaRecv < 0) deltaRecv = 0;
						if (deltaSent < 0) deltaSent = 0;

						// If the sample interval is too short, skip updating to avoid inflated readings
						// due to timer jitter/overlap. Keep the previous values.
						if (seconds < 0.8)
						{
							return (_lastReturnedDown, _lastReturnedUp);
						}

						double instantaneousDown = deltaRecv / seconds;
						double instantaneousUp = deltaSent / seconds;

						// Apply EMA with mild smoothing (alpha=0.5)
						const double alpha = 0.5;
						if (!_emaInitialized)
						{
							_emaBytesDownPerSec = instantaneousDown;
							_emaBytesUpPerSec = instantaneousUp;
							_emaInitialized = true;
						}
						else
						{
							_emaBytesDownPerSec = alpha * instantaneousDown + (1 - alpha) * _emaBytesDownPerSec;
							_emaBytesUpPerSec = alpha * instantaneousUp + (1 - alpha) * _emaBytesUpPerSec;
						}

						long bytesPerSecDown = (long)Math.Round(_emaBytesDownPerSec);
						long bytesPerSecUp = (long)Math.Round(_emaBytesUpPerSec);
						_lastReturnedDown = bytesPerSecDown;
						_lastReturnedUp = bytesPerSecUp;
						return (bytesPerSecDown, bytesPerSecUp);
					}
				}
				catch
				{
					return (0L, 0L);
				}
			});
		}
	}
}


