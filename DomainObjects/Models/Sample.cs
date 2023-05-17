using DomainObjects.Extensions;

namespace DomainObjects.Models;

public class Sample
{
    public string LabelId
    {
        get
        {
            return _LabelId.ToUpperAndTrimAndRemoveWhiteSpace();
        }
        set
        {
            _LabelId = (!string.IsNullOrEmpty(value)) ? value : "Not Provided";
        }
    }
    private string _LabelId { get; set; }
}