using Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecretProject.Services;

namespace SecretProject;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
        // create the Db file
		builder.Services.AddDbContextFactory<AppDbContext>(options =>
		{
			var dbPath = Path.Combine(FileSystem.Current.AppDataDirectory, "hourstracker.db");
			options.UseSqlite($"Data Source={dbPath}");
		});
		builder.Services.AddSingleton<HoursTrackerService>();
#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		// Make sure the SQlite file + tables exist before any page queries them.
		var dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
		using (var db = dbContextFactory.CreateDbContext())
		{
			db.Database.EnsureCreated();
		}

		return app;
	}
}
