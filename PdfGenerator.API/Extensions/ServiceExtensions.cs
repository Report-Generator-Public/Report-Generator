using DomainObjects.Constants;
using DomainObjects.Models.Config;
using Microsoft.OpenApi.Models;
using Pdf;

namespace PdfGenerator.API.Extensions;

public static class ServiceExtensions
{
    public static void ConfigureRequiredServices(this IServiceCollection services)
    {
        services.AddSwaggerGen(genOptions =>
        {
            genOptions.AddSecurityDefinition(GeneralConstants.ApiKeyHeader, new OpenApiSecurityScheme
            {
                Name = GeneralConstants.ApiKeyHeader,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "ApiKeyScheme",
                In = ParameterLocation.Header,
                Description = "ApiKey must appear in header"
            });

            genOptions.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = GeneralConstants.ApiKeyHeader
                        },
                        In = ParameterLocation.Header
                    },
                    Array.Empty<string>()
                }
            });
        });

        services.AddOptions<PdfConfig>().Configure<IConfiguration>((setting, configuration) =>
        {
            configuration.Bind("PdfConfig", setting);
        });
        
        services.AddOptions<ApiSecret>().Configure<IConfiguration>((setting, configuration) =>
        {
            configuration.Bind("ApiSecret", setting);
        });
        
        services.AddTransient<QrCodeGeneration>();
        services.AddTransient<ReportGeneration>();
    }
}