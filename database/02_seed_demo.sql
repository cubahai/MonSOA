USE [KTX_Demo];
GO

SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM dbo.Buildings)
        THROW 50001, 'Demo data already exists. Run this seed only on a new database.', 1;

    INSERT dbo.Buildings (building_code, building_name, address, gender_policy, floor_count)
    VALUES
        ('A', N'Tòa A - Nam', N'12 đường Đại học, TP. Hồ Chí Minh', 'MALE', 3),
        ('B', N'Tòa B - Nữ', N'14 đường Đại học, TP. Hồ Chí Minh', 'FEMALE', 3);

    INSERT dbo.Rooms (building_id, room_number, floor_number, monthly_rate)
    SELECT b.building_id, x.room_number, x.floor_number, x.monthly_rate
    FROM (VALUES
        ('A', '101', 1, CAST(650000 AS DECIMAL(12,2))),
        ('A', '102', 1, CAST(650000 AS DECIMAL(12,2))),
        ('A', '201', 2, CAST(750000 AS DECIMAL(12,2))),
        ('B', '101', 1, CAST(650000 AS DECIMAL(12,2))),
        ('B', '102', 1, CAST(650000 AS DECIMAL(12,2))),
        ('B', '201', 2, CAST(750000 AS DECIMAL(12,2)))
    ) AS x(building_code, room_number, floor_number, monthly_rate)
    JOIN dbo.Buildings AS b ON b.building_code = x.building_code;

    INSERT dbo.Beds (room_id, bed_number)
    SELECT r.room_id, x.bed_number
    FROM dbo.Rooms AS r
    CROSS JOIN (VALUES ('01'), ('02')) AS x(bed_number);

    INSERT dbo.Students (student_code, full_name, date_of_birth, gender, phone, email, faculty)
    VALUES
        ('SV001', N'Nguyễn Minh Anh', '2005-03-12', 'MALE', '0901000001', 'sv001@example.edu.vn', N'Công nghệ thông tin'),
        ('SV002', N'Trần Quốc Bảo', '2004-11-24', 'MALE', '0901000002', 'sv002@example.edu.vn', N'Công nghệ thông tin'),
        ('SV003', N'Lê Hoàng Nam', '2005-07-08', 'MALE', '0901000003', 'sv003@example.edu.vn', N'Kinh tế'),
        ('SV004', N'Phạm Gia Huy', '2006-01-19', 'MALE', '0901000004', 'sv004@example.edu.vn', N'Kỹ thuật điện'),
        ('SV005', N'Nguyễn Thảo Vy', '2005-05-30', 'FEMALE', '0901000005', 'sv005@example.edu.vn', N'Ngoại ngữ'),
        ('SV006', N'Võ Ngọc Hà', '2004-09-02', 'FEMALE', '0901000006', 'sv006@example.edu.vn', N'Kinh tế'),
        ('SV007', N'Đặng Khánh Linh', '2005-12-17', 'FEMALE', '0901000007', 'sv007@example.edu.vn', N'Công nghệ thông tin'),
        ('SV008', N'Bùi Mai Chi', '2006-04-11', 'FEMALE', '0901000008', 'sv008@example.edu.vn', N'Ngoại ngữ');

    INSERT dbo.Stays (student_id, bed_id, start_date)
    SELECT st.student_id, bed.bed_id, CAST('2026-09-01' AS DATE)
    FROM (VALUES
        ('SV001', 'A', '101', '01'),
        ('SV002', 'A', '101', '02'),
        ('SV003', 'A', '201', '01'),
        ('SV005', 'B', '101', '01'),
        ('SV006', 'B', '101', '02'),
        ('SV007', 'B', '201', '01')
    ) AS x(student_code, building_code, room_number, bed_number)
    JOIN dbo.Students AS st ON st.student_code = x.student_code
    JOIN dbo.Buildings AS b ON b.building_code = x.building_code
    JOIN dbo.Rooms AS r ON r.building_id = b.building_id AND r.room_number = x.room_number
    JOIN dbo.Beds AS bed ON bed.room_id = r.room_id AND bed.bed_number = x.bed_number;

    INSERT dbo.Invoices (stay_id, billing_month, amount, due_date)
    SELECT s.stay_id, '2026-10-01', r.monthly_rate, '2026-10-10'
    FROM dbo.Stays AS s
    JOIN dbo.Beds AS bed ON bed.bed_id = s.bed_id
    JOIN dbo.Rooms AS r ON r.room_id = bed.room_id;

    INSERT dbo.Payments (invoice_id, amount, paid_at, payment_method, reference_code)
    SELECT i.invoice_id, x.amount, x.paid_at, x.payment_method, x.reference_code
    FROM (VALUES
        ('SV001', CAST(650000 AS DECIMAL(12,2)), CAST('2026-10-03T09:00:00' AS DATETIME2(0)), 'BANK_TRANSFER', 'DEMO-001'),
        ('SV005', CAST(650000 AS DECIMAL(12,2)), CAST('2026-10-04T10:30:00' AS DATETIME2(0)), 'CASH', 'DEMO-002'),
        ('SV003', CAST(300000 AS DECIMAL(12,2)), CAST('2026-10-05T14:00:00' AS DATETIME2(0)), 'BANK_TRANSFER', 'DEMO-003')
    ) AS x(student_code, amount, paid_at, payment_method, reference_code)
    JOIN dbo.Students AS st ON st.student_code = x.student_code
    JOIN dbo.Stays AS s ON s.student_id = st.student_id
    JOIN dbo.Invoices AS i ON i.stay_id = s.stay_id AND i.billing_month = '2026-10-01';

    INSERT dbo.MaintenanceRequests
        (room_id, reported_by_student_id, title, description, priority, request_status, created_at, resolved_at)
    SELECT r.room_id, st.student_id, x.title, x.description, x.priority, x.request_status, x.created_at, x.resolved_at
    FROM (VALUES
        ('A', '101', 'SV001', N'Đèn phòng bị hỏng', N'Đèn trần không sáng.', 'MEDIUM', 'OPEN', CAST('2026-10-06T08:00:00' AS DATETIME2(0)), CAST(NULL AS DATETIME2(0))),
        ('B', '201', 'SV007', N'Vòi nước bị rò', N'Vòi nước trong phòng tắm bị rò.', 'HIGH', 'IN_PROGRESS', CAST('2026-10-07T11:00:00' AS DATETIME2(0)), CAST(NULL AS DATETIME2(0))),
        ('B', '101', 'SV005', N'Khóa cửa bị kẹt', N'Đã thay ổ khóa.', 'LOW', 'RESOLVED', CAST('2026-10-01T09:00:00' AS DATETIME2(0)), CAST('2026-10-02T15:00:00' AS DATETIME2(0)))
    ) AS x(building_code, room_number, student_code, title, description, priority, request_status, created_at, resolved_at)
    JOIN dbo.Buildings AS b ON b.building_code = x.building_code
    JOIN dbo.Rooms AS r ON r.building_id = b.building_id AND r.room_number = x.room_number
    JOIN dbo.Students AS st ON st.student_code = x.student_code;

    IF (SELECT COUNT(*) FROM dbo.Stays) <> 6 OR (SELECT COUNT(*) FROM dbo.Invoices) <> 6
        THROW 50002, 'Demo seed is incomplete.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
