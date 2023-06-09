namespace Automation.Models.Settings;

public sealed class FunctionSettings
{
    public string ProcessName { get; set; }
    public string LogoContainerName { get; set; }
    public string LogoFileName { get; set; }
    public string FooterLogoContainerName { get; set; }
    public string FooterLogoFileName { get; set; }
    public FooterSettings Footer { get; set; }
}