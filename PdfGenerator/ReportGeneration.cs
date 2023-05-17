using System.Text;
using DomainObjects.Constants;
using DomainObjects.Enums;
using DomainObjects.Extensions;
using DomainObjects.Models;
using DomainObjects.Models.Config;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pdf.Extensions;

namespace Pdf;

public class ReportGeneration
{
    private const string NotProvided = "NOTPROVIDED";
    private const int AdditionalInformationBaseRows = 4;
    private const int AdditionalInformationBaseRowsMax = 6;

    private readonly ILogger<ReportGeneration> _logger;
    private readonly TimeZoneInfo _gmtTimeZone;
    private readonly int _additionalInformationBlanksMax;
    private int _additionalInformationBlanks;
    private readonly QrCodeGeneration _qrCodeGeneration;
    private readonly PdfConfig _pdfConfig;

    public ReportGeneration(ILogger<ReportGeneration> logger, IOptions<PdfConfig> options, QrCodeGeneration qrCodeGeneration)
    {
        _logger = logger;
        _pdfConfig = options.Value;
        _gmtTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        _qrCodeGeneration = qrCodeGeneration;
        _additionalInformationBlanksMax = AdditionalInformationBaseRowsMax - AdditionalInformationBaseRows;
        _additionalInformationBlanks = 0;
    }

    public async Task<byte[]?> GenerateReport(ReportData reportData)
    {
        if (reportData.Urn == null)
            return default;

        try
        {
            return reportData.Urn.TemplateID switch
            {
                TemplateOptions.Antigen_Day_Two => await GenerateAntigenDayTwoReport(reportData),
                _ => await GenerateDynamicReport(reportData)
            };
        }
        catch (Exception e)
        {
            _logger.LogError("Report Generation {method} error triggered with exception: {message}", Helpers.GetCallerMemberName(),
                e.Message);
            return default;
        }
    }

    private async Task<byte[]?> GenerateDynamicReport(ReportData reportData)
    {
        try
        {
            //Setup PDF document readers and writers.
            MemoryStream stream = new MemoryStream();
            PdfWriter writer = new PdfWriter(stream);
            PdfDocument pdf = new PdfDocument(writer);
            Document document = new Document(pdf);

            //Create the report logo.
            document.Add(await CreateHeaderLogoTable(reportData));
            document.Add(await CreateLineBreak(100));
            //Create the address for Randox.
            document.Add(await CreateAddress(reportData));
            document.Add(await CreateAdditionalInformation(reportData));
            document.Add(await CreateLineBreak(100));

            if (IsPrivateUser(reportData))
                document.Add(await CreateReportTitle(reportData));

            if (reportData.Void && reportData.Urn.VoidID == 7)
            {
                //Create the void statement.
                document.Add(await CreateURNVoidStatememt(reportData));
            }
            else
            {
                //Create the statement.
                document.Add(await CreateStatement(reportData));
            }

            //Create the report table if correct template.
            if (reportData.Urn.VoidID == null)
            {
                if (IsAntiGen(reportData))
                    document.Add(await CreateAntiGenResultTable(reportData));
                else if (IsMobileLab(reportData) || IsGbOlympicsRemote(reportData))
                    document.Add(await CreateSingleResultReportTableForMobileLab(reportData));
                else if (IsCoronaFocus(reportData.TemplateOption))
                    document.Add(await CreateSingleResultReportTableForCoronaFocus(reportData));
                else if (IsPrivateUser(reportData))
                    document.Add(await CreateMultipleResultReportTable(reportData));

                //Generic Page Footer
                if (ShowTestContent(reportData))
                    document.Add(await CreateTestDetails(reportData));
            }

            document.Add(await CreateFooter(document, pdf.GetDefaultPageSize(), reportData));
            document.Close();

            return stream.ToArray();
        }
        catch (Exception e)
        {
            _logger.LogError("Report Generation {method} error triggered with exception: {message}", Helpers.GetCallerMemberName(),
                e.Message);
            return Array.Empty<byte>();
        }
    }

    private async Task<byte[]?> GenerateAntigenDayTwoReport(ReportData reportData)
    {
        try
        {
            //Setup PDF document readers and writers.
            MemoryStream stream = new MemoryStream();
            PdfWriter writer = new PdfWriter(stream);
            PdfDocument pdf = new PdfDocument(writer);
            Document document = new Document(pdf);

            //Create the report logo.
            document.Add(await CreateHeaderLogoTable(reportData));

            //Create the address for Randox.
            document.Add(await CreateLineBreak(100));
            document.Add(await CreateAddress(reportData, true));
            document.Add(await CreateAdditionalInformation(reportData));
            document.Add(await CreateLineBreak(100));

            // Add report title.
            document.Add(await CreateReportTitle(reportData));

            // Deal with voids.
            if (reportData.Void && reportData.Urn.VoidID == 7)
            {
                //Create the void statement.
                document.Add(await CreateURNVoidStatememt(reportData));
            }
            else
            {
                //Create the statement.
                document.Add(await CreateStatement(reportData));
            }

            if (reportData.Urn.VoidID == null)
            {
                //Create the report table if correct template.
                document.Add(await CreateAntiGenResultTable(reportData));

                //Generic Page Footer
                document.Add(await CreateTestDetails(reportData));
            }

            // Create footer.
            document.Add(await CreateFooter(document, pdf.GetDefaultPageSize(), reportData));
            document.Close();

            return stream.ToArray();
        }
        catch (Exception e)
        {
            _logger.LogError("Report Generation {name} error triggered with exception: {e.Message}", Helpers.GetCallerMemberName(),
                e.Message);
            return Array.Empty<byte>();
        }
    }

