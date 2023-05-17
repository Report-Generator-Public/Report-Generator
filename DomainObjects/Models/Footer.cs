namespace DomainObjects.Models;

public class Footer
{
    public string ContactDetails { get; set; }
    public string? LogoBase64 { get; set; } = null;
    public string FooterContent { get; set; }
}