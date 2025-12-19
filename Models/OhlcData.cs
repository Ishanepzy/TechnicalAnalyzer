using System;

namespace TechnicalAnalyzer.Models
{
    public class OhlcData
    {
        public int Id { get; set; }
        public string Ticker { get; set; }
        public DateTime DateTime { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
    }
}
