using System.Data.SqlClient;
using System.Text;
using Newtonsoft.Json;

namespace Automation.Services;

public static class JsonExtensions
{
    public static async Task<string> ReadJson(this SqlCommand cmd, CancellationToken cancellationToken)
    {
        var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var json = reader.ReadToJson();
        return json;
    }

    public static async Task<T> ReadJson<T>(this SqlCommand cmd)
        => await cmd.ReadJson<T>(null, CancellationToken.None);

    public static async Task<T> ReadJson<T>(this SqlCommand cmd, JsonSerializerSettings? settings)
        => await cmd.ReadJson<T>(settings, CancellationToken.None);

    public static async Task<T> ReadJson<T>(this SqlCommand cmd, CancellationToken cancellationToken)
        => await cmd.ReadJson<T>(null, cancellationToken);

    public static async Task<T> ReadJson<T>(this SqlCommand cmd, JsonSerializerSettings? settings, CancellationToken cancellationToken)
    {
        var json = await cmd.ReadJson(cancellationToken);
        var response = json.ParseJson<T>(settings);
        return response;
    }

    /// <summary>
    /// Converts SqlDataReader to json string
    /// </summary>
    /// <param name="reader">SqlDataReader</param>
    /// <param name="isArray">isArray specifies what type of json string to return. isArray = true -> '[]', isArray = false -> '{}'</param>
    /// <returns></returns>
    private static string ReadToJson(this SqlDataReader? reader, bool isArray = true)
    {
        var jsonResult = new StringBuilder();
        if (reader != null)
        {
            if (!reader.HasRows && isArray)
            {
                jsonResult.Append("[]");
            }
            else if (!reader.HasRows && !isArray)
            {
                jsonResult.Append("{}");
            }
            else
            {
                while (reader.Read())
                    jsonResult.Append(reader.GetValue(0));
            }
        }

        return jsonResult.ToString();
    }

    private static T ParseJson<T>(this string str, JsonSerializerSettings? settings)
        => JsonConvert.DeserializeObject<T>(str, settings);

    private static T ParseJson<T>(this string str)
        => JsonConvert.DeserializeObject<T>(str);

    private static object? ParseJson(this string str)
        => JsonConvert.DeserializeObject(str);
}