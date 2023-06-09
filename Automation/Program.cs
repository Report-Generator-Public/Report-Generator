using Automation.Models.Settings;
using Automation.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(s =>
    {
        s.AddLogging();
        s.AddTransient<LimsDataAccess>();
        s.AddTransient<BlobService>();
        s.AddTransient<FileShareService>();

        s
            .AddOptions<FunctionSettings>()
            .Configure<IConfiguration>((settings, config) => { config.Bind("FunctionSettings", settings); });

        s
            .AddOptions<LimsSettings>()
            .Configure<IConfiguration>((settings, config) => { config.Bind("LimsSettings", settings); });

        s
            .AddOptions<ReportGeneratorSettings>()
            .Configure<IConfiguration>((settings, config) => { config.Bind("ReportGeneratorSettings", settings); });

        s
            .AddOptions<StorageSettings>()
            .Configure<IConfiguration>((settings, config) => { config.Bind("StorageSettings", settings); });
        
        s
            .AddOptions<EventSettings>()
            .Configure<IConfiguration>((settings, config) => { config.Bind("EventSettings", settings); });
        
    })
    .Build();

host.Run();