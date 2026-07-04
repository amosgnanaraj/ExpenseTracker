using OoplesFinance.YahooFinanceAPI;
using OoplesFinance.YahooFinanceAPI.Enums;

namespace ExpenseTracker.Services
{
    public interface IStockPriceService
    {
        Task<decimal?> GetLatestPriceAsync(string symbol);
    }

    public class StockPriceService : IStockPriceService
    {
        private readonly ILogger<StockPriceService> _logger;

        public StockPriceService(ILogger<StockPriceService> logger)
        {
            _logger = logger;
        }

        public async Task<decimal?> GetLatestPriceAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return null;

            // Refine symbol for Indian markets if common
            var refinedSymbol = symbol.Trim().ToUpper();
            if (!refinedSymbol.Contains(".") && !refinedSymbol.Contains(":"))
            {
                // Default to NSE for Indian users as seen in existing logic
                refinedSymbol += ".NS";
            }
            else if (refinedSymbol.Contains(":"))
            {
                // Convert NSE:SYMBOL to SYMBOL.NS for Yahoo Finance
                refinedSymbol = refinedSymbol.Replace("NSE:", "") + ".NS";
                refinedSymbol = refinedSymbol.Replace("BSE:", "") + ".BO";
            }

            try
            {
                var client = new YahooClient();
                var summaryDetails = await client.GetSummaryDetailsAsync(refinedSymbol);
                
                if (summaryDetails != null)
                {
                    // RegularMarketPreviousClose is available. Let's see if there's a price.
                    // If RegularMarketPrice is missing, maybe it's under QuoteType or something.
                    // Re-checking the error: 'SummaryDetail' does not contain a definition for 'RegularMarketPrice'
                    return (decimal?)summaryDetails.RegularMarketPreviousClose?.Raw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching price for symbol {Symbol}", refinedSymbol);
            }

            return null;
        }
    }
}
