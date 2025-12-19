using System.Collections.Generic;

namespace TechnicalAnalyzer.Models
{
    public class FeatureVector
    {
        public double SmaEmaGap { get; set; }
        public double Rsi { get; set; }
        public double MacdHistogram { get; set; }
        public double Momentum { get; set; }
        public int Label { get; set; }
    }

    public class SignalFilterReport
    {
        public int OriginalSignals { get; set; }
        public int AcceptedSignals { get; set; }
        public int RejectedSignals { get; set; }
        public double AverageConfidence { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    public class KnnPrediction
    {
        public string Direction { get; set; }
        public double Confidence { get; set; }
        public string Explanation { get; set; }
    }
}

