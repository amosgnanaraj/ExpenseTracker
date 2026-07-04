using System;

namespace ExpenseTracker.Models
{
    public class TechnicalAnalysisResult
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        
        public decimal? Sma50 { get; set; }
        public decimal? Sma200 { get; set; }
        
        public decimal? Rsi14 { get; set; }
        
        public decimal? Macd { get; set; }
        public decimal? MacdSignal { get; set; }
        public decimal? MacdHistogram { get; set; }
        
        // Buy, Sell, Hold
        public string Recommendation { get; set; } = "Hold";
        public string Reason { get; set; } = string.Empty;
    }
}
