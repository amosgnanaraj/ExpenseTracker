using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OoplesFinance.YahooFinanceAPI;
using OoplesFinance.YahooFinanceAPI.Enums;
using Skender.Stock.Indicators;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services
{
    public interface ITechnicalAnalysisService
    {
        Task<TechnicalAnalysisResult?> GetAnalysisAsync(string symbol);
    }

    public class TechnicalAnalysisService : ITechnicalAnalysisService
    {
        private readonly ILogger<TechnicalAnalysisService> _logger;

        public TechnicalAnalysisService(ILogger<TechnicalAnalysisService> logger)
        {
            _logger = logger;
        }

        public async Task<TechnicalAnalysisResult?> GetAnalysisAsync(string symbol)
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
                var historicalDataList = await client.GetHistoricalDataAsync(refinedSymbol, DataFrequency.Daily, DateTime.UtcNow.AddYears(-2), DateTime.UtcNow);

                if (historicalDataList == null || !historicalDataList.Any())
                {
                    _logger.LogWarning("No historical data found for symbol {Symbol}", refinedSymbol);
                    return null;
                }

                // Convert Yahoo Finance HistoricalData to Skender Quote
                var quotes = historicalDataList
                    .Select(h => new Quote
                    {
                        Date = h.Date,
                        Open = (decimal)h.Open,
                        High = (decimal)h.High,
                        Low = (decimal)h.Low,
                        Close = (decimal)h.Close,
                        Volume = (decimal)h.Volume
                    })
                    .OrderBy(q => q.Date)
                    .ToArray();

                if (quotes.Length < 200)
                {
                    _logger.LogWarning("Not enough data to calculate 200-day SMA for {Symbol}. We need 200 quotes, but got {Length}.", refinedSymbol, quotes.Length);
                    // We can still try to return something, but Sma200 will be null
                }

                var currentPrice = quotes.Last().Close;

                var result = new TechnicalAnalysisResult
                {
                    Symbol = symbol,
                    CurrentPrice = currentPrice,
                    Recommendation = "Hold",
                    Reason = "Not enough data for a complete analysis."
                };

                // Calculate SMA 50
                var sma50 = quotes.Length >= 50 ? quotes.GetSma(50).LastOrDefault()?.Sma : null;
                result.Sma50 = sma50.HasValue ? (decimal)sma50.Value : null;

                // Calculate SMA 200
                var sma200 = quotes.Length >= 200 ? quotes.GetSma(200).LastOrDefault()?.Sma : null;
                result.Sma200 = sma200.HasValue ? (decimal)sma200.Value : null;

                // Calculate RSI 14
                var rsi14 = quotes.Length >= 14 ? quotes.GetRsi(14).LastOrDefault()?.Rsi : null;
                result.Rsi14 = rsi14.HasValue ? (decimal)rsi14.Value : null;

                // Calculate MACD (12, 26, 9)
                var macdResults = quotes.Length >= 26 ? quotes.GetMacd(12, 26, 9).LastOrDefault() : null;
                if (macdResults != null)
                {
                    result.Macd = macdResults.Macd.HasValue ? (decimal)macdResults.Macd.Value : null;
                    result.MacdSignal = macdResults.Signal.HasValue ? (decimal)macdResults.Signal.Value : null;
                    result.MacdHistogram = macdResults.Histogram.HasValue ? (decimal)macdResults.Histogram.Value : null;
                }

                // Evaluate Signals
                DetermineRecommendation(result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing technical analysis for {Symbol}", refinedSymbol);
                return null;
            }
        }

        private void DetermineRecommendation(TechnicalAnalysisResult result)
        {
            if (result.Sma50 == null || result.Sma200 == null || result.Rsi14 == null || result.MacdHistogram == null)
            {
                return; // Keep Hold and default reason
            }

            bool isBullishTrend = result.Sma50 > result.Sma200;
            bool isBearishTrend = result.Sma50 < result.Sma200;

            bool isStrongOversold = result.Rsi14 < 30;
            bool isOversold = result.Rsi14 < 45;
            
            bool isStrongOverbought = result.Rsi14 > 70;
            bool isOverbought = result.Rsi14 > 55;

            bool gainingMomentum = result.MacdHistogram > 0;
            bool losingMomentum = result.MacdHistogram < 0;

            if (isBullishTrend && isStrongOversold && gainingMomentum)
            {
                result.Recommendation = "Strong Buy";
                result.Reason = "Bullish trend with strong oversold conditions and positive MACD momentum.";
            }
            else if (isBullishTrend && (isOversold || gainingMomentum))
            {
                result.Recommendation = "Buy";
                result.Reason = "Bullish trend with favorable RSI or positive MACD momentum.";
            }
            else if (isBearishTrend && isStrongOverbought && losingMomentum)
            {
                result.Recommendation = "Strong Sell";
                result.Reason = "Bearish trend with strong overbought conditions and negative MACD momentum.";
            }
            else if (isBearishTrend && (isOverbought || losingMomentum))
            {
                result.Recommendation = "Sell";
                result.Reason = "Bearish trend with overbought RSI or negative MACD momentum.";
            }
            else
            {
                result.Recommendation = "Hold";
                result.Reason = "Mixed indicators. Trend and momentum do not strongly align.";
            }

            // Quick correction for extreme RSI regardless of trend:
            if (result.Rsi14 > 80)
            {
                result.Recommendation = "Strong Sell";
                result.Reason = "Extreme Overbought condition (RSI > 80). High probability of pullback.";
            }
            else if (result.Rsi14 < 20)
            {
                result.Recommendation = "Strong Buy";
                result.Reason = "Extreme Oversold condition (RSI < 20). Strong potential bounce opportunity.";
            }
        }
    }
}
