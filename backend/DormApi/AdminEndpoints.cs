using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;

internal static class AdminEndpoints
{
    private record Field(string Property, string Column, SqlDbType Type, bool Required = true, int Length = 0);
    private record Resource(string Table, string Key, string ListSql, Field[] Fields);

    private static readonly Dictionary<string, Resource> Resources = new(StringComparer.OrdinalIgnoreCase)
    {
        ["buildings"] = new("dbo.Buildings", "building_id", """
            SELECT building_id AS id, building_code AS buildingCode, building_name AS buildingName,
                   address, gender_policy AS genderPolicy, floor_count AS floorCount
            FROM dbo.Buildings ORDER BY building_code
            """, [
                new("buildingCode", "building_code", SqlDbType.VarChar, Length: 10),
                new("buildingName", "building_name", SqlDbType.NVarChar, Length: 100),
                new("address", "address", SqlDbType.NVarChar, Length: 200),
                new("genderPolicy", "gender_policy", SqlDbType.VarChar, Length: 10),
                new("floorCount", "floor_count", SqlDbType.Int)
            ]),
        ["rooms"] = new("dbo.Rooms", "room_id", """
            SELECT r.room_id AS id, r.building_id AS buildingId, b.building_code AS buildingCode,
                   CONCAT(b.building_code, ' / ', r.room_number) AS label,
                   r.room_number AS roomNumber, r.floor_number AS floorNumber,
                   r.monthly_rate AS monthlyRate, r.room_status AS roomStatus,
                   COALESCE(o.total_beds, 0) AS totalBeds,
                   COALESCE(o.occupied_beds, 0) AS occupiedBeds,
                   COALESCE(o.available_beds, 0) AS availableBeds
            FROM dbo.Rooms r JOIN dbo.Buildings b ON b.building_id = r.building_id
            LEFT JOIN dbo.vw_RoomOccupancy o ON o.room_id = r.room_id
            ORDER BY b.building_code, r.room_number
            """, [
                new("buildingId", "building_id", SqlDbType.Int),
                new("roomNumber", "room_number", SqlDbType.VarChar, Length: 10),
                new("floorNumber", "floor_number", SqlDbType.Int),
                new("monthlyRate", "monthly_rate", SqlDbType.Decimal),
                new("roomStatus", "room_status", SqlDbType.VarChar, Length: 12)
            ]),
        ["beds"] = new("dbo.Beds", "bed_id", """
            SELECT bed.bed_id AS id, bed.room_id AS roomId,
                   CONCAT(b.building_code, ' / ', r.room_number) AS roomLabel,
                   CONCAT(b.building_code, ' / ', r.room_number, ' / Giường ', bed.bed_number) AS label,
                   bed.bed_number AS bedNumber, bed.bed_status AS bedStatus,
                   st.full_name AS occupantName
            FROM dbo.Beds bed JOIN dbo.Rooms r ON r.room_id = bed.room_id
            JOIN dbo.Buildings b ON b.building_id = r.building_id
            LEFT JOIN dbo.Stays s ON s.bed_id = bed.bed_id AND s.stay_status = 'ACTIVE'
            LEFT JOIN dbo.Students st ON st.student_id = s.student_id
            ORDER BY b.building_code, r.room_number, bed.bed_number
            """, [
                new("roomId", "room_id", SqlDbType.Int),
                new("bedNumber", "bed_number", SqlDbType.VarChar, Length: 10),
                new("bedStatus", "bed_status", SqlDbType.VarChar, Length: 12)
            ]),
        ["students"] = new("dbo.Students", "student_id", """
            SELECT st.student_id AS id, st.student_code AS studentCode, st.full_name AS fullName,
                   st.date_of_birth AS dateOfBirth, st.gender, st.phone, st.email, st.faculty,
                   st.student_status AS studentStatus,
                   CASE WHEN s.stay_id IS NULL THEN NULL
                        ELSE CONCAT(b.building_code, ' / ', r.room_number, ' / ', bed.bed_number)
                   END AS currentBed
            FROM dbo.Students st
            LEFT JOIN dbo.Stays s ON s.student_id = st.student_id AND s.stay_status = 'ACTIVE'
            LEFT JOIN dbo.Beds bed ON bed.bed_id = s.bed_id
            LEFT JOIN dbo.Rooms r ON r.room_id = bed.room_id
            LEFT JOIN dbo.Buildings b ON b.building_id = r.building_id
            ORDER BY st.student_code
            """, [
                new("studentCode", "student_code", SqlDbType.VarChar, Length: 20),
                new("fullName", "full_name", SqlDbType.NVarChar, Length: 100),
                new("dateOfBirth", "date_of_birth", SqlDbType.Date),
                new("gender", "gender", SqlDbType.VarChar, Length: 6),
                new("phone", "phone", SqlDbType.VarChar, false, 20),
                new("email", "email", SqlDbType.VarChar, false, 254),
                new("faculty", "faculty", SqlDbType.NVarChar, Length: 100),
                new("studentStatus", "student_status", SqlDbType.VarChar, Length: 8)
            ]),
        ["stays"] = new("dbo.Stays", "stay_id", """
            SELECT s.stay_id AS id, s.student_id AS studentId, st.student_code AS studentCode,
                   st.full_name AS studentName, s.bed_id AS bedId,
                   CONCAT(b.building_code, ' / ', r.room_number, ' / Giường ', bed.bed_number) AS bedLabel,
                   s.start_date AS startDate, s.end_date AS endDate, s.stay_status AS stayStatus
            FROM dbo.Stays s JOIN dbo.Students st ON st.student_id = s.student_id
            JOIN dbo.Beds bed ON bed.bed_id = s.bed_id
            JOIN dbo.Rooms r ON r.room_id = bed.room_id
            JOIN dbo.Buildings b ON b.building_id = r.building_id
            ORDER BY s.start_date DESC, s.stay_id DESC
            """, [
                new("studentId", "student_id", SqlDbType.Int),
                new("bedId", "bed_id", SqlDbType.Int),
                new("startDate", "start_date", SqlDbType.Date),
                new("endDate", "end_date", SqlDbType.Date, false),
                new("stayStatus", "stay_status", SqlDbType.VarChar, Length: 9)
            ]),
        ["invoices"] = new("dbo.Invoices", "invoice_id", """
            SELECT i.invoice_id AS id, i.stay_id AS stayId,
                   st.student_code AS studentCode, st.full_name AS studentName,
                   CONCAT(b.building_code, ' / ', r.room_number) AS roomLabel,
                   i.billing_month AS billingMonth, i.amount, i.due_date AS dueDate,
                   bal.paid_amount AS paidAmount, bal.balance_due AS balanceDue,
                   bal.payment_status AS paymentStatus
            FROM dbo.Invoices i JOIN dbo.Stays s ON s.stay_id = i.stay_id
            JOIN dbo.Students st ON st.student_id = s.student_id
            JOIN dbo.Beds bed ON bed.bed_id = s.bed_id
            JOIN dbo.Rooms r ON r.room_id = bed.room_id
            JOIN dbo.Buildings b ON b.building_id = r.building_id
            JOIN dbo.vw_InvoiceBalances bal ON bal.invoice_id = i.invoice_id
            ORDER BY i.billing_month DESC, i.invoice_id DESC
            """, [
                new("stayId", "stay_id", SqlDbType.Int),
                new("billingMonth", "billing_month", SqlDbType.Date),
                new("amount", "amount", SqlDbType.Decimal),
                new("dueDate", "due_date", SqlDbType.Date)
            ]),
        ["payments"] = new("dbo.Payments", "payment_id", """
            SELECT p.payment_id AS id, p.invoice_id AS invoiceId,
                   CONCAT(st.student_code, ' / ', CONVERT(VARCHAR(7), i.billing_month, 120)) AS invoiceLabel,
                   st.full_name AS studentName, p.amount, p.paid_at AS paidAt,
                   p.payment_method AS paymentMethod, p.reference_code AS referenceCode
            FROM dbo.Payments p JOIN dbo.Invoices i ON i.invoice_id = p.invoice_id
            JOIN dbo.Stays s ON s.stay_id = i.stay_id
            JOIN dbo.Students st ON st.student_id = s.student_id
            ORDER BY p.paid_at DESC, p.payment_id DESC
            """, [
                new("invoiceId", "invoice_id", SqlDbType.Int),
                new("amount", "amount", SqlDbType.Decimal),
                new("paidAt", "paid_at", SqlDbType.DateTime2),
                new("paymentMethod", "payment_method", SqlDbType.VarChar, Length: 15),
                new("referenceCode", "reference_code", SqlDbType.VarChar, false, 50)
            ]),
        ["maintenance"] = new("dbo.MaintenanceRequests", "request_id", """
            SELECT mr.request_id AS id, mr.room_id AS roomId,
                   CONCAT(b.building_code, ' / ', r.room_number) AS roomLabel,
                   mr.reported_by_student_id AS reportedByStudentId,
                   st.full_name AS studentName, mr.title, mr.description,
                   mr.priority, mr.request_status AS requestStatus,
                   mr.created_at AS createdAt, mr.resolved_at AS resolvedAt
            FROM dbo.MaintenanceRequests mr JOIN dbo.Rooms r ON r.room_id = mr.room_id
            JOIN dbo.Buildings b ON b.building_id = r.building_id
            LEFT JOIN dbo.Students st ON st.student_id = mr.reported_by_student_id
            ORDER BY CASE WHEN mr.request_status = 'RESOLVED' THEN 1 ELSE 0 END,
                     mr.created_at DESC
            """, [
                new("roomId", "room_id", SqlDbType.Int),
                new("reportedByStudentId", "reported_by_student_id", SqlDbType.Int, false),
                new("title", "title", SqlDbType.NVarChar, Length: 150),
                new("description", "description", SqlDbType.NVarChar, false, 1000),
                new("priority", "priority", SqlDbType.VarChar, Length: 6),
                new("requestStatus", "request_status", SqlDbType.VarChar, Length: 11),
                new("resolvedAt", "resolved_at", SqlDbType.DateTime2, false)
            ])
    };

    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization();
        group.MapGet("/{resource}", List);
        group.MapPost("/{resource}", Create);
        group.MapPut("/{resource}/{id:int}", Update);
        group.MapDelete("/{resource}/{id:int}", Delete);
    }

    private static async Task<IResult> List(string resource, IConfiguration config)
    {
        if (!Resources.TryGetValue(resource, out var definition)) return Results.NotFound();
        await using var connection = OpenConnection(config);
        await connection.OpenAsync();
        await using var command = new SqlCommand(definition.ListSql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return Results.Ok(rows);
    }

    private static async Task<IResult> Create(string resource, JsonElement payload, IConfiguration config)
        => await Save(resource, null, payload, config);

    private static async Task<IResult> Update(string resource, int id, JsonElement payload, IConfiguration config)
        => await Save(resource, id, payload, config);

    private static async Task<IResult> Save(string resource, int? id, JsonElement payload, IConfiguration config)
    {
        if (!Resources.TryGetValue(resource, out var definition)) return Results.NotFound();
        if (payload.ValueKind != JsonValueKind.Object) return Error("Dữ liệu gửi lên không hợp lệ.");
        try
        {
            await using var connection = OpenConnection(config);
            await connection.OpenAsync();
            if (resource == "stays")
            {
                var stayError = await ValidateStay(connection, payload);
                if (stayError is not null) return Error(stayError);
            }
            if (resource == "payments")
            {
                var paymentError = await ValidatePayment(connection, payload, id);
                if (paymentError is not null) return Error(paymentError);
            }
            if (resource == "invoices" && id is not null)
            {
                var invoiceError = await ValidateInvoice(connection, payload, id.Value);
                if (invoiceError is not null) return Error(invoiceError);
            }

            var columns = string.Join(", ", definition.Fields.Select(f => f.Column));
            var placeholders = string.Join(", ", definition.Fields.Select(f => "@" + f.Property));
            var assignments = string.Join(", ", definition.Fields.Select(f => f.Column + " = @" + f.Property));
            var sql = id is null
                ? $"INSERT {definition.Table} ({columns}) OUTPUT INSERTED.{definition.Key} VALUES ({placeholders})"
                : $"UPDATE {definition.Table} SET {assignments} OUTPUT INSERTED.{definition.Key} WHERE {definition.Key} = @id";
            await using var command = new SqlCommand(sql, connection);
            if (id is not null) command.Parameters.Add("@id", SqlDbType.Int).Value = id.Value;
            foreach (var field in definition.Fields)
            {
                var parameter = command.Parameters.Add("@" + field.Property, field.Type);
                if (field.Length > 0) parameter.Size = field.Length;
                if (field.Type == SqlDbType.Decimal) { parameter.Precision = 12; parameter.Scale = 2; }
                parameter.Value = ConvertValue(payload, field) ?? DBNull.Value;
            }
            var savedId = await command.ExecuteScalarAsync();
            return savedId is null ? Results.NotFound() : Results.Ok(new { id = (int)savedId });
        }
        catch (ArgumentException ex) { return Error(ex.Message); }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        { return Error(resource == "stays" ? "Sinh viên hoặc giường đã có lượt ở đang hoạt động."
            : resource == "invoices" ? "Hóa đơn của lượt ở trong tháng này đã tồn tại."
            : "Mã hoặc thông tin này đã tồn tại."); }
        catch (SqlException ex) when (ex.Number is 547 or 515 or 8115 or 245)
        { return Error("Dữ liệu không hợp lệ hoặc đang được sử dụng."); }
    }

    private static async Task<IResult> Delete(string resource, int id, IConfiguration config)
    {
        if (!Resources.TryGetValue(resource, out var definition)) return Results.NotFound();
        try
        {
            await using var connection = OpenConnection(config);
            await connection.OpenAsync();
            await using var command = new SqlCommand($"DELETE FROM {definition.Table} WHERE {definition.Key} = @id", connection);
            command.Parameters.Add("@id", SqlDbType.Int).Value = id;
            return await command.ExecuteNonQueryAsync() == 0 ? Results.NotFound() : Results.NoContent();
        }
        catch (SqlException ex) when (ex.Number == 547)
        { return Error("Không thể xóa vì mục này đang được dữ liệu khác sử dụng."); }
    }

    private static object? ConvertValue(JsonElement payload, Field field)
    {
        if (!payload.TryGetProperty(field.Property, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
        {
            if (field.Property == "resolvedAt" && payload.TryGetProperty("requestStatus", out var status) && status.GetString() == "RESOLVED")
                return DateTime.Now;
            if (field.Required) throw new ArgumentException($"Vui lòng nhập {field.Property}.");
            return null;
        }
        try
        {
            return field.Type switch
            {
                SqlDbType.Int => value.GetInt32(),
                SqlDbType.Decimal => value.GetDecimal(),
                SqlDbType.Date or SqlDbType.DateTime2 => DateTime.Parse(value.GetString()!, CultureInfo.InvariantCulture),
                _ => value.GetString()!.Trim()
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException)
        { throw new ArgumentException($"Giá trị {field.Property} không hợp lệ."); }
    }

    private static async Task<string?> ValidateStay(SqlConnection connection, JsonElement payload)
    {
        var studentId = (int)ConvertValue(payload, new Field("studentId", "student_id", SqlDbType.Int))!;
        var bedId = (int)ConvertValue(payload, new Field("bedId", "bed_id", SqlDbType.Int))!;
        if (!payload.TryGetProperty("stayStatus", out var status) || status.GetString() != "ACTIVE") return null;
        await using var command = new SqlCommand("""
            SELECT st.gender, st.student_status, b.gender_policy, bed.bed_status, r.room_status
            FROM dbo.Students st CROSS JOIN dbo.Beds bed
            JOIN dbo.Rooms r ON r.room_id = bed.room_id
            JOIN dbo.Buildings b ON b.building_id = r.building_id
            WHERE st.student_id = @studentId AND bed.bed_id = @bedId
            """, connection);
        command.Parameters.Add("@studentId", SqlDbType.Int).Value = studentId;
        command.Parameters.Add("@bedId", SqlDbType.Int).Value = bedId;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return "Sinh viên hoặc giường không tồn tại.";
        if (reader.GetString(1) != "ACTIVE") return "Sinh viên đã ngừng hoạt động.";
        if (reader.GetString(3) != "AVAILABLE" || reader.GetString(4) != "AVAILABLE") return "Giường hoặc phòng đang bảo trì/đóng.";
        if (reader.GetString(2) != "MIXED" && reader.GetString(0) != reader.GetString(2)) return "Giới tính sinh viên không phù hợp với tòa nhà.";
        return null;
    }

    private static async Task<string?> ValidatePayment(SqlConnection connection, JsonElement payload, int? paymentId)
    {
        var invoiceId = (int)ConvertValue(payload, new Field("invoiceId", "invoice_id", SqlDbType.Int))!;
        var amount = (decimal)ConvertValue(payload, new Field("amount", "amount", SqlDbType.Decimal))!;
        await using var command = new SqlCommand("""
            SELECT i.amount - COALESCE(SUM(p.amount), 0)
            FROM dbo.Invoices i
            LEFT JOIN dbo.Payments p ON p.invoice_id = i.invoice_id AND p.payment_id <> @paymentId
            WHERE i.invoice_id = @invoiceId GROUP BY i.amount
            """, connection);
        command.Parameters.Add("@invoiceId", SqlDbType.Int).Value = invoiceId;
        command.Parameters.Add("@paymentId", SqlDbType.Int).Value = paymentId ?? 0;
        var remaining = await command.ExecuteScalarAsync();
        if (remaining is null) return "Hóa đơn không tồn tại.";
        if (amount <= 0 || amount > (decimal)remaining) return "Số tiền thanh toán phải lớn hơn 0 và không vượt quá công nợ.";
        return null;
    }

    private static async Task<string?> ValidateInvoice(SqlConnection connection, JsonElement payload, int invoiceId)
    {
        var amount = (decimal)ConvertValue(payload, new Field("amount", "amount", SqlDbType.Decimal))!;
        await using var command = new SqlCommand("SELECT COALESCE(SUM(amount), 0) FROM dbo.Payments WHERE invoice_id = @id", connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = invoiceId;
        var paid = (decimal)(await command.ExecuteScalarAsync())!;
        return amount < paid ? "Số tiền hóa đơn không thể nhỏ hơn số tiền đã thanh toán." : null;
    }

    private static SqlConnection OpenConnection(IConfiguration config) => new(config.GetConnectionString("DormDb"));
    private static IResult Error(string message) => Results.BadRequest(new { message });
}
