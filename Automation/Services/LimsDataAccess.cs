using System.Data;
using System.Data.SqlClient;
using Automation.Models.Settings;
using DomainObjects.Models;
using Microsoft.Extensions.Options;

namespace Automation.Services;

public sealed class LimsDataAccess
{
    private readonly LimsSettings _limsSettings;

    public LimsDataAccess(IOptions<LimsSettings> options)
    {
        _limsSettings = options.Value;
    }

    public async Task<List<ReportData>> GetResultsToGenerate()
    {
        using var conn = new SqlConnection(_limsSettings.ConnectionString);
        using var cmd = new SqlCommand("LIMS_READ_CovidResultsToReport", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 300;
        
        await conn.OpenAsync();
        var data = await cmd.ReadJson<List<ReportData>>();
        return data;
    }
}