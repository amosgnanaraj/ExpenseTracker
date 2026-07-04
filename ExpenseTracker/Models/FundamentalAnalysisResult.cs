namespace ExpenseTracker.Models
{
    public class FundamentalAnalysisResult
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal? CurrentPrice { get; set; }
        public decimal? MarketCap { get; set; }
        public decimal? TrailingPE { get; set; }
        public decimal? ForwardPE { get; set; }
        public decimal? ReturnOnEquity { get; set; }
        public decimal? ROCE { get; set; } // Added ROCE parameter
        public decimal? Eps { get; set; }
        public decimal? DividendYield { get; set; }
        public decimal? PriceToBook { get; set; }

        // Categories: Value, Growth, Income, Overvalued, Neutral
        public string Category { get; set; } = "Neutral";
        public string Reason { get; set; } = string.Empty;
    }
}
