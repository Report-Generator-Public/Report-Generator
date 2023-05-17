namespace DomainObjects.Models;

public class ReportStatement
{
    /// <summary>
    /// The descriptive message to be given to be output for the title
    /// </summary>
    /// <value></value>
    public string Content { get; set; } = "";

    /// <summary>
    /// The text that will be shown to describe the test.
    /// </summary>
    /// <value></value>
    public string? TypeOfTestContent { get; set; } = null;

    /// <summary>
    /// The text that will be shown to store the technical note.
    /// </summary>
    /// <value></value>
    public string? TechnicalNoteContent { get; set; } = null;
}