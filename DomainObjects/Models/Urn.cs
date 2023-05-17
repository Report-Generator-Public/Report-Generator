using DomainObjects.Enums;

namespace DomainObjects.Models;

public class Urn
{
    public int? VoidID { get; set; }
    public DateTime? ArrivalDate { get; set; }
    public string COVIDT1CTResult { get; set; } = "N/A";
    public string COVIDT2CTResult { get; set; } = "N/A";
    public string CombinedResult { get; set; } = "Not Approved";
    public TemplateOptions TemplateID { get; set; } = TemplateOptions.National_Health;
    public CorporateAccount? Corporate { get; set; }
    public string PassportNumber { get; set; }
    public string? BookingReference { get; set; }
    public Nationality Nationality { get; set; }
    public TestToReleaseType? TestToReleaseType { get; set; }
    public AccessionLocation AccessionLocation { get; set; }
}