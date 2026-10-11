using Serilog;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Zyven.Infrastructure;
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog(logger => logger.MinimumLevel.Information().MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning).WriteTo.Console());
var connection = builder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
builder.Services.AddDbContext<ZyvenDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SessionCleanup>();
builder.Services.AddScoped<CheckoutExpiration>();
builder.Services.AddScoped<AutomationRunner>();
builder.Services.AddSingleton(_ => new TelegramCredentialVault(builder.Configuration["Telegram:CredentialEncryptionKey"] ?? builder.Configuration["Payments:CredentialEncryptionKey"] ?? throw new InvalidOperationException("Telegram:CredentialEncryptionKey is required.")));
builder.Services.AddSingleton(new TelegramOptions(builder.Configuration["Telegram:PublicApiBaseUrl"] ?? ""));
builder.Services.AddHttpClient<ITelegramClient, TelegramClient>(client => { client.BaseAddress = new("https://api.telegram.org/"); client.Timeout = TimeSpan.FromSeconds(20); });
builder.Services.AddHangfire(c => c.UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connection)));
builder.Services.AddHangfireServer(o => o.WorkerCount = 2);
var host = builder.Build();
using (var scope = host.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<IRecurringJobManager>().AddOrUpdate<CheckoutExpiration>("expired-checkouts", x => x.Run(CancellationToken.None), "*/5 * * * *");
    scope.ServiceProvider.GetRequiredService<IRecurringJobManager>().AddOrUpdate<SessionCleanup>("expired-auth-sessions", x => x.Run(CancellationToken.None), Cron.Hourly());
    scope.ServiceProvider.GetRequiredService<IRecurringJobManager>().AddOrUpdate<AutomationRunner>("due-automation-executions", x => x.ResumeDue(CancellationToken.None), "* * * * *");
}
await host.RunAsync();
