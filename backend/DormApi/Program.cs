using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

if (!OperatingSystem.IsWindows())
    throw new PlatformNotSupportedException("The LocalDB demo requires Windows.");

builder.Services.AddDataProtection()
    .SetApplicationName("KtxDemo")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys")))
    .ProtectKeysWithDpapi();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "KtxDemo.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events.OnValidatePrincipal = AuthEndpoints.ValidatePrincipal;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    })
    .AddCookie(AuthEndpoints.PreAuthScheme, options =>
    {
        options.Cookie.Name = "KtxDemo.PreAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("sensitive", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
var allowedOrigins = new HashSet<string>(
    (builder.Configuration["ALLOWED_ORIGINS"] ?? builder.Configuration["AllowedOrigins"] ??
     "http://localhost:4200,http://localhost:4201,http://127.0.0.1:4200,http://127.0.0.1:4201")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);
app.Use(async (context, next) =>
{
    if (context.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
    {
        var origin = context.Request.Headers.Origin.ToString();
        var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
        if ((!string.IsNullOrEmpty(origin) && !allowedOrigins.Contains(origin)) ||
            (string.IsNullOrEmpty(origin) && !string.IsNullOrEmpty(fetchSite) &&
             fetchSite is not ("same-origin" or "none")))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }
    await next();
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapSettingsEndpoints();

app.MapGet("/api/dashboard/summary", async (IConfiguration config) =>
{
    await using var connection = new SqlConnection(config.GetConnectionString("DormDb"));
    await connection.OpenAsync();
    await using var command = new SqlCommand("""
        SELECT
            (SELECT COUNT(*) FROM dbo.Students) AS total_students,
            (SELECT COUNT(*) FROM dbo.Rooms) AS total_rooms,
            (SELECT COUNT(*) FROM dbo.Stays WHERE stay_status = 'ACTIVE') AS occupied_beds,
            (SELECT COUNT(*) FROM dbo.MaintenanceRequests WHERE request_status <> 'RESOLVED') AS open_requests,
            (SELECT COALESCE(SUM(balance_due), 0) FROM dbo.vw_InvoiceBalances) AS outstanding_balance
        """, connection);
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    return Results.Ok(new DashboardSummary(
        reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetDecimal(4)));
}).RequireAuthorization();

app.MapAdminEndpoints();

app.Run();

record DashboardSummary(int TotalStudents, int TotalRooms, int OccupiedBeds, int OpenRequests, decimal OutstandingBalance);
