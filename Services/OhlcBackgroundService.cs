using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TechnicalAnalyzer.Services;

namespace TechnicalAnalyzer.Services
{
    public class OhlcBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(60);

        public OhlcBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var nepseApiService = scope.ServiceProvider.GetRequiredService<NepseApiService>();
                    var tickers = await nepseApiService.GetStockTickersAsync();
                    foreach (var ticker in tickers)
                    {
                        try
                        {
                            await nepseApiService.GetHistoricalDataAsync(ticker, 5);
                        }
                        catch { }
                    }
                }
                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}