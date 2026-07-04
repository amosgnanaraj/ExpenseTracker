using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Npgsql;

namespace StockImporter
{
    class Program
    {
        private const string ConnectionString = "Host=localhost;Port=5432;Database=ExpenseTrackerDB;Username=expensetracker_user;Password=EXPapp!@#";

        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("    Expense Tracker Data Import Tool    ");
            Console.WriteLine("========================================");
            
            Console.WriteLine("\n[Phase 0] Select Import Type:");
            Console.WriteLine("1. Stock Trades (Aggregated)");
            Console.WriteLine("2. Mutual Funds (Current Portfolio)");
            Console.Write("\nChoice [1-2]: ");
            string choice = Console.ReadLine()?.Trim();

            if (choice == "2")
            {
                MutualFundImporter.Run();
                return;
            }

            Console.WriteLine("\n[Phase 1] Stock Trade Import - Input Selection");
            Console.Write("Enter the path to your Excel file: ");
            string filePath = Console.ReadLine()?.Trim('"');

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                // Try searching in current directory if not found
                if (string.IsNullOrEmpty(filePath)) filePath = "";
                var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xlsx");
                if (files.Length > 0 && string.IsNullOrEmpty(filePath))
                {
                    filePath = files[0];
                    Console.WriteLine($"Defaulting to found file: {Path.GetFileName(filePath)}");
                }
                else
                {
                    Console.WriteLine("Error: File not found.");
                    return;
                }
            }

