using System.Collections.Generic;
using System.Linq;
using TechnicalAnalyzer.Models;

namespace TechnicalAnalyzer.Services
{
    public class IndicatorService
    {
        // Calculates the Simple Moving Average (SMA)
        public List<decimal?> CalculateSMA(List<StockDataPoint> dataPoints, int period)
        {
            var sma = new List<decimal?>();
            for (int i = 0; i < dataPoints.Count; i++)
            {
                if (i + 1 < period)
                {
                    sma.Add(null); // Not enough data for SMA
                }
                else
                {
                    var avg = dataPoints.Skip(i + 1 - period).Take(period).Average(dp => dp.Close);
                    sma.Add(avg);
                }
            }
            return sma;
        }
        public List<decimal?> CalculateEMA(List<StockDataPoint> dataPoints, int period)
        {
            var ema = new List<decimal?>();
            if (dataPoints == null || dataPoints.Count < period)
                return Enumerable.Repeat<decimal?>(null, dataPoints?.Count ?? 0).ToList();

            decimal? prevEma = null;
            decimal multiplier = 2m / (period + 1);

            for (int i = 0; i < dataPoints.Count; i++)
            {
                if (i + 1 < period)
                {
                    ema.Add(null);
                }
                else if (i + 1 == period)
                {
                    // First EMA value is SMA
                    decimal sma = dataPoints.Take(period).Average(dp => dp.Close);
                    ema.Add(sma);
                    prevEma = sma;
                }
                else
                {
                    decimal close = dataPoints[i].Close;
                    decimal currentEma = ((close - prevEma.Value) * multiplier) + prevEma.Value;
                    ema.Add(currentEma);
                    prevEma = currentEma;
                }
            }
            return ema;
        }
        public List<decimal?> CalculateRSI(List<StockDataPoint> dataPoints, int period)
        {
            var rsi = new List<decimal?>();
            if (dataPoints == null || dataPoints.Count < period + 1)
                return Enumerable.Repeat<decimal?>(null, dataPoints?.Count ?? 0).ToList();

            decimal gain = 0, loss = 0;
            for (int i = 1; i <= period; i++)
            {
                var change = dataPoints[i].Close - dataPoints[i - 1].Close;
                if (change > 0) gain += change;
                else loss -= change;
            }
            gain /= period;
            loss /= period;

            rsi.AddRange(Enumerable.Repeat<decimal?>(null, period));
            rsi.Add(loss == 0 ? 100 : 100 - (100 / (1 + (gain / loss))));

            for (int i = period + 1; i < dataPoints.Count; i++)
            {
                var change = dataPoints[i].Close - dataPoints[i - 1].Close;
                if (change > 0)
                {
                    gain = (gain * (period - 1) + change) / period;
                    loss = (loss * (period - 1)) / period;
                }
                else
                {
                    gain = (gain * (period - 1)) / period;
                    loss = (loss * (period - 1) - change) / period;
                }
                rsi.Add(loss == 0 ? 100 : 100 - (100 / (1 + (gain / loss))));
            }
            return rsi;
        }

