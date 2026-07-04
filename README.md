# ExpenseTracker

ExpenseTracker is a comprehensive personal finance and investment tracking application built with ASP.NET Core MVC and PostgreSQL. It is designed to help users take control of their finances by tracking everyday expenses, managing multiple accounts, analyzing investment portfolios, and planning for debt reduction.

## Features

- **Dashboard & Analytics**: Visual representation of your financial health, spending patterns, and net worth using interactive charts.
- **Quick Entry**: A streamlined, fast interface for logging daily transactions. Features inline category editing and dynamic UI updates for rapid data entry.
- **Account Management**: Support for multiple account sources (Checking, Savings, Credit Cards, etc.).
- **Category Management**: Hierarchical categories (parent/subcategories) with the ability to manage them directly from the entry screens.
- **Investment Tracking**: Detailed portfolio management. Track stocks, EPF (Employee Provident Fund), and other investments.
- **Stock Analysis**: Built-in tools for both Fundamental and Technical analysis of stocks, powered by background importer services.
- **Statement Imports**: Easily import bank statements via CSV for automated transaction logging.
- **Debt Reduction Simulator**: Visualize the impact of different debt payoff strategies (Snowball vs. Avalanche) and compare them with investing strategies.
- **Security**: Robust authentication and authorization using ASP.NET Core Identity, with built-in support for PIN-protected viewing of sensitive transactions (e.g., Salary).

## Technology Stack

- **Backend**: ASP.NET Core 8.0 MVC
- **Data Access**: Entity Framework Core
- **Database**: PostgreSQL
- **Frontend**: HTML5, CSS3, JavaScript (Vanilla & jQuery), Bootstrap 5, Chart.js

## Project Structure

- `ExpenseTracker/`: The main ASP.NET Core MVC web application containing the user interface, controllers, and business logic.
- `Importer/StockImporter/`: A standalone console application responsible for importing and updating stock data, prices, and analysis metrics.

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/download/)

### Setup Instructions

1. **Clone the repository:**
   ```bash
   git clone <repository-url>
   cd ExpenseTracker
   ```

2. **Configure the Database:**
   Ensure PostgreSQL is running. Update the `DefaultConnection` string in `ExpenseTracker/appsettings.json` or `appsettings.Development.json` with your PostgreSQL credentials.

   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Database=ExpenseTrackerDb;Username=postgres;Password=yourpassword"
   }
   ```

3. **Run the Application:**
   Navigate to the web project directory and run the app. The application is configured to automatically create the database, apply necessary schema changes, and seed initial data (including roles and an admin user) on startup.

   ```bash
   cd ExpenseTracker
   dotnet run
   ```

4. **Login:**
   - **Default Admin Email:** `amosgnanaraj@gmail.com`
   - **Default Password:** `Password123!`

   *(Note: It is highly recommended to change this password after your first login if deploying to a public environment.)*

## Features Highlights

### Quick Entry Screen
The Quick Entry screen is designed for speed. It allows users to quickly select transaction types, amounts, and categories. The grid view supports inline editing—simply click a category badge in the "Recent Transactions" table to change it on the fly without page reloads or popups.

### Stock Importer
The `StockImporter` project is a background tool that fetches the latest stock prices and fundamental data, feeding it into the main database for portfolio analysis. It uses standard HTTP clients and background processing to ensure your investment data is up-to-date.

## License

This project is licensed under the MIT License.
