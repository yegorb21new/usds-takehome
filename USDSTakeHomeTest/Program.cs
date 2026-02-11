using Microsoft.EntityFrameworkCore;
using USDSTakeHomeTest.Data;

namespace USDSTakeHomeTest;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();

        // Add controllers (API endpoints)
        builder.Services.AddControllers();

        // Add EF Core DbContext (SQLite)
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddHttpClient();

        builder.Services.AddScoped<USDSTakeHomeTest.Services.EcfrDownloader>();
        builder.Services.AddScoped<USDSTakeHomeTest.Services.EcfrParser>();
        builder.Services.AddScoped<USDSTakeHomeTest.Services.MetricsCalculator>();
        builder.Services.AddScoped<USDSTakeHomeTest.Services.CurrentEcfrIngestService>();

        builder.Services.AddScoped<USDSTakeHomeTest.Services.CfrBulkDataClient>();
        builder.Services.AddScoped<USDSTakeHomeTest.Services.AnnualCfrIngestService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        // Map endpoints
        app.MapRazorPages();
        app.MapControllers();

        app.Run();
    }
}
