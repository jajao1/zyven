using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;
using Zyven.Application;
using Zyven.Domain;
using Zyven.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
var database = builder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
builder.Services.AddDbContext<ZyvenDbContext>(o => o.UseNpgsql(database));
if (args.Contains("--migrate"))
{
    await using var migrator = builder.Build();
    await using var scope = migrator.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ZyvenDbContext>().Database.MigrateAsync();
    return;
}
var jwtKey = builder.Configuration["Jwt:SigningKey"] ?? "";
if (Encoding.UTF8.GetByteCount(jwtKey) < 32) throw new InvalidOperationException("Jwt:SigningKey must have at least 32 bytes.");
if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]) || string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"])) throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience are required.");
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (origins.Length == 0 || origins.Any(x => x == "*" || !Uri.TryCreate(x, UriKind.Absolute, out _))) throw new InvalidOperationException("Cors:AllowedOrigins requires explicit origins.");
builder.Host.UseSerilog((_, logger) => logger.MinimumLevel.Information().MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning).Enrich.FromLogContext().WriteTo.Console());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.")));
builder.Services.Configure<PasswordHasherOptions>(o => o.IterationCount = 210000);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuthService>(); builder.Services.AddSingleton<AuthRateGate>();
builder.Services.AddSingleton<RegisterValidator>(); builder.Services.AddSingleton<LoginValidator>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new() { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"], IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ClockSkew = TimeSpan.Zero, ValidAlgorithms = [SecurityAlgorithms.HmacSha256] };
    o.Events = new()
    {
        OnTokenValidated = async context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirstValue("sid"), out var sid) || !Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var uid)) { context.Fail("Invalid session."); return; }
            var db = context.HttpContext.RequestServices.GetRequiredService<ZyvenDbContext>();
            var now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            if (!await db.Sessions.AsNoTracking().AnyAsync(s => s.Id == sid && s.UserId == uid && s.RevokedAt == null && s.ExpiresAt > now, context.HttpContext.RequestAborted)) context.Fail("Inactive session.");
        }
    };
});
builder.Services.AddAuthorization(); builder.Services.AddProblemDetails(); builder.Services.AddOpenApi();
var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    // Generate the identifier server-side; never reflect untrusted header data into logs.
    var id = Guid.NewGuid().ToString("N"); context.TraceIdentifier = id;
    context.Response.Headers["X-Correlation-ID"] = id;
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Cache-Control"] = "no-store";
    using (Serilog.Context.LogContext.PushProperty("CorrelationId", id)) await next();
});
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseCors();
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/api/auth"))
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (context.Request.Headers["X-Zyven-Client"] != "web" || (origin.Length > 0 && !origins.Contains(origin, StringComparer.OrdinalIgnoreCase))) { context.Response.StatusCode = 403; return; }
        var gate = context.RequestServices.GetRequiredService<AuthRateGate>();
        if (!await gate.Allow("ip", context.Connection.RemoteIpAddress?.ToString() ?? "unknown", 120)) { context.Response.StatusCode = 429; context.Response.Headers.RetryAfter = "900"; return; }
    }
    await next();
});
app.UseAuthentication(); app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/ready", async (ZyvenDbContext db, IConnectionMultiplexer redis, CancellationToken ct) =>
{
    try { if (!await db.Database.CanConnectAsync(ct)) return Results.StatusCode(503); await redis.GetDatabase().PingAsync(); return Results.Ok(new { status = "healthy" }); }
    catch { return Results.StatusCode(503); }
});
app.MapPost("/api/auth/register", async (RegisterRequest input, RegisterValidator validator, AuthService auth, HttpContext context, CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(input, ct); if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    var grant = await auth.Register(input, ct); if (grant is null) return Results.Problem(statusCode: 409, title: "Registration could not be completed.");
    SetCookie(context, grant, app.Environment.IsDevelopment()); return Results.Ok(grant.Response);
});
app.MapPost("/api/auth/login", async (LoginRequest input, LoginValidator validator, AuthService auth, AuthRateGate gate, HttpContext context, CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(input, ct); if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
    if (!await gate.Allow("account", AuthService.NormalizeEmail(input.Email), 20)) { context.Response.Headers.RetryAfter = "900"; return Results.StatusCode(429); }
    var grant = await auth.Login(input, ct); if (grant is null) return Results.Problem(statusCode: 401, title: "Invalid email or password.");
    SetCookie(context, grant, app.Environment.IsDevelopment()); return Results.Ok(grant.Response);
});
app.MapPost("/api/auth/refresh", async (AuthService auth, HttpContext context, CancellationToken ct) =>
{
    var raw = context.Request.Cookies["zyven_refresh"];
    var grant = raw is null ? null : await auth.Refresh(raw, ct);
    if (grant is null) { ClearCookie(context, app.Environment.IsDevelopment()); return Results.Unauthorized(); }
    SetCookie(context, grant, app.Environment.IsDevelopment()); return Results.Ok(grant.Response);
});
app.MapPost("/api/auth/logout", async (AuthService auth, HttpContext context, CancellationToken ct) =>
{
    await auth.Logout(Guid.Parse(context.User.FindFirstValue("sid")!), ct);
    ClearCookie(context, app.Environment.IsDevelopment()); return Results.NoContent();
}).RequireAuthorization();
app.MapGet("/api/auth/me", async (HttpContext context, ZyvenDbContext db, CancellationToken ct) =>
{
    var id = Guid.Parse(context.User.FindFirstValue("sub")!);
    return await db.Users.Where(x => x.Id == id).Select(x => new UserResponse(x.Id, x.Email, x.DisplayName)).SingleAsync(ct);
}).RequireAuthorization();
await app.RunAsync();
static CookieOptions CookieOptions(bool development) => new() { HttpOnly = true, Secure = !development, SameSite = SameSiteMode.Strict, Path = "/api/auth", IsEssential = true };
static void SetCookie(HttpContext context, AuthGrant grant, bool development) { var options = CookieOptions(development); options.Expires = grant.ExpiresAt; context.Response.Cookies.Append("zyven_refresh", grant.RefreshToken, options); }
static void ClearCookie(HttpContext context, bool development) => context.Response.Cookies.Delete("zyven_refresh", CookieOptions(development));
public partial class Program { }
