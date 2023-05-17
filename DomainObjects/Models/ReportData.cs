using DomainObjects.Constants;
using DomainObjects.Enums;

namespace DomainObjects.Models;

public class ReportData
{
    public string? LogoBase64 { get; set; } = null;
    public TestRegistration TestRegistration { get; set; } = new();
    public Sample Sample { get; set; } = new();
    public List<Result> Results { get; set; } = new();
    public ReportStatement Statement { get; set; } = new();
    public TemplateOptions TemplateOption { get; set; } = TemplateOptions.National_Health;
    public bool Void { get; set; } = false;
    public string? VoidMessage { get; set; }
    public Urn Urn { get; set; }
    public int? KitType { get; set; }
    public Lab? Lab { get; set; }
    public ReportAddress ReportAddress { get; set; }
    public Footer Footer { get; set; }

    public List<Result> FilterResults()
    {
        return Results.Where(x => x.ReportedName.ToUpper().Trim() == RequiredTests.Covid1TestName.ToUpper().Trim() ||
                                  x.ReportedName.ToUpper().Trim() == RequiredTests.Covid2TestName.ToUpper().Trim()).ToList();
    }

    public string GetTestName(string expectedReportedName)
    {
        return expectedReportedName switch
        {
            "COVID-19 T1" => "ORF1ab",
            "COVID-19 T2" => GetTestNameBasedOnKit(),
            _ => "Not Provided"
        };
    }

    private string GetTestNameBasedOnKit()
    {
        if (Urn.VoidID != null)
            return "N/A";
        if (KitType == 1)
            return "N-gene";

        return "E-gene";
    }
}