            Console.Write("Enter the user Email to associate these stocks with: ");
            string email = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(email))
            {
                Console.WriteLine("Error: Email is required to import data.");
                return;
            }

            try
            {
                Console.WriteLine("\n[Phase 1.5] Looking up User ID...");
                string userId = GetUserIdByEmail(email);
                
                if (string.IsNullOrEmpty(userId))
                {
                    Console.WriteLine($"Error: No user found with email '{email}'.");
                    return;
                }
                Console.WriteLine($"Found User ID: {userId}");

                Console.WriteLine("\n[Phase 2] Parsing Excel File...");
                var trades = ParseExcel(filePath);
                Console.WriteLine($"Found {trades.Count} valid trade records.");

                Console.WriteLine("\n[Phase 3] Aggregating Data...");
                var summaries = AggregateTrades(trades);
                Console.WriteLine($"Aggregated into {summaries.Count} unique stock holdings.");

                Console.WriteLine("\n[Phase 4] Importing to Database...");
                ImportToDatabase(summaries, userId);
                
                Console.WriteLine("\n========================================");
                Console.WriteLine("   Import Completed Successfully!       ");
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[FATAL ERROR] {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Details: {ex.InnerException.Message}");
                }
            }
        }

        static string GetUserIdByEmail(string email)
        {
            using (var conn = new NpgsqlConnection(ConnectionString))
            {
                conn.Open();
                string query = @"SELECT ""Id"" FROM ""AspNetUsers"" WHERE ""Email"" = @email OR ""UserName"" = @email LIMIT 1";
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("email", email);
                    var result = cmd.ExecuteScalar();
                    return result?.ToString();
                }
            }
        }

        static List<StockTrade> ParseExcel(string filePath)
        {
            var trades = new List<StockTrade>();

            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed();

                // Headers are on row 6 
                var headerRow = rows.FirstOrDefault(r => r.RowNumber() == 6);
                if (headerRow == null) throw new Exception("Header row (Row 6) not found in the Excel file.");

                int stockNameCol = GetColumnIndex(headerRow, "Stock name");
                int symbolCol = GetColumnIndex(headerRow, "Symbol");
                int typeCol = GetColumnIndex(headerRow, "Type");
                int quantityCol = GetColumnIndex(headerRow, "Quantity");
                int valueCol = GetColumnIndex(headerRow, "Value");
                int dateCol = GetColumnIndex(headerRow, "Execution date and time");

                foreach (var row in rows.Where(r => r.RowNumber() > 6))
                {
                    try
                    {
                        var typeStr = row.Cell(typeCol).GetValue<string>().ToUpper();
                        if (typeStr != "BUY" && typeStr != "SELL") continue;

                        decimal qty = row.Cell(quantityCol).GetValue<decimal>();
                        decimal val = row.Cell(valueCol).GetValue<decimal>();

                        if (qty == 0) continue;

                        DateTime execDate;
                        var dateCellValue = row.Cell(dateCol).Value;
                        if (dateCellValue.IsDateTime)
                        {
                            execDate = dateCellValue.GetDateTime();
                        }
                        else
                        {
                            string dateStr = row.Cell(dateCol).GetValue<string>();
                            if (!DateTime.TryParse(dateStr, out execDate))
                            {
                                // Try custom formats if needed, or fallback to today
                                execDate = DateTime.UtcNow;
                            }
                        }

                        trades.Add(new StockTrade
                        {
                            StockName = row.Cell(stockNameCol).GetValue<string>(),
                            Symbol = row.Cell(symbolCol).GetValue<string>(),
                            Type = typeStr,
                            Quantity = qty,
                            Value = Math.Abs(val), // Many brokers use negative for sells, we want positive total value for price calculation
                            ExecutionDate = execDate
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Skipping row {row.RowNumber()} due to parsing error: {ex.Message}");
                    }
                }
            }

            return trades;
        }

        static int GetColumnIndex(IXLRow headerRow, string columnName)
        {
            var cell = headerRow.Cells().FirstOrDefault(c => c.GetValue<string>().Trim().Equals(columnName, StringComparison.OrdinalIgnoreCase));
            if (cell == null) throw new Exception($"Required column '{columnName}' not found in header row (Row 6). Check if '{columnName}' exists.");
            return cell.Address.ColumnNumber;
        }

        static List<InvestmentSummary> AggregateTrades(List<StockTrade> trades)
        {
            return trades.GroupBy(t => t.Symbol)
                .Select(g =>
                {
                    // Sort trades by date to find latest info
                    var sortedTrades = g.OrderBy(t => t.ExecutionDate).ToList();
                    
                    // Net Quantity: Buys - Sells
                    decimal netQty = g.Sum(t => t.Type == "BUY" ? t.Quantity : -t.Quantity);
                    
                    // Average Buy Price (Cost Basis)
                    // Formula: Total Paid / Total Quantity Bought
                    decimal buyQty = g.Where(t => t.Type == "BUY").Sum(t => t.Quantity);
                    decimal buyValue = g.Where(t => t.Type == "BUY").Sum(t => t.Value);
                    
                    // Latest Price (for valuation)
                    // We take the unit price of the most recent trade as a proxy for current market price
                    var latestTrade = sortedTrades.Last();
                    decimal latestPrice = latestTrade.Quantity != 0 ? (latestTrade.Value / latestTrade.Quantity) : 0;
                    
                    return new InvestmentSummary
                    {
                        Symbol = g.Key,
                        Name = g.First().StockName,
                        Quantity = netQty,
                        BuyPrice = buyQty > 0 ? Math.Round(buyValue / buyQty, 2) : 0,
                        CurrentPrice = latestPrice, // Use last trade price as initial guess for market value
                        LatestDate = latestTrade.ExecutionDate
                    };
                })
                .Where(s => s.Quantity > 0) 
                .ToList();
        }

        static void ImportToDatabase(List<InvestmentSummary> summaries, string userId)
        {
            using (var conn = new NpgsqlConnection(ConnectionString))
            {
                conn.Open();

                foreach (var item in summaries)
                {
                    Console.WriteLine($"   Processing {item.Symbol}: Qty={item.Quantity}, AvgPrice={item.BuyPrice}, CurrentEst={item.CurrentPrice}, LastTrade={item.LatestDate:yyyy-MM-dd}");

                    string checkQuery = @"SELECT ""Id"" FROM ""Stocks"" WHERE ""UserId"" = @userId AND ""Symbol"" = @symbol";
                    
                    int? existingId = null;

                    using (var cmd = new NpgsqlCommand(checkQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);
                        cmd.Parameters.AddWithValue("symbol", item.Symbol);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                existingId = reader.GetInt32(0);
                            }
                        }
                    }

                    if (existingId.HasValue)
                    {
                        string updateQuery = @"UPDATE ""Stocks"" 
                                             SET ""Quantity"" = @qty, 
                                                 ""BuyPrice"" = @buyPrice, 
                                                 ""Name"" = @name, 
                                                 ""PurchaseDate"" = @date,
                                                 ""CurrentPrice"" = CASE 
                                                    WHEN ""CurrentPrice"" <= @buyPrice THEN @currPrice 
                                                    ELSE ""CurrentPrice"" 
                                                 END
                                             WHERE ""Id"" = @id";
                        using (var cmd = new NpgsqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("qty", item.Quantity);
                            cmd.Parameters.AddWithValue("buyPrice", item.BuyPrice);
                            cmd.Parameters.AddWithValue("currPrice", item.CurrentPrice);
                            cmd.Parameters.AddWithValue("name", item.Name);
                            cmd.Parameters.AddWithValue("date", DateTime.SpecifyKind(item.LatestDate, DateTimeKind.Utc));
                            cmd.Parameters.AddWithValue("id", existingId.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        string insertQuery = @"INSERT INTO ""Stocks"" (""Name"", ""Symbol"", ""Quantity"", ""BuyPrice"", ""CurrentPrice"", ""UserId"", ""PurchaseDate"") 
                                             VALUES (@name, @symbol, @qty, @buyPrice, @currPrice, @userId, @date)";
                        using (var cmd = new NpgsqlCommand(insertQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("name", item.Name);
                            cmd.Parameters.AddWithValue("symbol", item.Symbol);
                            cmd.Parameters.AddWithValue("qty", item.Quantity);
                            cmd.Parameters.AddWithValue("buyPrice", item.BuyPrice);
                            cmd.Parameters.AddWithValue("currPrice", item.CurrentPrice);
                            cmd.Parameters.AddWithValue("userId", userId);
                            cmd.Parameters.AddWithValue("date", DateTime.SpecifyKind(item.LatestDate, DateTimeKind.Utc));
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
    }

    class StockTrade
    {
        public string StockName { get; set; }
        public string Symbol { get; set; }
        public string Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal Value { get; set; }
        public DateTime ExecutionDate { get; set; }
    }

    class InvestmentSummary
    {
        public string Symbol { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public DateTime LatestDate { get; set; }
    }
}
