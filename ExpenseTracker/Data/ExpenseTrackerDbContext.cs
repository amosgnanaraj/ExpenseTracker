using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ExpenseTracker.Data
{
    public class ExpenseTrackerDbContext : IdentityDbContext<IdentityUser>
    {
        public ExpenseTrackerDbContext(DbContextOptions<ExpenseTrackerDbContext> options)
            : base(options)
        {
        }

        public DbSet<AccountSource> AccountSources { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<InvestmentType> InvestmentTypes { get; set; }
        public DbSet<Investment> Investments { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<MutualFund> MutualFunds { get; set; }
        public DbSet<FixedDeposit> FixedDeposits { get; set; }
        public DbSet<NPS> NPSs { get; set; }
        public DbSet<EPF> EPFs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Investment Type mapping
            modelBuilder.Entity<Investment>()
                .HasOne(i => i.InvestmentType)
                .WithMany(it => it.Investments)
                .HasForeignKey(i => i.InvestmentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Investment>()
                .Property(i => i.Quantity)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Investment>()
                .Property(i => i.BuyPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Investment>()
                .Property(i => i.CurrentPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Investment>()
                .Property(i => i.InterestRate)
                .HasPrecision(5, 2);

            // Configure AccountSource Type mapping to string for PostgreSQL ENUM safety if preferred
            // We use string conversion to ensure compatibility out of the box
            modelBuilder.Entity<AccountSource>()
                .Property(a => a.Type)
                .HasConversion<string>();

            // Configure Category self-referencing relationship
            modelBuilder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Transaction relationships
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.AccountSource)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountSourceId)
                .OnDelete(DeleteBehavior.Cascade);

            // TransactionType enum stored as string
            modelBuilder.Entity<Transaction>()
                .Property(t => t.Type)
                .HasConversion<string>()
                .HasDefaultValue(TransactionType.Debit);

            // Transfer destination FK (nullable)
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.TransferToAccountSource)
                .WithMany()
                .HasForeignKey(t => t.TransferToAccountSourceId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            modelBuilder.Entity<AccountSource>()
                .Property(a => a.Balance)
                .HasPrecision(18, 2);

            modelBuilder.Entity<AccountSource>()
                .Property(a => a.InterestRate)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<MutualFund>()
                .Property(m => m.Units)
                .HasPrecision(18, 4);

            modelBuilder.Entity<MutualFund>()
                .Property(m => m.AvgNAV)
                .HasPrecision(18, 4);

            modelBuilder.Entity<MutualFund>()
                .Property(m => m.CurrentNAV)
                .HasPrecision(18, 4);

            modelBuilder.Entity<FixedDeposit>()
                .Property(f => f.PayoutType)
                .HasConversion<string>();

            // Configure NPS Precisions
            modelBuilder.Entity<NPS>()
                .Property(n => n.TotalUnits)
                .HasPrecision(18, 4);
                
            modelBuilder.Entity<NPS>()
                .Property(n => n.CurrentNAV)
                .HasPrecision(18, 4);
                
            modelBuilder.Entity<NPS>()
                .Property(n => n.TotalInvested)
                .HasPrecision(18, 2);

            // Configure EPF Precisions
            modelBuilder.Entity<EPF>()
                .Property(e => e.EmployeeContribution)
                .HasPrecision(18, 2);
                
            modelBuilder.Entity<EPF>()
                .Property(e => e.EmployerContribution)
                .HasPrecision(18, 2);
                
            modelBuilder.Entity<EPF>()
                .Property(e => e.InterestEarned)
                .HasPrecision(18, 2);

            modelBuilder.Entity<EPF>()
                .Property(e => e.TransferIn)
                .HasPrecision(18, 2);
        }
    }
}
