using Microsoft.AspNetCore.Mvc;
using TechnicalAnalyzer.Models;
using TechnicalAnalyzer.Services;
using System.Threading.Tasks;
using System.Linq;

namespace TechnicalAnalyzer.Controllers
{
    public class StockController : Controller
    {
        private readonly NepseApiService _nepseApiService;
        private readonly IndicatorService _indicatorService;
        private readonly MachineLearningService _machineLearningService;

        // Constructor to initialize _nepseApiService and _indicatorService
        public StockController(
            NepseApiService nepseApiService,
            IndicatorService indicatorService,
            MachineLearningService machineLearningService)
        {
            _nepseApiService = nepseApiService ?? throw new ArgumentNullException(nameof(nepseApiService));
            _indicatorService = indicatorService ?? throw new ArgumentNullException(nameof(indicatorService));
            _machineLearningService = machineLearningService ?? throw new ArgumentNullException(nameof(machineLearningService));
        }

        // GET: /Stock/Chart?ticker=XYZ
        public async Task<IActionResult> Chart(string ticker, int? smaPeriod, int? emaPeriod, int? rsiPeriod, bool showMacd = false, int candleRange = 60)
        {
            if (string.IsNullOrEmpty(ticker))
            {
                // Ensure all ViewBag properties are set to null
                ViewBag.SMA = null;
                ViewBag.SMAPeriod = null;
                ViewBag.EMA = null;
                ViewBag.EMAPeriod = null;
                ViewBag.RSI = null;
                ViewBag.RSIPeriod = null;
                ViewBag.MACDLine = null;
                ViewBag.MACDSignal = null;
                ViewBag.MACDHistogram = null;
                ViewBag.Signals = null;
                return View(null);
            }

            // Fetch chart data from database instead of API
            var dbDataPoints = await _nepseApiService.GetOhlcDataFromDbAsync(ticker, candleRange);
            var stockData = new StockDataModel { Ticker = ticker, DataPoints = dbDataPoints };

            if (stockData != null && stockData.DataPoints != null && stockData.DataPoints.Count >0)
            {
                List<TradeSignalModel> signals = null;
                List<decimal?> sma = null;
                List<decimal?> ema = null;
                List<decimal?> rsiValues = null;
                List<decimal?> macdLine = null;
                List<decimal?> macdSignal = null;
                List<decimal?> macdHistogram = null;

                if (smaPeriod.HasValue && smaPeriod >1)
                {
                    sma = _indicatorService.CalculateSMA(stockData.DataPoints, smaPeriod.Value);
                    ViewBag.SMA = sma;
                    ViewBag.SMAPeriod = smaPeriod;
                }
                else
                {
                    ViewBag.SMA = null;
                    ViewBag.SMAPeriod = null;
                }

                if (emaPeriod.HasValue && emaPeriod >1)
                {
                    ema = _indicatorService.CalculateEMA(stockData.DataPoints, emaPeriod.Value);
                    ViewBag.EMA = ema;
                    ViewBag.EMAPeriod = emaPeriod;
                }
                else
                {
                    ViewBag.EMA = null;
                    ViewBag.EMAPeriod = null;
                }

                if (rsiPeriod.HasValue && rsiPeriod >1)
                {
                    rsiValues = _indicatorService.CalculateRSI(stockData.DataPoints, rsiPeriod.Value);
                    ViewBag.RSI = rsiValues;
                    ViewBag.RSIPeriod = rsiPeriod;
                }
                else
                {
                    ViewBag.RSI = null;
                    ViewBag.RSIPeriod = null;
                }

                var (macdLineFull, macdSignalFull, macdHistogramFull) = _indicatorService.CalculateMACD(stockData.DataPoints);
                macdLine = macdLineFull;
                macdSignal = macdSignalFull;
                macdHistogram = macdHistogramFull;

                if (showMacd)
                {
                    ViewBag.MACDLine = macdLine;
                    ViewBag.MACDSignal = macdSignal;
                    ViewBag.MACDHistogram = macdHistogram;
                }
                else
                {
                    ViewBag.MACDLine = null;
                    ViewBag.MACDSignal = null;
                    ViewBag.MACDHistogram = null;
                }

                if (sma != null && ema != null)
                {
                    signals = _indicatorService.CalculateSmaEmaSignals(stockData.DataPoints, sma, ema);
                }
                ViewBag.Signals = signals;

                // Backtest the strategy with the calculated indicators
                var backtestResults = _indicatorService.BacktestSignals(
                    stockData.DataPoints,
                    ViewBag.SMA as List<decimal?>,
                    ViewBag.EMA as List<decimal?>,
                    2, // stopLoss fixed at 2%
                    5  // takeProfit fixed at 5%
                );
                ViewBag.BacktestResults = backtestResults;

                // Machine learning feature generation using default indicator periods
                var mlSma = _indicatorService.CalculateSMA(stockData.DataPoints, 20);
                var mlEma = _indicatorService.CalculateEMA(stockData.DataPoints, 10);
                var mlRsi = _indicatorService.CalculateRSI(stockData.DataPoints, 14);
                var mlMacdHistogram = macdHistogram;
                var featureVectors = _machineLearningService.BuildFeatureVectors(
                    stockData.DataPoints,
                    mlSma,
                    mlEma,
                    mlRsi,
                    mlMacdHistogram);

                if (signals != null && signals.Count > 0)
                {
                    var (filteredSignals, report) = _machineLearningService.FilterSignalsWithRandomForest(
                        signals,
                        stockData.DataPoints,
                        mlSma,
                        mlEma,
                        mlRsi,
                        mlMacdHistogram,
                        featureVectors);
                    ViewBag.Signals = filteredSignals;
                    ViewBag.SignalFilterReport = report;
                }

                var latestVector = _machineLearningService.BuildVectorAtIndex(
                    stockData.DataPoints.Count - 1,
                    stockData.DataPoints,
                    mlSma,
                    mlEma,
                    mlRsi,
                    mlMacdHistogram);
                var knnPrediction = _machineLearningService.PredictNextMoveWithKnn(featureVectors, latestVector);
                ViewBag.KnnPrediction = knnPrediction;
            }
            else
            {
                // Ensure all ViewBag properties are set to null if no data
                ViewBag.SMA = null;
                ViewBag.SMAPeriod = null;
                ViewBag.EMA = null;
                ViewBag.EMAPeriod = null;
                ViewBag.RSI = null;
                ViewBag.RSIPeriod = null;
                ViewBag.MACDLine = null;
                ViewBag.MACDSignal = null;
                ViewBag.MACDHistogram = null;
                ViewBag.Signals = null;
            }

            return View(stockData);
        }

        // GET: /Stock/Index
        [HttpGet]
        public async Task<JsonResult> GetStockTickers()
        {
            var tickers = await _nepseApiService.GetStockTickersAsync();
            return Json(tickers);
        }
    }
}