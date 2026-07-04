using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Npgsql;

namespace StockImporter
{
    public class MutualFundImporter
    {
        private const string ConnectionString = "Host=localhost;Port=5432;Database=ExpenseTrackerDB;Username=expensetracker_user;Password=EXPapp!@#";

        public static void Run()
        {
            Console.WriteLine("\n[Phase 1] Mutual Fund Import - Input Selection");
            Console.Write("Enter the path to your Mutual Fund Excel file: ");
            string filePath = Console.ReadLine()?.Trim('"');

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
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

            Console.Write("Enter the user Email to associate these funds with: ");
            string email = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(email))
            {
                Console.WriteLine("Error: Email is required.");
                return;
            }

            try
            {
                string userId = GetUserIdByEmail(email);
                if (string.IsNullOrEmpty(userId))
                {
                    Console.WriteLine($"Error: No user found with email '{email}'.");
                    return;
                }

                Console.WriteLine("\n[Phase 2] Parsing Mutual Fund Excel File...");
                var funds = ParseExcel(filePath);
                Console.WriteLine($"Found {funds.Count} valid mutual fund records.");

                Console.WriteLine("\n[Phase 3] Importing to Database...");
                ImportToDatabase(funds, userId);

                Console.WriteLine("\n========================================");
                Console.WriteLine("   Mutual Fund Import Completed!        ");
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR] {ex.Message}");
            }
        }

        private static string GetUserIdByEmail(string email)
        {
            using (var conn = new NpgsqlConnection(ConnectionString))
            {
                conn.Open();
                string query = @"SELECT ""Id"" FROM ""AspNetUsers"" WHERE ""Email"" = @email OR ""UserName"" = @email LIMIT 1";
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("email", email);
                    return cmd.ExecuteScalar()?.ToString();
                }
            }
        }

        private static List<MutualFundRecord> ParseExcel(string filePath)
        {
            var funds = new List<MutualFundRecord>();

            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed();

                // Headers are on row 21
                var headerRow = rows.FirstOrDefault(r => r.RowNumber() == 21);
                if (headerRow == null) throw new Exception("Header row (Row 21) not found.");

                int nameCol = GetColumnIndex(headerRow, "Scheme Name");
                int folioCol = GetColumnIndex(headerRow, "Folio No.");
                int categoryCol = GetColumnIndex(headerRow, "Category");
                int subCategoryCol = GetColumnIndex(headerRow, "Sub-Category");
                int unitsCol = GetColumnIndex(headerRow, "units");
                int investedCol = GetColumnIndex(headerRow, "Invested Value");
                int currentValCol = GetColumnIndex(headerRow, "Current Value");

                // Data starts from row 23
                foreach (var row in rows.Where(r => r.RowNumber() >= 23))
                {
                    try
                    {
                        string name = row.Cell(nameCol).GetValue<string>();
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        decimal units = row.Cell(unitsCol).GetValue<decimal>();
                        decimal investedValue = row.Cell(investedCol).GetValue<decimal>();
                        decimal currentValue = row.Cell(currentValCol).GetValue<decimal>();

                        if (units == 0) continue;

                        funds.Add(new MutualFundRecord
                        {
                            SchemeName = name,
                            FolioNumber = row.Cell(folioCol).GetValue<string>(),
                            Category = row.Cell(categoryCol).GetValue<string>(),
                            SubCategory = row.Cell(subCategoryCol).GetValue<string>(),
                            Units = units,
                            InvestedValue = investedValue,
                            CurrentValue = currentValue
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Skipping row {row.RowNumber()}: {ex.Message}");
                    }
                }
            }
            return funds;
        }

        private static int GetColumnIndex(IXLRow headerRow, string columnName)
        {
            var cell = headerRow.Cells().FirstOrDefault(c => c.GetValue<string>().Trim().Equals(columnName, StringComparison.OrdinalIgnoreCase));
            if (cell == null) throw new Exception($"Column '{columnName}' not found in Row 21.");
            return cell.Address.ColumnNumber;
        }

        private static void ImportToDatabase(List<MutualFundRecord> records, string userId)
        {
            using (var conn = new NpgsqlConnection(ConnectionString))
            {
                conn.Open();

                // Ensure columns exist if migration failed
                EnsureColumnsExist(conn);

                foreach (var record in records)
                {
                    decimal avgNav = record.Units != 0 ? record.InvestedValue / record.Units : 0;
                    decimal currentNav = record.Units != 0 ? record.CurrentValue / record.Units : 0;

                    Console.WriteLine($"   Processing {record.SchemeName}...");

                    string checkQuery = @"SELECT ""Id"" FROM ""MutualFunds"" WHERE ""UserId"" = @userId AND ""Name"" = @name AND ""FolioNumber"" = @folio";
                    int? existingId = null;

                    using (var cmd = new NpgsqlCommand(checkQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);
                        cmd.Parameters.AddWithValue("name", record.SchemeName);
                        cmd.Parameters.AddWithValue("folio", (object?)record.FolioNumber ?? DBNull.Value);
                        existingId = (int?)cmd.ExecuteScalar();
                    }

                    if (existingId.HasValue)
                    {
                        string updateQuery = @"UPDATE ""MutualFunds"" 
                                             SET ""Units"" = @units, 
                                                 ""AvgNAV"" = @avgNav, 
                                                 ""CurrentNAV"" = @currentNav,
                                                 ""Category"" = @cat,
                                                 ""SubCategory"" = @subCat,
                                                 ""FolioNumber"" = @folio
                                             WHERE ""Id"" = @id";
                        using (var cmd = new NpgsqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("units", record.Units);
                            cmd.Parameters.AddWithValue("avgNav", avgNav);
                            cmd.Parameters.AddWithValue("currentNav", currentNav);
                            cmd.Parameters.AddWithValue("cat", (object?)record.Category ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("subCat", (object?)record.SubCategory ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("folio", (object?)record.FolioNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("id", existingId.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        string insertQuery = @"INSERT INTO ""MutualFunds"" (""Name"", ""Units"", ""AvgNAV"", ""CurrentNAV"", ""Category"", ""SubCategory"", ""UserId"", ""PurchaseDate"", ""FolioNumber"") 
                                             VALUES (@name, @units, @avgNav, @currentNav, @cat, @subCat, @userId, @date, @folio)";
                        using (var cmd = new NpgsqlCommand(insertQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("name", record.SchemeName);
                            cmd.Parameters.AddWithValue("units", record.Units);
                            cmd.Parameters.AddWithValue("avgNav", avgNav);
                            cmd.Parameters.AddWithValue("currentNav", currentNav);
                            cmd.Parameters.AddWithValue("cat", (object?)record.Category ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("subCat", (object?)record.SubCategory ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("userId", userId);
                            cmd.Parameters.AddWithValue("date", DateTime.UtcNow);
                            cmd.Parameters.AddWithValue("folio", (object?)record.FolioNumber ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        private static void EnsureColumnsExist(NpgsqlConnection conn)
        {
            string[] columns = { "Category", "SubCategory", "FolioNumber" };
            foreach (var col in columns)
            {
                string checkColQuery = $@"SELECT COUNT(*) FROM information_schema.columns 
                                         WHERE table_name = 'MutualFunds' AND column_name = '{col}'";
                using (var cmd = new NpgsqlCommand(checkColQuery, conn))
                {
                    long count = (long)cmd.ExecuteScalar();
                    if (count == 0)
                    {
                        Console.WriteLine($"   Adding missing column {col} to MutualFunds table...");
                        string addColQuery = $@"ALTER TABLE ""MutualFunds"" ADD COLUMN ""{col}"" text";
                        using (var addCmd = new NpgsqlCommand(addColQuery, conn))
                        {
                            addCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
    }

    public class MutualFundRecord
    {
        public string SchemeName { get; set; } = string.Empty;
        public string? FolioNumber { get; set; }
        public string? Category { get; set; }
        public string? SubCategory { get; set; }
        public decimal Units { get; set; }
        public decimal InvestedValue { get; set; }
        public decimal CurrentValue { get; set; }
    }
}
