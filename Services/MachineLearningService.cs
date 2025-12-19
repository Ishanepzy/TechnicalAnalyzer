using System;
using System.Collections.Generic;
using System.Linq;
using TechnicalAnalyzer.Models;

namespace TechnicalAnalyzer.Services
{
    public class MachineLearningService
    {
        private enum FeatureType
        {
            SmaEmaGap,
            Rsi,
            MacdHistogram,
            Momentum
        }

        private class DecisionStump
        {
            public FeatureType Feature { get; set; }
            public double Threshold { get; set; }
            public int AboveLabel { get; set; }
            public int BelowLabel { get; set; }

            public int Predict(FeatureVector vector)
            {
                var value = GetFeatureValue(vector, Feature);
                return value >= Threshold ? AboveLabel : BelowLabel;
            }

            private static double GetFeatureValue(FeatureVector vector, FeatureType feature) =>
                feature switch
                {
                    FeatureType.SmaEmaGap => vector.SmaEmaGap,
                    FeatureType.Rsi => vector.Rsi,
                    FeatureType.MacdHistogram => vector.MacdHistogram,
                    FeatureType.Momentum => vector.Momentum,
                    _ => 0
                };
        }

        private class RandomForestModel
        {
            private readonly List<DecisionStump> _trees;

            public RandomForestModel(List<DecisionStump> trees)
            {
                _trees = trees;
            }

            public double PredictProbability(FeatureVector vector)
            {
                if (_trees.Count == 0) return 0.5;
                var bullishVotes = _trees.Count(t => t.Predict(vector) >= 0);
                return bullishVotes / (double)_trees.Count;
            }
        }

        private readonly Random _random = new();

        public List<FeatureVector> BuildFeatureVectors(
            List<StockDataPoint> dataPoints,
            List<decimal?> sma,
            List<decimal?> ema,
            List<decimal?> rsi,
            List<decimal?> macdHistogram,
            int futureOffset = 3)
        {
            var vectors = new List<FeatureVector>();
            if (dataPoints == null || sma == null || ema == null || rsi == null || macdHistogram == null)
                return vectors;

            for (int i = 1; i < dataPoints.Count - futureOffset; i++)
            {
                if (sma[i] == null || ema[i] == null || rsi[i] == null || macdHistogram[i] == null)
                    continue;

                var current = dataPoints[i];
                var previous = dataPoints[i - 1];
                var future = dataPoints[i + futureOffset];
                if (previous.Close == 0)
                    continue;

                var gap = (double)((ema[i] - sma[i]) ?? 0m);
                var rsiVal = (double)(rsi[i] ?? 50m);
                var macdVal = (double)(macdHistogram[i] ?? 0m);
                var momentum = (double)((current.Close - previous.Close) / previous.Close);
                var label = future.Close >= current.Close ? 1 : -1;

                vectors.Add(new FeatureVector
                {
                    SmaEmaGap = gap,
                    Rsi = rsiVal,
                    MacdHistogram = macdVal,
                    Momentum = momentum,
                    Label = label
                });
            }
            return vectors;
        }

        public (List<TradeSignalModel> Filtered, SignalFilterReport Report) FilterSignalsWithRandomForest(
            List<TradeSignalModel> signals,
            List<StockDataPoint> dataPoints,
            List<decimal?> sma,
            List<decimal?> ema,
            List<decimal?> rsi,
            List<decimal?> macdHistogram,
            List<FeatureVector> trainingData,
            int treeCount = 25)
        {
            var report = new SignalFilterReport();
            if (signals == null || signals.Count == 0)
                return (signals ?? new List<TradeSignalModel>(), report);

            report.OriginalSignals = signals.Count;
            if (trainingData == null || trainingData.Count < 10)
            {
                report.Notes.Add("Not enough historical data to train the Random Forest filter.");
                report.AcceptedSignals = signals.Count;
                return (signals, report);
            }

            var forest = TrainForest(trainingData, treeCount);
            var accepted = new List<TradeSignalModel>();
            double confidenceSum = 0;

            foreach (var signal in signals)
            {
                var vector = BuildVectorAtIndex(signal.Index, dataPoints, sma, ema, rsi, macdHistogram);
                if (vector == null)
                {
                    accepted.Add(signal);
                    continue;
                }

                var probability = forest.PredictProbability(vector);
                signal.MachineLearningConfidence = probability;
                var shouldAccept = signal.Type == "Buy" ? probability >= 0.5 : probability < 0.5;
                if (shouldAccept)
                {
                    accepted.Add(signal);
                    confidenceSum += probability;
                }
                else
                {
                    report.RejectedSignals++;
                }
            }

            report.AcceptedSignals = accepted.Count;
            if (accepted.Count > 0)
            {
                report.AverageConfidence = confidenceSum / accepted.Count;
            }
            else
            {
                report.Notes.Add("All signals were filtered out by the Random Forest classifier.");
            }
            return (accepted, report);
        }

