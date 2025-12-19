using TechnicalAnalyzer.Services;
using TechnicalAnalyzer.Data;
using Microsoft.EntityFrameworkCore;

namespace TechnicalAnalyzer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            DotNetEnv.Env.Load();
            var builder = WebApplication.CreateBuilder(args);
            Console.WriteLine("ConnString: " + builder.Configuration.GetConnectionString("DefaultConnection"));
            // Register NepseApiService with HttpClient
            builder.Services.AddHttpClient<NepseApiService>();

            // Register IndicatorService (only once)
            builder.Services.AddScoped<IndicatorService>();
            builder.Services.AddScoped<MachineLearningService>();

            // Register OhlcDbContext for SQL Server
            builder.Services.AddDbContext<OhlcDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Register OhlcBackgroundService for background OHLC updates
            builder.Services.AddHostedService<TechnicalAnalyzer.Services.OhlcBackgroundService>();

            // Add MVC controllers + views
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // Show detailed errors in development
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            // Minimal API for testing tickers
            app.MapGet("/api/stocks", async (NepseApiService nepseApiService) =>
            {
                var tickers = await nepseApiService.GetStockTickersAsync();
                return Results.Ok(tickers);
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Stock}/{action=Chart}/{id?}");

            app.Run();
        }
    }
}