    private async Task<Table> CreateHeaderLogoTable(ReportData reportData)
    {
        return await Task.Run(() =>
        {
            Table table = new Table(new float[] { 1, 1 })
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(-5.0f);

            if (reportData.LogoBase64 is not null)
            {
                table.AddCell(new Cell()
                    .Add(
                        new Image(ImageDataFactory.Create(Convert.FromBase64String(reportData.LogoBase64)))
                            .SetHeight(UnitValue.CreatePercentValue(30))
                            .SetWidth(UnitValue.CreatePercentValue(40))
                            .SetHorizontalAlignment(HorizontalAlignment.LEFT)
                            .SetMarginTop(0.0f)
                    )
                    .SetBorder(Border.NO_BORDER)
                );
            }

            table.AddCell(new Cell()
                .Add(
                    new Image(ImageDataFactory.Create(Convert.FromBase64String(CreateQRCodeImage(reportData))))
                        .SetHeight(UnitValue.CreatePercentValue(25))
                        .SetWidth(UnitValue.CreatePercentValue(25))
                        .SetHorizontalAlignment(HorizontalAlignment.RIGHT)
                        .SetMarginTop(-5.0f)
                )
                .SetBorder(Border.NO_BORDER)
            );

            return table;
        });
    }

    private string CreateQRCodeImage(ReportData reportData)
    {
        string name = reportData.TestRegistration.FirstName + " " + reportData.TestRegistration.LastName;

        string dateOfReportFormat = DateFormatHasHours(reportData) ? "dd-MMM-yyyy HH:mm" : "dd-MMM-yyyy";
        DateTime formattedDateOfReport = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _gmtTimeZone);
        bool antigen = IsAntiGen(reportData) || IsAntigenDayTwo(reportData);

        var qrData = new QrReportData()
        {
            IsAntigen = antigen,
            Barcode = reportData.Sample.LabelId,
            Name = name,
            DoB = (DateTime)reportData.TestRegistration.DateOfBirth,
            ReportDate = formattedDateOfReport.ToString(dateOfReportFormat),
            PassportNum = reportData.Urn.PassportNumber,
            Result = reportData.Urn.CombinedResult
        };

