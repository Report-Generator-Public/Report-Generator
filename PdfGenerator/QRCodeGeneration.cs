using System.Text;
using DomainObjects.Models;
using QRCoder;

namespace Pdf;

public class QrCodeGeneration
{
    public string CreateQrCodeForData(QrReportData data)
    {
        StringBuilder builder = new StringBuilder();
        var currentTitle = data.IsAntigen ? data.AntigenTitle : data.Title;

        builder.AppendLine(currentTitle);
        builder.AppendLine($"URN: {data.Barcode}");
        builder.AppendLine($"Name: {data.Name}");
        builder.AppendLine($"Date of Birth: {data.DoB.ToLocalTime().ToString("dd-MMM-yyyy")}");
        builder.AppendLine($"Date of Report: {data.ReportDate}");
        if (!string.IsNullOrWhiteSpace(data.PassportNum))
            builder.AppendLine($"Passport Number: {data.PassportNum}");
        builder.AppendLine($"Result: {data.Result}");

        return Generate(builder.ToString());
    }

    private string Generate(string content)
    {
        QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        Base64QRCode qrCode = new Base64QRCode(qrCodeData);
        string qrCodeImageAsBase64 = qrCode.GetGraphic(5);

        return qrCodeImageAsBase64;
    }
}