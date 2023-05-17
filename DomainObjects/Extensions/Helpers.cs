using System.Runtime.CompilerServices;

namespace DomainObjects.Extensions;

public static class Helpers
{
    /// <summary>
    /// Static operation to get the name of the method calling this method
    /// </summary>
    /// <param name="name"></param>
    /// <returns>The name of the method that is currently being called</returns>
    public static string GetCallerMemberName([CallerMemberName] string name = "")
    {
        return name;
    }
}