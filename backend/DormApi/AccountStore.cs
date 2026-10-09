using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

internal sealed record Account(
    int Id, string Username, string FullName, string Role, byte[] PasswordSalt,
    byte[] PasswordHash, int PasswordIterations, bool IsActive, int FailedAttempts,
    DateTime? LockedUntil, int AuthVersion, bool TotpEnabled, byte[]? TotpSecret,
    byte[]? PendingSecret, DateTime? PendingExpiresAt, long? LastStep,
    string Language, string Theme);

internal static class AccountStore
{
    public const int PasswordIterations = 600_000;
    private static readonly byte[] DummySalt = Convert.FromHexString("B35D311D6D9B5D0C5A717257691E2D8C");
    private static readonly byte[] DummyHash = new byte[32];
    private const string Columns = """
        user_id, username, full_name, role_name, password_salt, password_hash,
        password_iterations, is_active, failed_attempts, locked_until, auth_version,
        totp_enabled, totp_secret, totp_pending_secret, totp_pending_expires_at,
        totp_last_step, language_code, theme_code
        """;

    public static SqlConnection Open(IConfiguration config) => new(config.GetConnectionString("DormDb"));

    public static async Task<Account?> ByUsername(SqlConnection connection, string username)
    {
        await using var command = new SqlCommand($"SELECT {Columns} FROM dbo.UserAccounts WHERE username = @username", connection);
        command.Parameters.Add("@username", SqlDbType.NVarChar, 50).Value = username;
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Read(reader) : null;
    }

    public static async Task<Account?> ById(SqlConnection connection, int id)
    {
        await using var command = new SqlCommand($"SELECT {Columns} FROM dbo.UserAccounts WHERE user_id = @id", connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Read(reader) : null;
    }

    private static Account Read(SqlDataReader reader) => new(
        reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        (byte[])reader[4], (byte[])reader[5], reader.GetInt32(6), reader.GetBoolean(7),
        reader.GetInt32(8), reader.IsDBNull(9) ? null : reader.GetDateTime(9),
        reader.GetInt32(10), reader.GetBoolean(11), reader.IsDBNull(12) ? null : (byte[])reader[12],
        reader.IsDBNull(13) ? null : (byte[])reader[13],
        reader.IsDBNull(14) ? null : reader.GetDateTime(14),
        reader.IsDBNull(15) ? null : reader.GetInt64(15), reader.GetString(16), reader.GetString(17));

    public static bool PasswordMatches(Account? account, string password)
    {
        var salt = account?.PasswordSalt ?? DummySalt;
        var storedHash = account?.PasswordHash ?? DummyHash;
        var iterations = account?.PasswordIterations ?? PasswordIterations;
        var candidate = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, storedHash.Length);
        return account is not null && CryptographicOperations.FixedTimeEquals(candidate, storedHash);
    }

    public static async Task RecordFailure(SqlConnection connection, int id)
    {
        await using var command = new SqlCommand("""
            UPDATE dbo.UserAccounts SET failed_attempts = failed_attempts + 1,
                locked_until = CASE WHEN failed_attempts + 1 >= 5
                    THEN DATEADD(MINUTE, 15, SYSUTCDATETIME()) ELSE locked_until END
            WHERE user_id = @id
            """, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await command.ExecuteNonQueryAsync();
    }

    public static async Task ResetFailures(SqlConnection connection, int id)
    {
        await using var command = new SqlCommand("UPDATE dbo.UserAccounts SET failed_attempts = 0, locked_until = NULL WHERE user_id = @id", connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await command.ExecuteNonQueryAsync();
    }

    public static async Task RehashPassword(SqlConnection connection, int id, string password, bool invalidateSessions = false)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32);
        await using var command = new SqlCommand("""
            UPDATE dbo.UserAccounts SET password_salt = @salt, password_hash = @hash,
                password_iterations = @iterations, auth_version = auth_version + @increment
            WHERE user_id = @id
            """, connection);
        command.Parameters.Add("@salt", SqlDbType.VarBinary, 16).Value = salt;
        command.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = hash;
        command.Parameters.Add("@iterations", SqlDbType.Int).Value = PasswordIterations;
        command.Parameters.Add("@increment", SqlDbType.Int).Value = invalidateSessions ? 1 : 0;
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await command.ExecuteNonQueryAsync();
    }
}
