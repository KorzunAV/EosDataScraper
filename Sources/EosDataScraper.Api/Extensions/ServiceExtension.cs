using EosDataScraper.Common;
using EosDataScraper.Common.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EosDataScraper.Api.Extensions
{
    public static class ServiceExtension
    {
        public static void AddServices(this IServiceCollection services)
        {
            services.AddHostedService<QueuedHostedService>();
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

            services.AddSingleton<DbUpdateService, DbUpdateService>();
        }

        public static void RunOnceServices(this IApplicationBuilder app)
        {
            var q = app.ApplicationServices.GetService<IBackgroundTaskQueue>();

            var dbUpdateService = app.ApplicationServices.GetService<DbUpdateService>();

            q.QueueBackgroundWorkItem(async token =>
            {
                await dbUpdateService.StartAndWaitAsync(token);
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
