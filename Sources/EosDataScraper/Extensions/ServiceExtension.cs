using EosDataScraper.Common;
using EosDataScraper.Common.Services;
using EosDataScraper.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EosDataScraper.Extensions
{
    public static class ServiceExtension
    {
        public static void AddServices(this IServiceCollection services)
        {
            services.AddHostedService<QueuedHostedService>();
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

            services.AddSingleton<NodeInfoService, NodeInfoService>();
            services.AddSingleton<DbUpdateService, DbUpdateService>();

            services.AddSingleton<DappRadarService, DappRadarService>();
            services.AddSingleton<DappComService, DappComService>();

            services.AddSingleton<TokenCostService, TokenCostService>();

            services.AddSingleton<ScraperService, ScraperService>();
        }

        public static void RunOnceServices(this IApplicationBuilder app)
        {
            var q = app.ApplicationServices.GetService<IBackgroundTaskQueue>();

            var dbUpdateService = app.ApplicationServices.GetService<DbUpdateService>();
            var nodeInfoService = app.ApplicationServices.GetService<NodeInfoService>();
            var dappRadarService = app.ApplicationServices.GetService<DappRadarService>();
            var dappComService = app.ApplicationServices.GetService<DappComService>();

            q.QueueBackgroundWorkItem(async token =>
            {
                await dbUpdateService.StartAndWaitAsync(token);
                await nodeInfoService.StartAndWaitAsync(token);
                await dappRadarService.StartAsync(token);
                await dappComService.StartAsync(token);
            });
        }

        public static void AddServicesToQueue(this IApplicationBuilder app)
        {
            var q = app.ApplicationServices.GetService<IBackgroundTaskQueue>();
            var scraper = app.ApplicationServices.GetService<ScraperService>();
            var priceUpdater = app.ApplicationServices.GetService<TokenCostService>();

            q.QueueBackgroundWorkItem(async token =>
            {
                await scraper.StartAsync(token);
                await priceUpdater.StartAsync(token);
            });
        }

        public static void AddLogger(this IApplicationBuilder app)
        {
            var configuration = app.ApplicationServices.GetService<IConfiguration>();
            var factory = app.ApplicationServices.GetService<ILoggerFactory>();

            factory.AddProvider(new TelegramLoggerProvider(configuration));
        }
    }
}
