namespace DomainObjects.Models;

public class QrReportData
{
    public bool IsAntigen { get; set; }
    public string Title { get; private set; } = "Confirmation this is a genuine Randox Health Result Certificate for COVID-19 PCR Test.";
    public string AntigenTitle { get; private set; } = "Confirmation this is a genuine Randox Health Result Certificate for COVID-19 Antigen Test.";
    public string Barcode { get; set; }
    public string Name { get; set; }
    public DateTime DoB { get; set; }
    /// <summary>
    /// String passed over to help with date and time string formatted (i.e. templates 4 and 6 differences)
    /// </summary>
    /// <value></value>
    public string ReportDate { get; set; }
    public string PassportNum { get; set; }
    public string Result { get; set; }
}