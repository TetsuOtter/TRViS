namespace TRViS.Services;

public interface IWifiService
{
	Task<string?> GetCurrentSsidAsync();
}
