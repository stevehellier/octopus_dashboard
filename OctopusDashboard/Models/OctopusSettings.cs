namespace OctopusDashboard.Models;

public class OctopusSettings
{
    public string ApiKey { get; set; } = "";
    public string Mpan { get; set; } = "";
    public string ElectricityMeterSerial { get; set; } = "";
    public string Mprn { get; set; } = "";
    public string GasMeterSerial { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string ElectricityTariffCode { get; set; } = "";
    public string GasTariffCode { get; set; } = "";
}
