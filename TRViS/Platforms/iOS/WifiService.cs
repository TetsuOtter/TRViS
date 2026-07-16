using SystemConfiguration;

namespace TRViS.Platforms.iOS;

public class WifiService : TRViS.Services.IWifiService
{
	private static readonly NLog.Logger logger = TRViS.Services.LoggerService.GetGeneralLogger();

	public Task<string?> GetCurrentSsidAsync()
	{
		try
		{
			var status = CaptiveNetwork.TryCopyCurrentNetworkInfo("en0", out Foundation.NSDictionary? dict);
			if (status != StatusCode.OK || dict is null)
			{
				logger.Debug("WifiService(iOS): CaptiveNetwork returned null for en0");
				return Task.FromResult<string?>(null);
			}
			if (dict.TryGetValue(CaptiveNetwork.NetworkInfoKeySSID, out Foundation.NSObject? ssidObj))
			{
				string? ssid = ssidObj?.ToString();
				logger.Debug("WifiService(iOS): SSID={0}", ssid ?? "(null)");
				return Task.FromResult<string?>(ssid);
			}
		}
		catch (Exception ex)
		{
			logger.Warn(ex, "WifiService(iOS): Failed to get SSID");
		}
		return Task.FromResult<string?>(null);
	}
}
