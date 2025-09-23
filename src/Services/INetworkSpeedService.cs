using System.Threading.Tasks;

namespace InternetSpeedMonitor.Services
{
	public interface INetworkSpeedService
	{
		Task<(long bytesDownPerSec, long bytesUpPerSec)> GetAggregateBytesPerSecondAsync();
	}
}


