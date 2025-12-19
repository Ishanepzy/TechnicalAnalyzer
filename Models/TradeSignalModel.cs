namespace TechnicalAnalyzer.Models
{
    public class TradeSignalModel
    {
        public int Index { get; set; } // Index in DataPoints
        public string Type { get; set; } // "Buy" or "Sell"
        public string TimeLabel { get; set; }
        public decimal Price { get; set; }
        public double? MachineLearningConfidence { get; set; }
    }
}