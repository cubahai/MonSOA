using System.Data;
using System.Security.Claims;
using Microsoft.Data.SqlClient;

internal static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings").RequireAuthorization();
        group.MapGet("/", GetSettings);
        group.MapPut("/", UpdateSettings);
    }

    private static async Task<IResult> GetSettings(ClaimsPrincipal principal, IConfiguration config)
    {
        await using var connection = AccountStore.Open(config);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT language_code, theme_code FROM dbo.UserAccounts WHERE user_id = @id", connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = UserId(principal);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return Results.NotFound();
        return Results.Ok(new Settings(reader.GetString(0), reader.GetString(1)));
    }

    private static async Task<IResult> UpdateSettings(Settings settings, ClaimsPrincipal principal, IConfiguration config)
    {
        if (settings.Language is not ("vi" or "en" or "zh") ||
            settings.Theme is not ("light" or "dark" or "system"))
            return Results.BadRequest(new { message = "Ngôn ngữ hoặc giao diện không hợp lệ." });
        await using var connection = AccountStore.Open(config);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "UPDATE dbo.UserAccounts SET language_code = @language, theme_code = @theme WHERE user_id = @id", connection);
        command.Parameters.Add("@language", SqlDbType.VarChar, 2).Value = settings.Language;
        command.Parameters.Add("@theme", SqlDbType.VarChar, 10).Value = settings.Theme;
        command.Parameters.Add("@id", SqlDbType.Int).Value = UserId(principal);
        return await command.ExecuteNonQueryAsync() == 1 ? Results.Ok(settings) : Results.NotFound();
    }

    private static int UserId(ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private sealed record Settings(string Language, string Theme);
}
