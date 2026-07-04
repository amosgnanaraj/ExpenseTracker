using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OoplesFinance.YahooFinanceAPI;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services
{
    public interface IFundamentalAnalysisService
    {
        Task<FundamentalAnalysisResult?> GetAnalysisAsync(string symbol);
    }

    public class FundamentalAnalysisService : IFundamentalAnalysisService
    {
        private readonly ILogger<FundamentalAnalysisService> _logger;

        public FundamentalAnalysisService(ILogger<FundamentalAnalysisService> logger)
        {
            _logger = logger;
        }

        public async Task<FundamentalAnalysisResult?> GetAnalysisAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return null;

            var refinedSymbol = symbol.Trim().ToUpper();
            if (!refinedSymbol.Contains(".") && !refinedSymbol.Contains(":"))
            {
                refinedSymbol += ".NS";
            }
            else if (refinedSymbol.Contains(":"))
            {
                refinedSymbol = refinedSymbol.Replace("NSE:", "") + ".NS";
                refinedSymbol = refinedSymbol.Replace("BSE:", "") + ".BO";
            }

            try
            {
                var client = new YahooClient();
                
                // Fetch data asynchronously in parallel safely
                var summaryDetailsTask = SafeFetchAsync(() => client.GetSummaryDetailsAsync(refinedSymbol));
                var financialDataTask = SafeFetchAsync(() => client.GetFinancialDataAsync(refinedSymbol));
                var keyStatsTask = SafeFetchAsync(() => client.GetKeyStatisticsAsync(refinedSymbol));

                await Task.WhenAll(summaryDetailsTask, financialDataTask, keyStatsTask);

                var summaryDetails = await summaryDetailsTask;
                var financialData = await financialDataTask;
                var keyStats = await keyStatsTask;

                if (summaryDetails == null && financialData == null && keyStats == null)
                    return null;

                var result = new FundamentalAnalysisResult
                {
                    Symbol = symbol,
                    CurrentPrice = (decimal?)summaryDetails?.RegularMarketPreviousClose?.Raw,
                    MarketCap = (decimal?)summaryDetails?.MarketCap?.Raw,
                    TrailingPE = (decimal?)summaryDetails?.TrailingPE?.Raw,
                    ForwardPE = (decimal?)summaryDetails?.ForwardPE?.Raw,
                    DividendYield = (decimal?)summaryDetails?.DividendYield?.Raw,
                    PriceToBook = (decimal?)keyStats?.PriceToBook?.Raw,
                    Eps = (decimal?)keyStats?.TrailingEps?.Raw,
                    ReturnOnEquity = (decimal?)financialData?.ReturnOnEquity?.Raw,
                    ROCE = (decimal?)financialData?.ReturnOnAssets?.Raw
                };

                DetermineCategory(result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing fundamental analysis for {Symbol}", refinedSymbol);
                return null;
            }
        }

        private void DetermineCategory(FundamentalAnalysisResult result)
        {
            // Simple deterministic algorithm to assign category

            if (result.TrailingPE > 45 || (result.TrailingPE < 0 && result.PriceToBook > 5))
            {
                result.Category = "Overvalued";
                result.Reason = "Exceptionally high P/E or negative earnings with high P/B indicates an overvalued stock.";
            }
            else if (result.DividendYield >= 0.04m)
            {
                result.Category = "Income";
                result.Reason = $"High dividend yield ({(result.DividendYield * 100)?.ToString("0.00")}%) is excellent for income generation.";
            }
            else if (result.TrailingPE > 0 && result.TrailingPE <= 18 && result.PriceToBook <= 2.0m)
            {
                result.Category = "Value";
                result.Reason = "Low P/E (< 18) and a logical P/B ratio reflects a potentially undervalued 'Value' stock.";
            }
            else if (result.ForwardPE > 20 && result.ReturnOnEquity >= 0.15m)
            {
                result.Category = "Growth";
                result.Reason = "Strong forward earnings potential and high ROE reflects a 'Growth' stock orientation.";
            }
            else
            {
                result.Category = "Neutral";
                result.Reason = "Fundamental metrics are within standard market bounds without strong skew.";
            }
        }

        private async Task<T> SafeFetchAsync<T>(Func<Task<T>> fetchFunc) where T : class
        {
            try
            {
                return await fetchFunc();
            }
            catch
            {
                return null;
            }
        }
    }
}