        public KnnPrediction PredictNextMoveWithKnn(
            List<FeatureVector> trainingData,
            FeatureVector latestVector,
            int neighbors = 7)
        {
            if (trainingData == null || trainingData.Count < neighbors || latestVector == null)
                return null;

            var ordered = trainingData
                .Select(v => new { Vector = v, Distance = Distance(v, latestVector) })
                .OrderBy(x => x.Distance)
                .Take(neighbors)
                .ToList();

            if (ordered.Count == 0)
                return null;

            var bullishVotes = ordered.Count(x => x.Vector.Label >= 0);
            var bearishVotes = ordered.Count - bullishVotes;
            var direction = bullishVotes >= bearishVotes ? "Bullish" : "Bearish";
            var confidence = ordered.Count > 0
                ? Math.Max(bullishVotes, bearishVotes) / (double)ordered.Count
                : 0.5;

            return new KnnPrediction
            {
                Direction = direction,
                Confidence = confidence,
                Explanation = $"Based on the {neighbors} most similar situations, {bullishVotes} favored upward movement."
            };
        }

        public FeatureVector BuildVectorAtIndex(
            int index,
            List<StockDataPoint> dataPoints,
            List<decimal?> sma,
            List<decimal?> ema,
            List<decimal?> rsi,
            List<decimal?> macdHistogram)
        {
            if (index <= 0 || index >= dataPoints?.Count ||
                sma == null || ema == null || rsi == null || macdHistogram == null)
                return null;

            if (sma[index] == null || ema[index] == null || rsi[index] == null || macdHistogram[index] == null)
                return null;

            var prev = dataPoints[index - 1];
            var current = dataPoints[index];
            if (prev.Close == 0)
                return null;

            return new FeatureVector
            {
                SmaEmaGap = (double)((ema[index] - sma[index]) ?? 0m),
                Rsi = (double)(rsi[index] ?? 50m),
                MacdHistogram = (double)(macdHistogram[index] ?? 0m),
                Momentum = (double)((current.Close - prev.Close) / prev.Close),
                Label = 0
            };
        }

        private RandomForestModel TrainForest(List<FeatureVector> vectors, int treeCount)
        {
            var trees = new List<DecisionStump>();
            var featureTypes = Enum.GetValues(typeof(FeatureType)).Cast<FeatureType>().ToList();

            for (int i = 0; i < treeCount; i++)
            {
                var feature = featureTypes[_random.Next(featureTypes.Count)];
                var values = vectors.Select(v => GetFeatureValue(v, feature)).ToList();
                var min = values.Min();
                var max = values.Max();
                var threshold = Math.Abs(max - min) < 1e-6
                    ? min
                    : min + _random.NextDouble() * (max - min);

                var aboveLabel = MajorityLabel(vectors.Where(v => GetFeatureValue(v, feature) >= threshold));
                var belowLabel = MajorityLabel(vectors.Where(v => GetFeatureValue(v, feature) < threshold));

                trees.Add(new DecisionStump
                {
                    Feature = feature,
                    Threshold = threshold,
                    AboveLabel = aboveLabel,
                    BelowLabel = belowLabel
                });
            }

            return new RandomForestModel(trees);
        }

        private static double GetFeatureValue(FeatureVector vector, FeatureType feature) =>
            feature switch
            {
                FeatureType.SmaEmaGap => vector.SmaEmaGap,
                FeatureType.Rsi => vector.Rsi,
                FeatureType.MacdHistogram => vector.MacdHistogram,
                FeatureType.Momentum => vector.Momentum,
                _ => 0
            };

        private static int MajorityLabel(IEnumerable<FeatureVector> vectors)
        {
            var bullish = 0;
            var bearish = 0;
            foreach (var vector in vectors)
            {
                if (vector.Label >= 0) bullish++;
                else bearish++;
            }
            if (bullish == 0 && bearish == 0) return 1;
            return bullish >= bearish ? 1 : -1;
        }

        private static double Distance(FeatureVector a, FeatureVector b)
        {
            var gap = a.SmaEmaGap - b.SmaEmaGap;
            var rsi = a.Rsi - b.Rsi;
            var macd = a.MacdHistogram - b.MacdHistogram;
            var momentum = a.Momentum - b.Momentum;
            return Math.Sqrt(gap * gap + rsi * rsi + macd * macd + momentum * momentum);
        }
    }
}

