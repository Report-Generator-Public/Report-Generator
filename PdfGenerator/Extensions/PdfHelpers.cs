using System.Text.RegularExpressions;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace Pdf.Extensions;

public static class PdfHelpers
{
    public static void AddCellToTable(this Table table, PdfFont font, Color fontColour, float fontSize, string content,
        bool hasBorder, TextAlignment? alignment = null, Color? backgroundColour = null, int colSpan = 1, int rowSpan = 1,
        bool underline = false, float marginTop = 0, float marginBottom = 0)
    {
        Paragraph paragraph = new Paragraph()
            .SetFont(font)
            .SetFontColor(fontColour)
            .SetFontSize(fontSize)
            .SetTextAlignment(alignment ?? TextAlignment.CENTER)
            .SetMarginBottom(marginBottom)
            .SetMarginTop(marginTop);

        paragraph.FormatContentAddToParagraph(content, font, fontSize, underline);

        Cell newCell = new Cell(rowSpan, colSpan)
            .Add(paragraph);

        if (!hasBorder)
            newCell.SetBorder(Border.NO_BORDER);

        if (backgroundColour != null)
            newCell.SetBackgroundColor(backgroundColour);

        table.AddCell(newCell);
    }

    private static void FormatContentAddToParagraph(this Paragraph statementParagraph, string statementContent, PdfFont font, float fontSize,
        bool underline = false)
    {
        var words = Regex.Split(statementContent, @"([\s^,]+)").ToList();
        words.RemoveAll(x => x == "\r");

        bool currentlyFormatting = false;
        string phrase = "";

        for (int i = 0; i < words.Count; i++)
        {
            string word = words[i];
            bool isBold = false;
            bool isUnderlined = false;
            bool hasFullStop = false;
            string wordColor = "BLACK";

            if (word.EndsWith('.'))
            {
                word = word.Substring(0, word.Length - 1);
                hasFullStop = true;
            }

            if (word.StartsWith('*') && (word.EndsWith('*') || word.EndsWith("*.") || word.EndsWith("*,") || word.EndsWith("*:")))
                isBold = true;

            //Remove the bolding symbol.
            word = word.Replace("*", string.Empty);

            if (currentlyFormatting)
            {
                phrase += word;
            }

            if (word.StartsWith('['))
            {
                currentlyFormatting = true;
                phrase += word.Substring(1, word.Length - 1);
            }

            if (word.EndsWith(']') && currentlyFormatting)
            {
                List<string> wordAndColor = phrase.Replace("[", string.Empty).Replace("]", string.Empty).Split(":").ToList();

                if (wordAndColor.Count > 1)
                    wordColor = wordAndColor[^1].ToUpper();

                if (wordAndColor.Count > 2)
                {
                    word = "";

                    for (int j = 0; j < wordAndColor.Count - 2; j++)
                    {
                        word += wordAndColor[j];
                    }

                    word = word.Replace("[", string.Empty).Replace("]", string.Empty);
                }
                else
                {
                    word = wordAndColor[0].Replace("[", string.Empty).Replace("]", string.Empty);
                }

                phrase = word;
                currentlyFormatting = false;
            }

            if (underline)
                isUnderlined = true;

            if (word.StartsWith("http") || word.StartsWith("www."))
            {
                isUnderlined = true;
                wordColor = "BLUE";
            }

            if (!currentlyFormatting)
            {
                AddWordToParagraph(ref statementParagraph, (string.IsNullOrEmpty(phrase)) ? word : phrase, font, fontSize, isBold,
                    isUnderlined, hasFullStop, wordColor);
                phrase = "";
            }
        }
    }

    private static void AddWordToParagraph(ref Paragraph paragraph, string word, PdfFont font, float fontSize, bool isBold = false,
        bool isUnderlined = false, bool hasFullStop = false, string color = "")
    {
        Color fontColor = GetColor(color);

        if (hasFullStop)
            word += ".";

        Text text = new Text(word);

        if (isBold)
            text.SetBold();

        if (isUnderlined)
            text.SetUnderline();

        text.SetFontColor(fontColor);
        text.SetFont(font);
        text.SetFontSize(fontSize);

        paragraph.Add(text);
    }
    
    //Get the right color using the string provided.
    private static Color GetColor(string color) => color switch
    {
        "BLACK" => ColorConstants.BLACK,
        "RED" => ColorConstants.RED,
        "BLUE" => ColorConstants.BLUE,
        "GREEN" => ColorConstants.GREEN,
        "" => ColorConstants.BLACK,
        _ => ColorConstants.BLACK
    };
}