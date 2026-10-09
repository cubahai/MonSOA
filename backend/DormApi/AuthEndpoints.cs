using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;

internal static class AuthEndpoints
{
    public const string PreAuthScheme = "KtxDemo.PreAuth";
    private const string SecretPurpose = "KtxDemo.TotpSecret.v1";
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");
        group.MapPost("/login", Login).RequireRateLimiting("sensitive");
        group.MapPost("/2fa/verify", VerifyLoginCode).RequireRateLimiting("sensitive");
        group.MapGet("/me", (ClaimsPrincipal principal) => Results.Ok(ToUser(principal))).RequireAuthorization();
        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();
        group.MapGet("/2fa/status", TwoFactorStatus).RequireAuthorization();
        group.MapPost("/2fa/setup", BeginSetup).RequireAuthorization().RequireRateLimiting("sensitive");
        group.MapPost("/2fa/confirm", ConfirmSetup).RequireAuthorization().RequireRateLimiting("sensitive");
        group.MapPost("/2fa/disable", DisableTwoFactor).RequireAuthorization().RequireRateLimiting("sensitive");
        group.MapPost("/2fa/recovery/regenerate", RegenerateRecoveryCodes).RequireAuthorization().RequireRateLimiting("sensitive");
        group.MapPost("/change-password", ChangePassword).RequireAuthorization().RequireRateLimiting("sensitive");
    }

    public static async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
            !int.TryParse(context.Principal?.FindFirstValue("auth_version"), out var version))
        {
            context.RejectPrincipal();
            return;
        }
        try
        {
            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            await using var connection = AccountStore.Open(config);
            await connection.OpenAsync();
            await using var command = new SqlCommand("SELECT auth_version, is_active FROM dbo.UserAccounts WHERE user_id = @id", connection);
            command.Parameters.Add("@id", SqlDbType.Int).Value = id;
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || !reader.GetBoolean(1) || reader.GetInt32(0) != version)
                context.RejectPrincipal();
        }
        catch
        {
            context.RejectPrincipal();
        }
    }

    private static async Task<IResult> Login(LoginRequest request, HttpContext http, IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password) ||
            request.Username.Length > 50 || request.Password.Length > 256)
            return Results.BadRequest(new { message = "Vui lòng nhập tài khoản và mật khẩu hợp lệ." });

        await using var connection = AccountStore.Open(config);
        await connection.OpenAsync();
        var account = await AccountStore.ByUsername(connection, request.Username.Trim());
        var passwordMatches = AccountStore.PasswordMatches(account, request.Password);
        if (account is null || !account.IsActive) return Results.Unauthorized();
        if (account.LockedUntil is not null && account.LockedUntil > DateTime.UtcNow)
            return Locked();
        if (account.LockedUntil is not null) await AccountStore.ResetFailures(connection, account.Id);
        if (!passwordMatches)
        {
            await AccountStore.RecordFailure(connection, account.Id);
            return Results.Unauthorized();
        }

        if (account.PasswordIterations < AccountStore.PasswordIterations)
            await AccountStore.RehashPassword(connection, account.Id, request.Password);
        if (account.TotpEnabled)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
                new Claim("remember", request.RememberMe ? "1" : "0")
            };
            await http.SignInAsync(PreAuthScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, PreAuthScheme)),
                new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) });
            return Results.Ok(new LoginResult(true, null));
        }
        await AccountStore.ResetFailures(connection, account.Id);
        await SignInMain(http, account, request.RememberMe);
        return Results.Ok(new LoginResult(false, ToUser(account)));
    }

    private static async Task<IResult> VerifyLoginCode(CodeRequest request, HttpContext http,
        IConfiguration config, IDataProtectionProvider protection)
    {
        var challenge = await http.AuthenticateAsync(PreAuthScheme);
        if (!challenge.Succeeded || !int.TryParse(challenge.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return Results.Unauthorized();
        await using var connection = AccountStore.Open(config);
        await connection.OpenAsync();
        var account = await AccountStore.ById(connection, id);
        if (account is null || !account.IsActive || !account.TotpEnabled || account.TotpSecret is null)
            return Results.Unauthorized();
        if (account.LockedUntil is not null && account.LockedUntil > DateTime.UtcNow) return Locked();
        if (account.LockedUntil is not null) await AccountStore.ResetFailures(connection, id);

        if (!await VerifySecondFactor(connection, account, request.Code, protection))
        {
            await AccountStore.RecordFailure(connection, id);
            return Results.Unauthorized();
        }
        await AccountStore.ResetFailures(connection, id);
        var remember = challenge.Principal?.FindFirstValue("remember") == "1";
        await http.SignOutAsync(PreAuthScheme);
        await SignInMain(http, account, remember);
        return Results.Ok(new LoginResult(false, ToUser(account)));
    }

    private static async Task<IResult> TwoFactorStatus(ClaimsPrincipal principal, IConfiguration config)
    {
        await using var connection = AccountStore.Open(config);
        await connection.OpenAsync();
        var account = await AccountStore.ById(connection, UserId(principal));
        if (account is null) return Results.NotFound();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.UserRecoveryCodes WHERE user_id = @id AND used_at IS NULL", connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = account.Id;
        var remaining = (int)(await command.ExecuteScalarAsync())!;
        return Results.Ok(new { enabled = account.TotpEnabled, recoveryCodesRemaining = remaining });
    }

    private static async Task<IResult> BeginSetup(PasswordRequest request, ClaimsPrincipal principal,
        HttpContext http, IConfiguration config, IDataProtectionProvider protection)
    {
        NoStore(http);
        await using var connection = AccountStore.Open(config);
        await connection.OpenAsync();
        var account = await AccountStore.ById(connection, UserId(principal));
        if (account is null || account.TotpEnabled) return Bad("Xác thực hai lớp đã được bật.");
        if (string.IsNullOrEmpty(request.Password) || !AccountStore.PasswordMatches(account, request.Password))
            return Results.Unauthorized();
        var secret = TotpSecurity.NewSecret();
        var encrypted = protection.CreateProtector(SecretPurpose).Protect(secret);
        await using var command = new SqlCommand("""
            UPDATE dbo.UserAccounts SET totp_pending_secret = @secret,
                totp_pending_expires_at = DATEADD(MINUTE, 10, SYSUTCDATETIME()) WHERE user_id = @id
            """, connection);
        command.Parameters.Add("@secret", SqlDbType.VarBinary, -1).Value = encrypted;
        command.Parameters.Add("@id", SqlDbType.Int).Value = account.Id;
        await command.ExecuteNonQueryAsync();
        return Results.Ok(new
        {
            secret = TotpSecurity.Base32Encode(secret),
            provisioningUri = TotpSecurity.ProvisioningUri(account.Username, secret),
            expiresInSeconds = 600
        });
    }

    private static async Task<IResult> ConfirmSetup(CodeRequest request, ClaimsPrincipal principal,
        HttpContext http, IConfiguration config, IDataProtectionProvider protection)
    {
        NoStore(http);
        await using var connection = AccountStore.Open(config);
        await connection.OpenAsync();
        var account = await AccountStore.ById(connection, UserId(principal));
        if (account is null || account.TotpEnabled || account.PendingSecret is null ||
            account.PendingExpiresAt is null || account.PendingExpiresAt <= DateTime.UtcNow)
            return Bad("Phiên cài đặt đã hết hạn. Vui lòng bắt đầu lại.");
        var secret = protection.CreateProtector(SecretPurpose).Unprotect(account.PendingSecret);
        var step = TotpSecurity.Verify(secret, request.Code);
        if (step is null) return Bad("Mã xác thực không đúng hoặc đã hết hạn.");
        var codes = Enumerable.Range(0, 8).Select(_ => TotpSecurity.NewRecoveryCode()).ToArray();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using (var update = new SqlCommand("""
            UPDATE dbo.UserAccounts SET totp_secret = totp_pending_secret, totp_enabled = 1,
                totp_pending_secret = NULL, totp_pending_expires_at = NULL,
                totp_last_step = @step, auth_version = auth_version + 1
            WHERE user_id = @id AND totp_enabled = 0
            """, connection, transaction))
        {
            update.Parameters.Add("@step", SqlDbType.BigInt).Value = step.Value;
            update.Parameters.Add("@id", SqlDbType.Int).Value = account.Id;
            if (await update.ExecuteNonQueryAsync() != 1) return Bad("Không thể bật xác thực hai lớp.");
        }
        foreach (var code in codes)
        {
            await using var insert = new SqlCommand("INSERT dbo.UserRecoveryCodes (user_id, code_hash) VALUES (@id, @hash)", connection, transaction);
            insert.Parameters.Add("@id", SqlDbType.Int).Value = account.Id;
            insert.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = TotpSecurity.RecoveryCodeHash(code);
            await insert.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        var current = await http.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await SignInMain(http, account with { AuthVersion = account.AuthVersion + 1 }, current.Properties?.IsPersistent ?? false);
        return Results.Ok(new { recoveryCodes = codes });
    }

    private static async Task<IResult> DisableTwoFactor(PasswordAndCodeRequest request, ClaimsPrincipal principal,
        HttpContext http, IConfiguration config, IDataProtectionProvider protection)
    {
        await using var connection = AccountStore.Open(config);
        await connection.OpenAsync();
        var account = await AccountStore.ById(connection, UserId(principal));
        if (account is null || !account.TotpEnabled) return Bad("Xác thực hai lớp chưa được bật.");
        if (string.IsNullOrEmpty(request.Password) || !AccountStore.PasswordMatches(account, request.Password))
            return Results.Unauthorized();
        if (!await VerifySecondFactor(connection, account, request.Code, protection)) return Results.Unauthorized();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using (var delete = new SqlCommand("DELETE dbo.UserRecoveryCodes WHERE user_id = @id", connection, transaction))
        {
            delete.Parameters.Add("@id", SqlDbType.Int).Value = account.Id;
            await delete.ExecuteNonQueryAsync();
        }
        await using (var update = new SqlCommand("""
            UPDATE dbo.UserAccounts SET totp_enabled = 0, totp_secret = NULL,
                totp_last_step = NULL, totp_pending_secret = NULL,
                totp_pending_expires_at = NULL, auth_version = auth_version + 1
            WHERE user_id = @id
            """, connection, transaction))
        {
            update.Parameters.Add("@id", SqlDbType.Int).Value = account.Id;
            await update.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> RegenerateRecoveryCodes(PasswordAndCodeRequest request, ClaimsPrincipal principal,
        HttpContext http, IConfiguration config, IDataProtectionProvider protection)
    {
        NoStore(http);
        await using var connection = AccountStore.Open(config);
        await connection.OpenAsync();
        var account = await AccountStore.ById(connection, UserId(principal));
        if (account is null || !account.TotpEnabled) return Bad("Xác thực hai lớp chưa được bật.");
        if (string.IsNullOrEmpty(request.Password) || !AccountStore.PasswordMatches(account, request.Password))
            return Results.Unauthorized();
        if (!await VerifySecondFactor(connection, account, request.Code, protection)) return Results.Unauthorized();
        var codes = Enumerable.Range(0, 8).Select(_ => TotpSecurity.NewRecoveryCode()).ToArray();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using (var delete = new SqlCommand("DELETE dbo.UserRecoveryCodes WHERE user_id = @id", connection, transaction))
        {
            delete.Parameters.Add("@id", SqlDbType.Int).Value = account.Id;
            await delete.ExecuteNonQueryAsync();
        }
        foreach (var code in codes)
        {
            await using var insert = new SqlCommand("INSERT dbo.UserRecoveryCodes (user_id, code_hash) VALUES (@id, @hash)", connection, transaction);
            insert.Parameters.Add("@id", SqlDbType.Int).Value = account.Id;
            insert.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = TotpSecurity.RecoveryCodeHash(code);
            await insert.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        return Results.Ok(new { recoveryCodes = codes });
    }

    private static async Task<IResult> ChangePassword(ChangePasswordRequest request, ClaimsPrincipal principal,
        HttpContext http, IConfiguration config, IDataProtectionProvider protection)
    {
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length is < 12 or > 128)
            return Bad("Mật khẩu mới cần từ 12 đến 128 ký tự.");
        await using var connection = AccountStore.Open(config);
        await connection.OpenAsync();
        var account = await AccountStore.ById(connection, UserId(principal));
        if (account is null || string.IsNullOrEmpty(request.CurrentPassword) ||
            !AccountStore.PasswordMatches(account, request.CurrentPassword)) return Results.Unauthorized();
        if (request.CurrentPassword == request.NewPassword) return Bad("Mật khẩu mới phải khác mật khẩu hiện tại.");
        if (account.TotpEnabled && !await VerifySecondFactor(connection, account, request.Code, protection))
            return Results.Unauthorized();
        await AccountStore.RehashPassword(connection, account.Id, request.NewPassword, invalidateSessions: true);
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<bool> VerifySecondFactor(SqlConnection connection, Account account,
        string? code, IDataProtectionProvider protection)
    {
        if (account.TotpSecret is null || string.IsNullOrWhiteSpace(code)) return false;
        var normalized = new string(code.Where(char.IsLetterOrDigit).ToArray());
        if (normalized.Length == 16)
        {
            await using var recovery = new SqlCommand("""
                UPDATE dbo.UserRecoveryCodes SET used_at = SYSUTCDATETIME()
                WHERE user_id = @id AND code_hash = @hash AND used_at IS NULL
                """, connection);
            recovery.Parameters.Add("@id", SqlDbType.Int).Value = account.Id;
            recovery.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = TotpSecurity.RecoveryCodeHash(code);
            return await recovery.ExecuteNonQueryAsync() == 1;
        }
        if (code.Length != 6) return false;
        byte[] secret;
        try { secret = protection.CreateProtector(SecretPurpose).Unprotect(account.TotpSecret); }
        catch (CryptographicException) { return false; }
        var step = TotpSecurity.Verify(secret, code, account.LastStep);
        if (step is null) return false;
        await using var update = new SqlCommand("""
            UPDATE dbo.UserAccounts SET totp_last_step = @step
            WHERE user_id = @id AND (totp_last_step IS NULL OR totp_last_step < @step)
            """, connection);
        update.Parameters.Add("@step", SqlDbType.BigInt).Value = step.Value;
        update.Parameters.Add("@id", SqlDbType.Int).Value = account.Id;
        return await update.ExecuteNonQueryAsync() == 1;
    }

    private static async Task SignInMain(HttpContext http, Account account, bool remember)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.Username),
            new Claim(ClaimTypes.GivenName, account.FullName),
            new Claim(ClaimTypes.Role, account.Role),
            new Claim("auth_version", account.AuthVersion.ToString())
        };
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties
            {
                IsPersistent = remember,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(remember ? TimeSpan.FromDays(14) : TimeSpan.FromHours(8))
            });
    }

    private static object ToUser(Account account) => new { account.Username, account.FullName, account.Role };
    private static object ToUser(ClaimsPrincipal principal) => new
    {
        username = principal.Identity!.Name!,
        fullName = principal.FindFirstValue(ClaimTypes.GivenName) ?? "",
        role = principal.FindFirstValue(ClaimTypes.Role) ?? ""
    };
    private static int UserId(ClaimsPrincipal principal) => int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult Bad(string message) => Results.BadRequest(new { message });
    private static IResult Locked() => Results.Json(new { message = "Tài khoản tạm khóa 15 phút sau nhiều lần thử sai." }, statusCode: 429);
    private static void NoStore(HttpContext http) => http.Response.Headers.CacheControl = "no-store";

    private sealed record LoginRequest(string? Username, string? Password, bool RememberMe);
    private sealed record CodeRequest(string? Code);
    private sealed record PasswordRequest(string? Password);
    private sealed record PasswordAndCodeRequest(string? Password, string? Code);
    private sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword, string? Code);
    private sealed record LoginResult(bool RequiresTwoFactor, object? User);
}
