using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TechnicalAnalyzer.Models
{
    public class StockDataPoint
    {
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
        public string TimeLabel { get; set; } 

        // Add this property for Chart.js
        [JsonInclude]
        public string x => Date.ToString("o"); // ISO 8601 string
        [JsonInclude]
        public decimal o => Open;
        [JsonInclude]
        public decimal h => High;
        [JsonInclude]
        public decimal l => Low;
        [JsonInclude]
        public decimal c => Close;
    }

    public class StockDataModel
    {
        public string Ticker { get; set; }
        public List<StockDataPoint> DataPoints { get; set; }
        // Raw JSON returned by upstream API (for debugging)
        public string RawJson { get; set; }
    }
}