        public (List<decimal?> macdLine, List<decimal?> signalLine, List<decimal?> histogram) CalculateMACD(List<StockDataPoint> dataPoints, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
        {
            var closes = dataPoints.Select(dp => dp.Close).ToList();
            var emaFast = CalculateEMA(dataPoints, fastPeriod);
            var emaSlow = CalculateEMA(dataPoints, slowPeriod);

            var macdLine = new List<decimal?>();
            for (int i = 0; i < closes.Count; i++)
            {
                if (emaFast[i] == null || emaSlow[i] == null)
                    macdLine.Add(null);
                else
                    macdLine.Add(emaFast[i] - emaSlow[i]);
            }

            // Calculate Signal Line (EMA of MACD Line)
            var macdDataPoints = macdLine.Select((val, idx) => new StockDataPoint { Close = val ?? 0, Date = dataPoints[idx].Date }).ToList();
            var signalLine = CalculateEMA(macdDataPoints, signalPeriod);

            // Histogram = MACD Line - Signal Line
            var histogram = new List<decimal?>();
            for (int i = 0; i < macdLine.Count; i++)
            {
                if (macdLine[i] == null || signalLine[i] == null)
                    histogram.Add(null);
                else
                    histogram.Add(macdLine[i] - signalLine[i]);
            }

            return (macdLine, signalLine, histogram);
        }
        public List<TradeSignalModel> CalculateSmaEmaSignals(List<StockDataPoint> dataPoints, List<decimal?> sma, List<decimal?> ema)
        {
            var signals = new List<TradeSignalModel>();
            if (sma == null || ema == null || sma.Count != ema.Count) return signals;

            for (int i = 1; i < sma.Count; i++)
            {
                if (sma[i - 1] == null || ema[i - 1] == null || sma[i] == null || ema[i] == null)
                    continue;

                // Buy: SMA crosses above EMA
                if (sma[i - 1] < ema[i - 1] && sma[i] >= ema[i])
                {
                    signals.Add(new TradeSignalModel
                    {
                        Index = i,
                        Type = "Buy",
                        TimeLabel = dataPoints[i].TimeLabel,
                        Price = dataPoints[i].Close
                    });
                }
                // Sell: SMA crosses below EMA
                else if (sma[i - 1] > ema[i - 1] && sma[i] <= ema[i])
                {
                    signals.Add(new TradeSignalModel
                    {
                        Index = i,
                        Type = "Sell",
                        TimeLabel = dataPoints[i].TimeLabel,
                        Price = dataPoints[i].Close
                    });
                }
            }
            return signals;
        }

        public class BacktestResult
        {
            public int TotalTrades { get; set; }
            public int Wins { get; set; }
            public int Losses { get; set; }
            public decimal TotalProfit { get; set; }
            public decimal MaxDrawdown { get; set; }
            public List<decimal> EquityCurve { get; set; } = new();
            public List<string> TradeLog { get; set; } = new();
            public List<string> AlertLog { get; set; } = new();
        }

        public BacktestResult BacktestSignals(
            List<StockDataPoint> dataPoints,
            List<decimal?> sma,
            List<decimal?> ema,
            decimal stopLossPercent,
            decimal takeProfitPercent)
        {
            var result = new BacktestResult();
            decimal capital = 10000m;
            decimal equity = capital;
            decimal maxEquity = capital;
            decimal minEquity = capital;
            bool inPosition = false;
            decimal entryPrice = 0;
            int entryIndex = 0;
            int wins = 0, losses = 0;
            int trades = 0;
            List<decimal> equityCurve = new();
            for (int i = 1; i < dataPoints.Count; i++)
            {
                if (!inPosition && ema[i] != null && sma[i] != null && ema[i - 1] != null && sma[i - 1] != null)
                {
                    // Buy signal
                    if (ema[i] > sma[i] && ema[i - 1] <= sma[i - 1])
                    {
                        inPosition = true;
                        entryPrice = dataPoints[i].Close;
                        entryIndex = i;
                        trades++;
                        result.TradeLog.Add($"Buy at {entryPrice} on {dataPoints[i].Date}");
                    }
                }
                if (inPosition)
                {
                    decimal stopLossPrice = entryPrice * (1m - stopLossPercent / 100m);
                    decimal takeProfitPrice = entryPrice * (1m + takeProfitPercent / 100m);
                    decimal exitPrice = dataPoints[i].Close;
                    bool exit = false;
                    // Stop loss
                    if (dataPoints[i].Low <= stopLossPrice)
                    {
                        exitPrice = stopLossPrice;
                        exit = true;
                        result.TradeLog.Add($"Stop loss at {exitPrice} on {dataPoints[i].Date}");
                        result.AlertLog.Add($"Stop loss hit at {exitPrice} on {dataPoints[i].Date}");
                    }
                    // Take profit
                    else if (dataPoints[i].High >= takeProfitPrice)
                    {
                        exitPrice = takeProfitPrice;
                        exit = true;
                        result.TradeLog.Add($"Take profit at {exitPrice} on {dataPoints[i].Date}");
                        result.AlertLog.Add($"Take profit hit at {exitPrice} on {dataPoints[i].Date}");
                    }
                    // Sell signal
                    else if (ema[i] < sma[i] && ema[i - 1] >= sma[i - 1])
                    {
                        exit = true;
                        result.TradeLog.Add($"Sell at {exitPrice} on {dataPoints[i].Date}");
                    }
                    if (exit)
                    {
                        decimal netPnL = (exitPrice - entryPrice);
                        equity += netPnL;
                        if (netPnL > 0) wins++; else losses++;
                        inPosition = false;
                        entryPrice = 0;
                        entryIndex = 0;
                    }
                }
                equityCurve.Add(equity);
                if (equity > maxEquity) maxEquity = equity;
                if (equity < minEquity) minEquity = equity;
            }
            result.TotalTrades = trades;
            result.Wins = wins;
            result.Losses = losses;
            result.TotalProfit = equity - capital;
            result.MaxDrawdown = maxEquity - minEquity;
            result.EquityCurve = equityCurve;
            return result;
        }
    }
}