using System;
using System.Management;
using System.Threading.Tasks;

namespace InternetSpeedMonitor.Services
{
	public class WmiNetworkSpeedService : INetworkSpeedService
	{
		public Task<(long bytesDownPerSec, long bytesUpPerSec)> GetAggregateBytesPerSecondAsync()
		{
			return Task.Run(() =>
			{
				long totalDownload = 0;
				long totalUpload = 0;
				using var searcher = new ManagementObjectSearcher(
					"SELECT Name, BytesReceivedPerSec, BytesSentPerSec FROM Win32_PerfFormattedData_Tcpip_NetworkInterface");
				foreach (ManagementObject obj in searcher.Get())
				{
					var name = obj["Name"]?.ToString();
					if (string.IsNullOrEmpty(name) || name.Contains("Loopback") || name.Contains("Teredo") || name.Contains("isatap"))
						continue;
					totalDownload += Convert.ToInt64(obj["BytesReceivedPerSec"] ?? 0);
					totalUpload += Convert.ToInt64(obj["BytesSentPerSec"] ?? 0);
				}
				return (totalDownload, totalUpload);
			});
		}
	}
}


