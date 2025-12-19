using System;

namespace TechnicalAnalyzer.Models
{
    public class Trade
    {
        public DateTime DateTime { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
    }
} 