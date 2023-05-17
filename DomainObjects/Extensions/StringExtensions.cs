namespace DomainObjects.Extensions;

public static class StringExtensions
{
    public static bool StartMatches(this string val1, string val2)
    {
        return val1.ToUpper().Trim().StartsWith(val2.ToUpper().Trim());
    }
    
    public static string ToUpperAndTrimAndRemoveWhiteSpace(this string value)
    {
        return value.ToUpper().Trim().Replace(" ", string.Empty);
    }
}