        return _qrCodeGeneration.CreateQrCodeForData(qrData);
    }

    /// <summary>
    /// Creates a line break to separate sections of the report.
    /// </summary>
    /// <param name="width">The percentage of the screen for the page break to use.</param>
    /// <returns>Returns the page-break to be added to the report.</returns>
    private static async Task<Paragraph> CreateLineBreak(float width = 100.0f)
    {
        PdfFont lineBreakFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color lineBreakFontColor = ColorConstants.GRAY;
        float lineBreakFontSize = width / 10.0f;

        return await Task.Run(() => new Paragraph("______________________________________________________________________________________________")
            .SetFont(lineBreakFont)
            .SetFontColor(lineBreakFontColor)
            .SetFontSize(lineBreakFontSize));
    }

    /// <summary>
    /// Creates the base address for the report.
    /// </summary>
    /// <returns></returns>
    private async Task<Table> CreateAddress(ReportData reportData, bool isRandoxHealthAddress = false)
    {
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 10.0f;

        return await Task.Run(() =>
        {
            Table table = new Table(new float[] { 1 })
                .SetWidth(UnitValue.CreatePercentValue(33))
                .SetMarginTop(5.0f)
                .SetHorizontalAlignment(HorizontalAlignment.LEFT);

            if (isRandoxHealthAddress)
            {
                table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Name, false, TextAlignment.LEFT);
                table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Line1, false, TextAlignment.LEFT);
                table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Line2, false, TextAlignment.LEFT);
                table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Town, false, TextAlignment.LEFT);

                if (IsAntigenDayTwo(reportData))
                {
                    table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Postcode, false, TextAlignment.LEFT);
                    table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Phone, false, TextAlignment.LEFT);
                    table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Email, false, TextAlignment.LEFT);
                }
                else
                {
                    table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Country, false, TextAlignment.LEFT);
                    table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Postcode, false, TextAlignment.LEFT);
                }
            }
            else
            {
                table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Name, false, TextAlignment.LEFT);
                table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Line1, false, TextAlignment.LEFT);
                table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Line2, false, TextAlignment.LEFT);
                table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Town, false, TextAlignment.LEFT);

                if (IsAntigenDayTwo(reportData))
                {
                    table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Postcode, false, TextAlignment.LEFT);
                    table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Phone, false, TextAlignment.LEFT);
                    table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Email, false, TextAlignment.LEFT);
                }
                else
                {
                    table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Country, false, TextAlignment.LEFT);
                    table.AddCellToTable(font, fontColour, fontSize, reportData.ReportAddress.Postcode, false, TextAlignment.LEFT);
                }
            }

            return table;
        });
    }

    /// <summary>
    /// Creates the additional information which states data about the report.
    /// </summary>
    /// <param name="reportData">The object that will store the data to be displayed.</param>
    /// <returns>A paragraph that contains the information.</returns>
    private async Task<Table> CreateAdditionalInformation(ReportData reportData)
    {
        //Setup fonts for report table.
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 10.0f;

        return await Task.Run(() =>
        {
            float marginTop = IsAntigenDayTwo(reportData) ? -132.0f : -115.0f;

            Table table = new Table(new float[] { 1, 1 })
                .SetWidth(UnitValue.CreatePercentValue(50))
                .SetMarginTop(marginTop)
                .SetHorizontalAlignment(HorizontalAlignment.RIGHT);

            TryAddURN(ref table, reportData, font, fontColour, fontSize);
            TryAddGender(ref table, reportData, font, fontColour, fontSize);
            TryAddDateOfReceipt(ref table, reportData, font, fontColour, fontSize);
            TryAddDateOfReport(ref table, reportData, font, fontColour, fontSize);
            TryAddSwabDate(ref table, reportData, font, fontColour, fontSize);
            TryAddPassportNumber(ref table, reportData, font, fontColour, fontSize);
            TryAddNationality(ref table, reportData, font, fontColour, fontSize);
            TryAddBlankCells(ref table, reportData, font, fontColour, fontSize);

            return table;
        });
    }

    /// <summary>
    /// Creates the table that will store the required test data.
    /// </summary>
    /// <param name="reportData">The test data that will be used to create the table.</param>
    /// <returns>A table that will be added to the PDF document.</returns>
    private async Task<Table> CreateMultipleResultReportTable(ReportData reportData)
    {
        return await Task.Run(() =>
        {
            Table table;
            if (IsInternationalArrival(reportData.TemplateOption))
            {
                table = new Table(new float[] { 1, 1, 3, 1, 1, 1 })
                    .SetWidth(UnitValue.CreatePercentValue(100))
                    .SetMarginTop(10.0f);
            }
            else
            {
                table = new Table(new float[] { 1, 3, 1, 1, 1 })
                    .SetWidth(UnitValue.CreatePercentValue(100))
                    .SetMarginTop(10.0f);
            }

            AddReportHeaders(ref table, reportData.TemplateOption);
            AddReportRows(ref table, reportData);

            return table;
        });
    }

    private async Task<Table> CreateAntiGenResultTable(ReportData reportData)
    {
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color backgroundColour = new DeviceRgb(217, 217, 217);
        Color fontColor = ColorConstants.BLACK;
        float fontSize = 10.0f;

        return await Task.Run(() =>
        {
            Table table = new Table(new float[] { 1, 1, 1, 2 })
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(10.0f);

            string testKit = (_pdfConfig.IsFlowFlex) ? "FlowFlex SARS-CoV-2 Rapid Antigen Test" : "Roche SARS-CoV-2 Rapid Antigen Test";

            table.AddCellToTable(font, fontColor, fontSize, "URN", true, backgroundColour: backgroundColour);
            table.AddCellToTable(font, fontColor, fontSize, "Target Name", true, backgroundColour: backgroundColour);
            table.AddCellToTable(font, fontColor, fontSize, "Result", true, backgroundColour: backgroundColour);
            table.AddCellToTable(font, fontColor, fontSize, "Test Kit", true, backgroundColour: backgroundColour);

            table.AddCellToTable(font, fontColor, fontSize, reportData.Sample.LabelId, true);
            table.AddCellToTable(font, fontColor, fontSize, "SARS-CoV-2", true);
            table.AddCellToTable(font, fontColor, fontSize, reportData.Urn.CombinedResult, true);
            table.AddCellToTable(font, fontColor, fontSize, testKit, true);

            return table;
        });
    }

    private async Task<Table> CreateSingleResultReportTableForCoronaFocus(ReportData reportData)
    {
        return await Task.Run(() =>
        {
            Table table = new Table(new float[] { 1, 1, 1, 1 })
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(10.0f);

            AddCoronaFocusSingleResultReportHeaders(ref table);
            AddCoronaFocusSingleResultReportRows(ref table, reportData);

            return table;
        });
    }

    private async Task<Table> CreateSingleResultReportTableForMobileLab(ReportData reportData)
    {
        return await Task.Run(() =>
        {
            Table table = new Table(new float[] { 1, 3, 1, 1 })
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(10.0f);

            if (IsGbOlympicsRemote(reportData))
            {
                table = new Table(new float[] { 1, 3, 1, 1, 1 })
                    .SetWidth(UnitValue.CreatePercentValue(100))
                    .SetMarginTop(10.0f);

                AddTeamGbLabSingleResultReportHeaders(ref table);
                AddTeamGBLabSingleResultReportRows(ref table, reportData);
            }
            else
            {
                AddMobileLabSingleResultReportHeaders(ref table);
                AddMobileLabSingleResultReportRows(ref table, reportData);
            }

            return table;
        });
    }

    private async Task<Table> CreateReportTitle(ReportData reportData)
    {
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 16.0f;

        return await Task.Run(() =>
        {
            string reportTitle = GetReportTitle(reportData.TemplateOption);

            Table table = new Table(new float[] { 1 })
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(2.0f);

            table.AddCellToTable(font, fontColour, fontSize, $"*{reportTitle}*", false);

            return table;
        });
    }

    /// <summary>
    /// Creates the conditional statement that will show based on the results in the report.
    /// </summary>
    /// <param name="reportData"></param>
    /// <returns>A paragraph that contains the information.</returns>
    private async Task<Table> CreateStatement(ReportData reportData)
    {
        //Setup fonts for report table.
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 9.0f;

        return await Task.Run(() =>
        {
            Table table = new Table(new float[] { 1, 1, 3, 1, 1, 1 })
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(5.0f);

            table.AddCellToTable(font, fontColour, fontSize,
                ReplacePlaceholderVariables(reportData.Statement.Content, reportData.TestRegistration), false, TextAlignment.LEFT);

            return table;
        });
    }

    /// <summary>
    /// Creates the test details that will be shown in the report.
    /// </summary>
    /// <param name="reportData">The object that will store the data to be displayed.</param>
    /// <returns>A paragraph that contains the information.</returns>
    private async Task<Table> CreateTestDetails(ReportData reportData)
    {
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 9.0f;

        return await Task.Run(() =>
        {
            Table table = new Table(new float[] { 1, 1 })
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(15.0f);

            if (reportData?.Statement?.TypeOfTestContent != null)
                table.AddCellToTable(font, fontColour, fontSize, reportData.Statement.TypeOfTestContent, false, TextAlignment.LEFT,
                    rowSpan: 1, colSpan: 2, marginBottom: 5.0f);
            if (reportData?.Statement?.TechnicalNoteContent != null)
                table.AddCellToTable(font, fontColour, fontSize, reportData.Statement.TechnicalNoteContent, false, TextAlignment.LEFT,
                    rowSpan: 1, colSpan: 2, marginBottom: 10.0f);

            if (ShowHcpStatement(reportData.Sample.LabelId, reportData.Urn?.Corporate?.ShowHcpStatement ?? true))
                table.AddCellToTable(font, fontColour, fontSize,
                    "Sample collection was conducted by a Health Care Practitioner (HCP). In locations where Randox Health are responsible for sample collection, any samples collected from individuals under the age of 18 will be carried out by the accompanying parent or guardian under the supervision of a HCP.",
                    false, TextAlignment.LEFT, rowSpan: 1, colSpan: 2, marginBottom: 10.0f);

            return table;
        });
    }

    private async Task<Table> CreateURNVoidStatememt(ReportData reportData)
    {
        //Setup fonts for report table.
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 9.0f;

        return await Task.Run(() =>
        {
            Table table = new Table(new float[] { 1, 1, 3, 1, 1, 1 })
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(30.0f);

            table.AddCellToTable(font, fontColour, fontSize,
                ReplacePlaceholderVariables(reportData.VoidMessage, reportData.TestRegistration), false);

            return table;
        });
    }

    /// <summary>
    /// Creates the additional information which states data about the report.
    /// </summary>
    /// <param name="doc">The document that is being created for the report.</param>
    /// <param name="ps">The page size of the report that will be created.</param>
    /// <returns>A paragraph that contains the information.</returns>
    private async Task<Table> CreateFooter(Document doc, PageSize ps, ReportData reportData)
    {
        PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        Color fontColour = ColorConstants.BLACK;
        float boldFontSize = 12.0f;

        return await Task.Run(() =>
        {
            Table table = new Table(new float[] { 1, 1, 1 })
                .UseAllAvailableWidth()
                .SetFixedPosition(doc.GetLeftMargin(), doc.GetBottomMargin(), ps.GetWidth() - doc.GetLeftMargin() - doc.GetRightMargin());

            TryAddFooterContactContent(ref table, reportData);
            int initialRclsLogoWidth = TryAddFooterAddressContent(ref table, reportData);
            TryAddRCLSLogo(ref table, reportData, initialRclsLogoWidth);

            // Add end of report text.
            table.AddCellToTable(boldFont, fontColour, boldFontSize, "- End of Report -", false, colSpan: 3);

            // Add base footer content.
            AddBaseFooterContent(ref table, reportData);

            return table;
        });
    }

    private void TryAddRCLSLogo(ref Table table, ReportData reportData, int initialWidth = 1)
    {
        if (IsPrivateUser(reportData) && !IsAntiGen(reportData) && !IsAntigenDayTwo(reportData) && !IsMobileLab(reportData))
        {
            if (reportData.Footer.LogoBase64 is not null)
            {
                table.AddCell(new Cell(1, initialWidth)
                    .Add(
                        new Image(ImageDataFactory.Create(Convert.FromBase64String(reportData.Footer.LogoBase64)))
                            .SetHeight(UnitValue.CreatePercentValue(50))
                            .SetWidth(UnitValue.CreatePercentValue(50))
                            .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                    )
                    .SetWidth(UnitValue.CreatePercentValue(25))
                    .SetBorder(Border.NO_BORDER)
                );
            }
        }
        else
            table.AddCell(new Cell().SetBorder(Border.NO_BORDER));
    }

    private void AddBaseFooterContent(ref Table table, ReportData reportData)
    {
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 8.0f;

        table.AddCellToTable(font, fontColour, fontSize, reportData.Footer.FooterContent, false, TextAlignment.LEFT, colSpan: 3);
    }

    /// <summary>
    /// Creates and adds all of the rows for the report table.
    /// </summary>
    /// <param name="table">The table that the rows will be added to.</param>
    /// <param name="reportData"></param>
    private void TryAddFooterContactContent(ref Table table, ReportData reportData)
    {
        //Setup fonts for report table.
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 9.0f;

        string contactContent = "";

        if (IsMobileLab(reportData))
            contactContent = reportData.Footer.ContactDetails;
        else
        {
            if (IsGbOlympics(reportData) || IsGbOlympicsRemote(reportData))
            {
                if (reportData.Footer.LogoBase64 is not null)
                {
                    table.AddCell(new Cell(1, 3)
                        .Add(
                            new Image(ImageDataFactory.Create(Convert.FromBase64String(reportData.Footer.LogoBase64)))
                                .SetHeight(UnitValue.CreatePercentValue(25))
                                .SetWidth(UnitValue.CreatePercentValue(25))
                                .SetHorizontalAlignment(HorizontalAlignment.LEFT)
                        )
                        .SetBorder(Border.NO_BORDER)
                    );
                }
            }

            contactContent = reportData.Footer.ContactDetails;
        }

        table.AddCellToTable(font, fontColour, fontSize, contactContent, false, TextAlignment.LEFT);
    }

    private int TryAddFooterAddressContent(ref Table table, ReportData reportData)
    {
        //Setup fonts for report table.
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 9.0f;

        StringBuilder addressBuilder = new StringBuilder();
        int cellWidth = 1;

        if (IsPrivateUser(reportData) && !IsAntiGen(reportData) && !IsAntigenDayTwo(reportData))
        {
            // Testing Location
            if (!IsGbOlympicsRemote(reportData))
            {
                addressBuilder.Clear();
                if (reportData.Urn.AccessionLocation != null && reportData.Lab == null)
                {
                    addressBuilder.AppendLine("*Accessioning* *Location*");
                    addressBuilder.AppendLine(reportData.Urn.AccessionLocation.AddressLine1);

                    if (reportData.Urn.AccessionLocation.AddressLine2 != null)
                        addressBuilder.AppendLine(reportData.Urn.AccessionLocation.AddressLine2);

                    addressBuilder.AppendLine($"{reportData.Urn.AccessionLocation.City}, {reportData.Urn.AccessionLocation.Country}");
                    addressBuilder.AppendLine(reportData.Urn.AccessionLocation.Postcode);
                }
                else if (reportData.Lab != null)
                {
                    addressBuilder.AppendLine("*Testing* *Location*");
                    addressBuilder.AppendLine($"RCLS {reportData.Lab.Name}");
                    addressBuilder.AppendLine(reportData.Lab.AddressLine1);

                    if (reportData.Lab.AddressLine2 != null)
                        addressBuilder.AppendLine(reportData.Lab.AddressLine2);

                    addressBuilder.AppendLine(reportData.Lab.City);
                    addressBuilder.AppendLine(reportData.Lab.Country.Name);
                    addressBuilder.AppendLine(reportData.Lab.Postcode);
                }
                else
                {
                    addressBuilder.AppendLine("*Testing* *Location*");
                    addressBuilder.AppendLine("RCLS-Testing Labs");
                    addressBuilder.AppendLine("30 Randalstown Road");
                    addressBuilder.AppendLine("Antrim BT41 4LF");
                }

                table.AddCellToTable(font, fontColour, fontSize, addressBuilder.ToString(), false, TextAlignment.LEFT, colSpan: cellWidth);

                return 1;
            }

            return 2;
        }

        if (IsAntiGen(reportData) || IsAntigenDayTwo(reportData) || IsMobileLab(reportData) || IsGbOlympicsRemote(reportData))
        {
            addressBuilder.Clear();
            if (IsAntigenDayTwo(reportData) && reportData.Lab == null)
            {
                addressBuilder.AppendLine("*Accessioning* *Location*");
                addressBuilder.AppendLine(reportData.Urn.AccessionLocation.AddressLine1);

                if (reportData.Urn.AccessionLocation.AddressLine2 != null)
                    addressBuilder.AppendLine(reportData.Urn.AccessionLocation.AddressLine2);

                addressBuilder.AppendLine($"{reportData.Urn.AccessionLocation.City}, {reportData.Urn.AccessionLocation.Country}");
                addressBuilder.AppendLine(reportData.Urn.AccessionLocation.Postcode);
            }
            else
            {
                addressBuilder.AppendLine("*Testing* *Location*");
                addressBuilder.AppendLine($"RCLS {reportData.Lab.Name}");
                addressBuilder.AppendLine(reportData.Lab.AddressLine1);

                if (reportData.Lab.AddressLine2 != null)
                    addressBuilder.AppendLine(reportData.Lab.AddressLine2);

                addressBuilder.AppendLine(reportData.Lab.City);
                addressBuilder.AppendLine(reportData.Lab.Country.Name);
                addressBuilder.AppendLine(reportData.Lab.Postcode);
            }

            cellWidth = IsGbOlympicsRemote(reportData) ? 1 : 2;

            table.AddCellToTable(font, fontColour, fontSize, addressBuilder.ToString(), false, TextAlignment.LEFT, colSpan: cellWidth);
        }

        return 1;
    }

    private void TryAddURN(ref Table table, ReportData reportData, PdfFont font, Color fontColour, float fontSize)
    {
        table.AddCellToTable(font, fontColour, fontSize, "URN:", false, TextAlignment.LEFT);
        table.AddCellToTable(font, fontColour, fontSize, reportData.Sample?.LabelId ?? "Not Provided", false, TextAlignment.LEFT);
    }

    private void TryAddGender(ref Table table, ReportData reportData, PdfFont font, Color fontColour, float fontSize)
    {
        string gender = !string.IsNullOrWhiteSpace(reportData.TestRegistration.Gender)
            ? reportData.TestRegistration.Gender
            : "Not Provided";
        table.AddCellToTable(font, fontColour, fontSize, "Gender:", false, TextAlignment.LEFT);
        table.AddCellToTable(font, fontColour, fontSize, gender, false, TextAlignment.LEFT);
    }

    private void TryAddDateOfReceipt(ref Table table, ReportData reportData, PdfFont font, Color fontColour, float fontSize)
    {
        string dateOfReceipt = (reportData.Urn.ArrivalDate != null)
            ? ((DateTime)reportData.Urn.ArrivalDate).ToString("dd-MMM-yyyy") ?? "Not Provided"
            : "Not Provided";
        table.AddCellToTable(font, fontColour, fontSize, "Date Of Receipt:", false, TextAlignment.LEFT);
        table.AddCellToTable(font, fontColour, fontSize, dateOfReceipt, false, TextAlignment.LEFT);
    }

    private void TryAddDateOfReport(ref Table table, ReportData reportData, PdfFont font, Color fontColour, float fontSize)
    {
        string dateOfReportFormat = DateFormatHasHours(reportData) ? "dd-MMM-yyyy HH:mm" : "dd-MMM-yyyy";
        DateTime formattedDateOfReport = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _gmtTimeZone);

        table.AddCellToTable(font, fontColour, fontSize, "Date Of Report:", false, TextAlignment.LEFT);
        table.AddCellToTable(font, fontColour, fontSize, formattedDateOfReport.ToString(dateOfReportFormat), false, TextAlignment.LEFT);
    }

    private void TryAddSwabDate(ref Table table, ReportData reportData, PdfFont font, Color fontColour, float fontSize)
    {
        DateTime formattedSwabDate = TimeZoneInfo.ConvertTimeFromUtc(reportData.TestRegistration.SampleCollectedDate, _gmtTimeZone);

        if (ShowSwabDate(reportData))
        {
            string swabDateFormat = (ShowSwabDateAndTime(reportData)) ? "dd-MMM-yyyy HH:mm" : "dd-MMM-yyyy";

            table.AddCellToTable(font, fontColour, fontSize, "Swab Date:", false, TextAlignment.LEFT);
            table.AddCellToTable(font, fontColour, fontSize, formattedSwabDate.ToString(swabDateFormat), false, TextAlignment.LEFT);
        }
        else
        {
            _additionalInformationBlanks++;
        }
    }

    private void TryAddPassportNumber(ref Table table, ReportData reportData, PdfFont font, Color fontColour, float fontSize)
    {
        if (IsPrivateUser(reportData) && ShowPassportNumber(reportData.Urn.PassportNumber))
        {
            table.AddCellToTable(font, fontColour, fontSize, "Passport Number:", false, TextAlignment.LEFT);
            table.AddCellToTable(font, fontColour, fontSize, reportData.Urn.PassportNumber ?? "Not Provided", false, TextAlignment.LEFT);
        }
        else
        {
            _additionalInformationBlanks++;
        }
    }

    private void TryAddNationality(ref Table table, ReportData reportData, PdfFont font, Color fontColour, float fontSize)
    {
        if (IsPrivateUser(reportData) && ShowNationality(reportData.Urn.Nationality))
        {
            table.AddCellToTable(font, fontColour, fontSize, "Nationality:", false, TextAlignment.LEFT);
            table.AddCellToTable(font, fontColour, fontSize, reportData.Urn.Nationality.Name ?? "Not Provided", false, TextAlignment.LEFT);
        }
        else
        {
            _additionalInformationBlanks++;
        }
    }

    private void TryAddBlankCells(ref Table table, ReportData reportData, PdfFont tableFont, Color tableFontColor, float tableFontSize)
    {
        var numOfBlanks = (_additionalInformationBlanks > _additionalInformationBlanksMax)
            ? _additionalInformationBlanksMax
            : _additionalInformationBlanks;

        table.SetMarginBottom(numOfBlanks * 10.0f);
    }

    /// <summary>
    /// Creates and adds the table headers to the report table.
    /// </summary>
    /// <param name="table">The table that the headers will be added.</param>
    /// <param name="template"></param>
    private void AddReportHeaders(ref Table table, TemplateOptions template)
    {
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        Color backgroundColour = new DeviceRgb(217, 217, 217);
        float fontSize = 10.0f;

        //Create header columns.
        table.AddCellToTable(font, fontColour, fontSize, "URN", true, backgroundColour: backgroundColour);
        if (IsInternationalArrival(template))
        {
            table.AddCellToTable(font, fontColour, fontSize, "Test Type", true, backgroundColour: backgroundColour);
        }

        table.AddCellToTable(font, fontColour, fontSize, "Target Name", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "Result", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "CT Value", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "Test Kit", true, backgroundColour: backgroundColour);
    }

    /// <summary>
    /// Creates and adds all of the rows for the report table.
    /// </summary>
    /// <param name="table">The table that the rows will be added to.</param>
    /// <param name="reportData">The tests that will be used to populate the rows.</param>
    private void AddReportRows(ref Table table, ReportData reportData)
    {
        //Setup fonts for report table.
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 10.0f;

        foreach (Result result in reportData.FilterResults())
        {
            table.AddCellToTable(font, fontColour, fontSize, reportData.Sample.LabelId ?? "Not Provided", true);
            if (IsInternationalArrival(reportData.TemplateOption))
            {
                string columnTwoValue = reportData.Urn.TestToReleaseType?.Name ?? "N/A";
                table.AddCellToTable(font, fontColour, fontSize, columnTwoValue, true);
            }

            table.AddCellToTable(font, fontColour, fontSize, reportData.GetTestName(result.ReportedName), true);

            string content = result.FormattedEntry ?? "Not Provided";

            if (content.ToUpper() == "PLOD")
                content = "Positive";

            table.AddCellToTable(font, fontColour, fontSize, content, true);

            if (result.ReportedName == RequiredTests.Covid1TestName)
            {
                table.AddCellToTable(font, fontColour, fontSize, reportData.Urn.COVIDT1CTResult ?? "N/A", true);
                AddTestColumnForCTValue(ref table, reportData, font, fontColour, fontSize);
            }
            else if (result.ReportedName == RequiredTests.Covid2TestName)
            {
                table.AddCellToTable(font, fontColour, fontSize, reportData.Urn.COVIDT2CTResult ?? "N/A", true);
                AddTestColumnForCTValue(ref table, reportData, font, fontColour, fontSize);
            }
            else
            {
                table.AddCellToTable(font, fontColour, fontSize, "N/A", true);
                AddTestColumnForCTValue(ref table, reportData, font, fontColour, fontSize);
            }
        }
    }

    private void AddTestColumnForCTValue(ref Table table, ReportData reportData, PdfFont font, Color fontColour, float fontSize)
    {
        string testMethodName = "Randox COVID-19 qPCR";

        if (IsMobileLab(reportData) || IsGbOlympicsRemote(reportData))
            testMethodName = "Bosch Vivalytic SARS CoV-2 RT-PCR";

        // If kit type is Perkin Elmer.
        if (reportData.KitType == 1)
            testMethodName = "PerkinElmer® SARS-CoV-2 RT-qPCR";

        table.AddCellToTable(font, fontColour, fontSize, testMethodName, true);
    }

    private void AddCoronaFocusSingleResultReportHeaders(ref Table table)
    {
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        Color backgroundColour = new DeviceRgb(217, 217, 217);
        float fontSize = 10.0f;

        //Create header columns.
        table.AddCellToTable(font, fontColour, fontSize, "URN", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "Test Type", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "Target Name", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "Result", true, backgroundColour: backgroundColour);
    }

    private void AddCoronaFocusSingleResultReportRows(ref Table table, ReportData reportData)
    {
        //Setup fonts for report table.
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 10.0f;

        if (reportData.FilterResults().Count > 0)
        {
            Result result = reportData.FilterResults()[0];

            table.AddCellToTable(font, fontColour, fontSize, reportData.Sample.LabelId ?? "Not Provided", true);
            table.AddCellToTable(font, fontColour, fontSize, reportData.Urn.TestToReleaseType.Name ?? "N/A", true);
            table.AddCellToTable(font, fontColour, fontSize, "COVID 19", true);
            table.AddCellToTable(font, fontColour, fontSize, reportData.Urn.CombinedResult, true);
        }
    }

    private void AddMobileLabSingleResultReportHeaders(ref Table table)
    {
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        Color backgroundColour = new DeviceRgb(217, 217, 217);
        float fontSize = 10.0f;

        //Create header columns.
        table.AddCellToTable(font, fontColour, fontSize, "URN", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "Target Name", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "Result", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "Test Kit", true, backgroundColour: backgroundColour);
    }

    private static void AddTeamGbLabSingleResultReportHeaders(ref Table table)
    {
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        Color backgroundColour = new DeviceRgb(217, 217, 217);
        float fontSize = 10.0f;

        //Create header columns.
        table.AddCellToTable(font, fontColour, fontSize, "URN", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "Target Name", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "Result", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "CQ Value", true, backgroundColour: backgroundColour);
        table.AddCellToTable(font, fontColour, fontSize, "Test Kit", true, backgroundColour: backgroundColour);
    }

    private void AddMobileLabSingleResultReportRows(ref Table table, ReportData reportData)
    {
        //Setup fonts for report table.
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 10.0f;

        if (reportData.FilterResults().Count > 0)
        {
            Result result = reportData.FilterResults()[0];

            table.AddCellToTable(font, fontColour, fontSize, reportData.Sample.LabelId ?? "Not Provided", true);
            table.AddCellToTable(font, fontColour, fontSize, "E-gene", true);

            string content = result.FormattedEntry ?? "Not Provided";
            if (content.ToUpper() == "PLOD")
                content = "Positive";

            table.AddCellToTable(font, fontColour, fontSize, content, true);

            if (result.ReportedName == RequiredTests.Covid1TestName)
            {
                AddTestColumnForCTValue(ref table, reportData, font, fontColour, fontSize);
            }
            else if (result.ReportedName == RequiredTests.Covid2TestName)
            {
                AddTestColumnForCTValue(ref table, reportData, font, fontColour, fontSize);
            }
            else
            {
                AddTestColumnForCTValue(ref table, reportData, font, fontColour, fontSize);
            }
        }
    }

    private void AddTeamGBLabSingleResultReportRows(ref Table table, ReportData reportData)
    {
        //Setup fonts for report table.
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        Color fontColour = ColorConstants.BLACK;
        float fontSize = 10.0f;

        if (reportData.FilterResults().Count > 0)
        {
            Result result = reportData.FilterResults()[0];

            table.AddCellToTable(font, fontColour, fontSize, reportData.Sample.LabelId ?? "Not Provided", true);
            table.AddCellToTable(font, fontColour, fontSize, "E-gene", true);

            string content = result.FormattedEntry ?? "Not Provided";
            if (content.ToUpper() == "PLOD")
                content = "Positive";

            table.AddCellToTable(font, fontColour, fontSize, content, true);

            if (result.ReportedName == RequiredTests.Covid1TestName)
            {
                table.AddCellToTable(font, fontColour, fontSize, reportData.Urn.COVIDT1CTResult ?? "N/A", true);
                AddTestColumnForCTValue(ref table, reportData, font, fontColour, fontSize);
            }
            else if (result.ReportedName == RequiredTests.Covid2TestName)
            {
                AddTestColumnForCTValue(ref table, reportData, font, fontColour, fontSize);
            }
            else
            {
                AddTestColumnForCTValue(ref table, reportData, font, fontColour, fontSize);
            }
        }
    }

    private string ReplacePlaceholderVariables(string statementContent, TestRegistration registration)
    {
        //Replace first name placeholder with first name.
        statementContent = statementContent.Replace("{FIRSTNAME}", registration.FirstName);
        //Replace last name placeholder with last name.
        statementContent = statementContent.Replace("{LASTNAME}", registration.LastName);

        //Replace date of birth placeholder with date of birth.
        TimeZoneInfo gmt = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        var dob = registration.DateOfBirth;
        if (dob != null)
            dob = TimeZoneInfo.ConvertTimeFromUtc((DateTime)registration.DateOfBirth, gmt);
        statementContent =
            statementContent.Replace("{DATE_OF_BIRTH}", (dob != null) ? ((DateTime)dob).ToString("dd-MMM-yyyy") : "Not Provided");

        //Replace contact number placeholder with contact number.
        statementContent = statementContent.Replace("{CONTACT_NUMBER}", registration?.PhoneNumber ?? "Not Provided");

        //Replace booking reference number placeholder with booking reference number.
        statementContent = statementContent.Replace("{BOOKING_REFERENCE}", registration?.Urn?.BookingReference ?? "Not Provided");

        return statementContent;
    }

    private bool IsPrivateUser(ReportData reportData)
    {
        return reportData.TemplateOption == TemplateOptions.Private_CT
               || reportData.TemplateOption == TemplateOptions.Private_Updated
               || reportData.TemplateOption == TemplateOptions.Egypt_Report
               || reportData.TemplateOption == TemplateOptions.International_Arrivals
               || reportData.TemplateOption == TemplateOptions.Corona_Focus
               || reportData.TemplateOption == TemplateOptions.Mobile_Lab
               || reportData.TemplateOption == TemplateOptions.Test_To_Release
               || reportData.TemplateOption == TemplateOptions.Randox_Portugal
               || reportData.TemplateOption == TemplateOptions.GB_Olympics
               || reportData.TemplateOption == TemplateOptions.GB_Olympics_Remote
               || IsAntiGen(reportData)
               || IsAntigenDayTwo(reportData);
    }

    private bool ShowTestContent(ReportData reportData)
    {
        return reportData.TemplateOption == TemplateOptions.National_Health
               || reportData.TemplateOption == TemplateOptions.Private_CT
               || reportData.TemplateOption == TemplateOptions.Private_Updated
               || reportData.TemplateOption == TemplateOptions.Egypt_Report
               || reportData.TemplateOption == TemplateOptions.International_Arrivals
               || reportData.TemplateOption == TemplateOptions.Corona_Focus
               || reportData.TemplateOption == TemplateOptions.Mobile_Lab
               || reportData.TemplateOption == TemplateOptions.Test_To_Release
               || reportData.TemplateOption == TemplateOptions.Randox_Portugal
               || reportData.TemplateOption == TemplateOptions.GB_Olympics
               || reportData.TemplateOption == TemplateOptions.GB_Olympics_Remote
               || IsAntiGen(reportData)
               || IsAntigenDayTwo(reportData);
    }

    private bool IsAntiGen(ReportData reportData) => reportData.TemplateOption == TemplateOptions.Antigen;
    private bool IsMobileLab(ReportData reportData) => reportData.TemplateOption == TemplateOptions.Mobile_Lab;
    private bool IsInternationalArrival(TemplateOptions template) => template == TemplateOptions.International_Arrivals;
    private bool IsCoronaFocus(TemplateOptions template) => template == TemplateOptions.Corona_Focus;
    private bool IsGbOlympics(ReportData reportData) => reportData.TemplateOption == TemplateOptions.GB_Olympics;
    private bool IsGbOlympicsRemote(ReportData reportData) => reportData.TemplateOption == TemplateOptions.GB_Olympics_Remote;
    private bool IsAntigenDayTwo(ReportData reportData) => reportData.TemplateOption == TemplateOptions.Antigen_Day_Two;

    private bool ShowSwabDate(ReportData reportData)
    {
        return reportData.TemplateOption == TemplateOptions.Private_Updated
               || reportData.TemplateOption == TemplateOptions.Egypt_Report
               || reportData.TemplateOption == TemplateOptions.International_Arrivals
               || reportData.TemplateOption == TemplateOptions.Corona_Focus
               || reportData.TemplateOption == TemplateOptions.Mobile_Lab
               || reportData.TemplateOption == TemplateOptions.Test_To_Release
               || reportData.TemplateOption == TemplateOptions.Randox_Portugal
               || reportData.TemplateOption == TemplateOptions.GB_Olympics
               || reportData.TemplateOption == TemplateOptions.GB_Olympics_Remote
               || IsAntiGen(reportData)
               || IsAntigenDayTwo(reportData);
    }

    private bool ShowSwabDateAndTime(ReportData reportData)
    {
        return reportData.TemplateOption == TemplateOptions.Egypt_Report
               || reportData.TemplateOption == TemplateOptions.International_Arrivals
               || reportData.TemplateOption == TemplateOptions.Corona_Focus
               || reportData.TemplateOption == TemplateOptions.Mobile_Lab
               || reportData.TemplateOption == TemplateOptions.Test_To_Release
               || reportData.TemplateOption == TemplateOptions.Randox_Portugal
               || reportData.TemplateOption == TemplateOptions.GB_Olympics
               || reportData.TemplateOption == TemplateOptions.GB_Olympics_Remote
               || IsAntiGen(reportData)
               || IsAntigenDayTwo(reportData);
    }

    private static bool ShowPassportNumber(string passportNumber)
    {
        if (string.IsNullOrWhiteSpace(passportNumber))
            return false;

        return passportNumber.ToUpperAndTrimAndRemoveWhiteSpace() != NotProvided;
    }

    private static bool ShowNationality(Nationality? nationality)
    {
        if (nationality == null)
            return false;
        if (string.IsNullOrWhiteSpace(nationality.Name))
            return false;

        return nationality.Name.ToUpperAndTrimAndRemoveWhiteSpace() != NotProvided;
    }

    private static bool ShowHcpStatement(string urn, bool forceHcpStatement = true)
    {
        return (urn.StartMatches("R6")
                || urn.StartMatches("PD")
                || urn.StartMatches("CN")
                || urn.StartMatches("CF")
                || urn.StartMatches("CG")
                || urn.StartMatches("G"))
               && forceHcpStatement;
    }

    private bool DateFormatHasHours(ReportData reportData)
    {
        return reportData.TemplateOption == TemplateOptions.Egypt_Report
               || reportData.TemplateOption == TemplateOptions.International_Arrivals
               || reportData.TemplateOption == TemplateOptions.Corona_Focus
               || reportData.TemplateOption == TemplateOptions.Mobile_Lab
               || reportData.TemplateOption == TemplateOptions.Randox_Portugal
               || reportData.TemplateOption == TemplateOptions.GB_Olympics
               || reportData.TemplateOption == TemplateOptions.GB_Olympics_Remote
               || IsAntiGen(reportData)
               || IsAntigenDayTwo(reportData);
    }

    /// <summary>
    /// Returns the report title based on the template provided.
    /// </summary>
    /// <param name="option"></param>
    /// <returns></returns>
    private static string GetReportTitle(TemplateOptions option)
    {
        return option switch
        {
            TemplateOptions.International_Arrivals => "Day 2 / Day 8 Test Result Certificate",
            TemplateOptions.Corona_Focus => "Day 2 / Day 8 Test Result Certificate",
            TemplateOptions.Test_To_Release => "Test to Release Certificate",
            TemplateOptions.Antigen_Day_Two => "Lateral Flow Day 2 Test Result Certificate",
            _ => "Results report / Certificate"
        };
    }
}