using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using P26_002_Pultral.Services;

namespace P26_002_Pultral
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ServiceProvider _provider = BuildServiceProvider();

        public static ErpDataService ErpData => _provider.GetRequiredService<ErpDataService>();
        public static DatabaseService Db     => _provider.GetRequiredService<DatabaseService>();
        public static LabelPrinterService Printer => _provider.GetRequiredService<LabelPrinterService>();
        public static LogBufferService Logs => _provider.GetRequiredService<LogBufferService>();

        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging(s => s.SetMinimumLevel(LogLevel.Warning));
            services.AddMemoryCache();
            services.Configure<GeniusClientOptions>(_ => { });
            services.AddSingleton<ClientApiService>();
            services.AddSingleton<ErpDataService>();
            services.AddSingleton<DatabaseService>();
            services.AddSingleton<LabelPrinterService>();
            services.AddSingleton<LogBufferService>();
            return services.BuildServiceProvider();
        }
    }
}