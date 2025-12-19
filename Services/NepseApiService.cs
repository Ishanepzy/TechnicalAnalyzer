using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TechnicalAnalyzer.Models;
using System.Linq;
using System;
using TechnicalAnalyzer.Data;
using Microsoft.EntityFrameworkCore;

namespace TechnicalAnalyzer.Services
{
    public class NepseApiService
    {
        private readonly HttpClient _httpClient;
        private readonly OhlcDbContext _dbContext;

        public NepseApiService(HttpClient httpClient, OhlcDbContext dbContext)
        {
            _httpClient = httpClient;
            _dbContext = dbContext;
        }

        public async Task<StockDataModel> GetHistoricalDataAsync(string ticker, int candleRange =60)
        {
            var response = await _httpClient.GetAsync($"http://127.0.0.1:8000/DailyScripPriceGraph/{ticker}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var scripPrices = JsonSerializer.Deserialize<List<ScripPriceDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var model = new StockDataModel { Ticker = ticker, RawJson = json };

            if (scripPrices == null || scripPrices.Count ==0)
            {
                model.DataPoints = new List<StockDataPoint>();
                return model;
            }

            // Nepal Standard Time offset
            TimeSpan nepalOffset = TimeSpan.FromHours(5) + TimeSpan.FromMinutes(45);

            // Convert DTOs into trades using contractRate as price
            var trades = scripPrices.Select(dto =>
            {
                DateTime utcDate;
                try
                {
                    if (dto.Time >9999999999) // milliseconds
                        utcDate = DateTimeOffset.FromUnixTimeMilliseconds(dto.Time).UtcDateTime;
                    else
                        utcDate = DateTimeOffset.FromUnixTimeSeconds(dto.Time).UtcDateTime;
                }
                catch
                {
                    utcDate = DateTime.UtcNow;
                }
                // Convert to Nepal time
                var nepalDate = utcDate.Add(nepalOffset);
                return new Trade
                {
                    DateTime = nepalDate,
                    Price = dto.ContractRate,
                    Quantity = dto.ContractQuantity ??0m
                };
            }).ToList();

            // Group by selected time range (minutes)
            IEnumerable<IGrouping<DateTime, Trade>> groupedTrades;
            if (candleRange ==1)
            {
                groupedTrades = trades.GroupBy(t => new DateTime(t.DateTime.Year, t.DateTime.Month, t.DateTime.Day, t.DateTime.Hour, t.DateTime.Minute,0));
            } else {
                groupedTrades = trades.GroupBy(t => new DateTime(t.DateTime.Year, t.DateTime.Month, t.DateTime.Day, t.DateTime.Hour, (t.DateTime.Minute / candleRange) * candleRange,0));
            }
            var grouped = groupedTrades
                .Select(g => new StockDataPoint
                {
                    Date = g.Key,
                    Open = g.OrderBy(t => t.DateTime).First().Price,
                    High = g.Max(t => t.Price),
                    Low = g.Min(t => t.Price),
                    Close = g.OrderBy(t => t.DateTime).Last().Price,
                    Volume = (long)g.Sum(t => t.Quantity),
                    TimeLabel = g.Key.ToString("yyyy-MM-dd HH:mm")
                })
                .OrderBy(dp => dp.Date)
                .ToList();

            // Automatically store/update OHLC data in SQL Server
            await StoreOhlcDataAsync(ticker, grouped);


            model.DataPoints = grouped;
            return model;
        }

        public async Task<List<string>> GetStockTickersAsync()
        {
            var response = await _httpClient.GetAsync("http://127.0.0.1:8000/CompanyList\r\n");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var companies = JsonSerializer.Deserialize<List<CompanyInfo>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var tickers = companies.Select(c => c.Symbol).ToList();
            return tickers;
        }

        public async Task StoreOhlcDataAsync(string ticker, List<StockDataPoint> grouped)
        {
            foreach (var dp in grouped)
            {
                var existing = await _dbContext.OhlcDatas
                    .FirstOrDefaultAsync(o => o.Ticker == ticker && o.DateTime == dp.Date);
                if (existing == null)
                {
                    _dbContext.OhlcDatas.Add(new OhlcData
                    {
                        Ticker = ticker,
                        DateTime = dp.Date,
                        Open = dp.Open,
                        High = dp.High,
                        Low = dp.Low,
                        Close = dp.Close,
                        Volume = dp.Volume
                    });
                }
                else
                {
                    existing.Open = dp.Open;
                    existing.High = dp.High;
                    existing.Low = dp.Low;
                    existing.Close = dp.Close;
                    existing.Volume = dp.Volume;
                }
            }
            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<StockDataPoint>> GetOhlcDataFromDbAsync(string ticker, int candleRange = 60)
        {
            var ohlcList = await _dbContext.OhlcDatas
                .Where(o => o.Ticker == ticker)
                .OrderBy(o => o.DateTime)
                .ToListAsync();

            // Group by selected time range (minutes)
            IEnumerable<IGrouping<DateTime, OhlcData>> groupedTrades;
            if (candleRange == 1)
            {
                groupedTrades = ohlcList.GroupBy(t => new DateTime(t.DateTime.Year, t.DateTime.Month, t.DateTime.Day, t.DateTime.Hour, t.DateTime.Minute, 0));
            }
            else
            {
                groupedTrades = ohlcList.GroupBy(t => new DateTime(t.DateTime.Year, t.DateTime.Month, t.DateTime.Day, t.DateTime.Hour, (t.DateTime.Minute / candleRange) * candleRange, 0));
            }
            var grouped = groupedTrades
                .Select(g => new StockDataPoint
                {
                    Date = g.Key,
                    Open = g.OrderBy(t => t.DateTime).First().Open,
                    High = g.Max(t => t.High),
                    Low = g.Min(t => t.Low),
                    Close = g.OrderBy(t => t.DateTime).Last().Close,
                    Volume = g.Sum(t => t.Volume),
                    TimeLabel = g.Key.ToString("yyyy-MM-dd HH:mm")
                })
                .OrderBy(dp => dp.Date)
                .ToList();
            return grouped;
        }

    }
}