using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DetachService;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var time = DateTime.UtcNow;
        var logPath = $"{AppDomain.CurrentDomain.BaseDirectory}\\logs\\service\\{time:yyyy-MM-dd_H-mm}.txt";
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath)
            .CreateLogger();
        try
        {
            Log.Information("Starting up the service");
            var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }
        catch (Exception e)
        {
            Log.Fatal(e, e.Message);
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "DetachService";
            })
            .ConfigureServices(services =>
            {
                services.AddHostedService<Worker>();
            })
            .UseSerilog();
    }
}