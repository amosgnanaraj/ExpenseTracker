using ExpenseTracker.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ExpenseTrackerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ExpenseTrackerDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<ExpenseTracker.Services.IStockPriceService, ExpenseTracker.Services.StockPriceService>();
builder.Services.AddScoped<ExpenseTracker.Services.IInvestmentImportService, ExpenseTracker.Services.InvestmentImportService>();
builder.Services.AddScoped<ExpenseTracker.Services.ITechnicalAnalysisService, ExpenseTracker.Services.TechnicalAnalysisService>();
builder.Services.AddScoped<ExpenseTracker.Services.IFundamentalAnalysisService, ExpenseTracker.Services.FundamentalAnalysisService>();
builder.Services.AddScoped<ExpenseTracker.Services.ITransactionImportService, ExpenseTracker.Services.TransactionImportService>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
});

var app = builder.Build();

// Auto-create database and seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ExpenseTrackerDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    
    // WARNING: In a real production app, use Migrations to update schemas!
    // context.Database.EnsureDeleted(); 
    context.Database.EnsureCreated();
    
    // Add AccountNumber safely if it doesn't exist
    try
    {
        context.Database.ExecuteSqlRaw("ALTER TABLE \"AccountSources\" ADD COLUMN \"AccountNumber\" text NULL;");
    }
    catch { /* Ignore if already exists */ }

    // Run SeedData.sql if Categories table is empty
    if (!context.Categories.Any())
    {
        var seedSql = System.IO.File.ReadAllText("SeedData.sql");
        context.Database.ExecuteSqlRaw(seedSql);
    }

    // Check if new investment tables exist, if not run migration
    try
    {
        // Try to query stocks, if it fails, run migration
        context.Database.ExecuteSqlRaw("SELECT 1 FROM \"Stocks\" LIMIT 1;");
    }
    catch
    {
        Console.WriteLine("Applying Investment Table Migration...");
        var migrationSql = System.IO.File.ReadAllText("MigrateToSeparateTables.sql");
        context.Database.ExecuteSqlRaw(migrationSql);
        Console.WriteLine("Migration Applied Successfully.");
    }

    // Auth Seeding
    string[] roleNames = { "Admin", "User" };
    foreach (var roleName in roleNames)
    {
        var roleExist = roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult();
        if (!roleExist)
        {
            roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult();
        }
    }

    var adminEmail = "amosgnanaraj@gmail.com";
    var adminUser = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
    if (adminUser == null)
    {
        adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var createAdminUser = userManager.CreateAsync(adminUser, "Password123!").GetAwaiter().GetResult();
        if (createAdminUser.Succeeded)
        {
            userManager.AddToRoleAsync(adminUser, "Admin").GetAwaiter().GetResult();
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
