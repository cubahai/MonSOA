USE [KTX_Demo];
GO

-- Required for filtered indexes, including when run through sqlcmd.
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

CREATE TABLE dbo.Buildings (
    building_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Buildings PRIMARY KEY,
    building_code VARCHAR(10) NOT NULL CONSTRAINT UQ_Buildings_Code UNIQUE,
    building_name NVARCHAR(100) NOT NULL,
    address NVARCHAR(200) NOT NULL,
    gender_policy VARCHAR(10) NOT NULL,
    floor_count INT NOT NULL,
    CONSTRAINT CK_Buildings_GenderPolicy CHECK (gender_policy IN ('MALE', 'FEMALE', 'MIXED')),
    CONSTRAINT CK_Buildings_FloorCount CHECK (floor_count > 0)
);

CREATE TABLE dbo.Rooms (
    room_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Rooms PRIMARY KEY,
    building_id INT NOT NULL,
    room_number VARCHAR(10) NOT NULL,
    floor_number INT NOT NULL,
    monthly_rate DECIMAL(12,2) NOT NULL,
    room_status VARCHAR(12) NOT NULL CONSTRAINT DF_Rooms_Status DEFAULT ('AVAILABLE'),
    CONSTRAINT FK_Rooms_Buildings FOREIGN KEY (building_id) REFERENCES dbo.Buildings(building_id),
    CONSTRAINT UQ_Rooms_BuildingNumber UNIQUE (building_id, room_number),
    CONSTRAINT CK_Rooms_Floor CHECK (floor_number > 0),
    CONSTRAINT CK_Rooms_Rate CHECK (monthly_rate > 0),
    CONSTRAINT CK_Rooms_Status CHECK (room_status IN ('AVAILABLE', 'MAINTENANCE', 'CLOSED'))
);

CREATE TABLE dbo.Beds (
    bed_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Beds PRIMARY KEY,
    room_id INT NOT NULL,
    bed_number VARCHAR(10) NOT NULL,
    bed_status VARCHAR(12) NOT NULL CONSTRAINT DF_Beds_Status DEFAULT ('AVAILABLE'),
    CONSTRAINT FK_Beds_Rooms FOREIGN KEY (room_id) REFERENCES dbo.Rooms(room_id),
    CONSTRAINT UQ_Beds_RoomNumber UNIQUE (room_id, bed_number),
    CONSTRAINT CK_Beds_Status CHECK (bed_status IN ('AVAILABLE', 'MAINTENANCE'))
);

CREATE TABLE dbo.Students (
    student_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Students PRIMARY KEY,
    student_code VARCHAR(20) NOT NULL CONSTRAINT UQ_Students_Code UNIQUE,
    full_name NVARCHAR(100) NOT NULL,
    date_of_birth DATE NOT NULL,
    gender VARCHAR(6) NOT NULL,
    phone VARCHAR(20) NULL,
    email VARCHAR(254) NULL,
    faculty NVARCHAR(100) NOT NULL,
    student_status VARCHAR(8) NOT NULL CONSTRAINT DF_Students_Status DEFAULT ('ACTIVE'),
    CONSTRAINT CK_Students_Gender CHECK (gender IN ('MALE', 'FEMALE')),
    CONSTRAINT CK_Students_Status CHECK (student_status IN ('ACTIVE', 'INACTIVE'))
);

CREATE UNIQUE INDEX UX_Students_Email ON dbo.Students(email) WHERE email IS NOT NULL;

CREATE TABLE dbo.Stays (
    stay_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Stays PRIMARY KEY,
    student_id INT NOT NULL,
    bed_id INT NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NULL,
    stay_status VARCHAR(9) NOT NULL CONSTRAINT DF_Stays_Status DEFAULT ('ACTIVE'),
    CONSTRAINT FK_Stays_Students FOREIGN KEY (student_id) REFERENCES dbo.Students(student_id),
    CONSTRAINT FK_Stays_Beds FOREIGN KEY (bed_id) REFERENCES dbo.Beds(bed_id),
    CONSTRAINT CK_Stays_Dates CHECK (end_date IS NULL OR end_date >= start_date),
    CONSTRAINT CK_Stays_Status CHECK (stay_status IN ('ACTIVE', 'ENDED', 'CANCELLED')),
    CONSTRAINT CK_Stays_EndedDate CHECK (stay_status <> 'ENDED' OR end_date IS NOT NULL)
);

-- A student may hold one active bed, and a bed may have one active student.
CREATE UNIQUE INDEX UX_Stays_ActiveStudent ON dbo.Stays(student_id) WHERE stay_status = 'ACTIVE';
CREATE UNIQUE INDEX UX_Stays_ActiveBed ON dbo.Stays(bed_id) WHERE stay_status = 'ACTIVE';

CREATE TABLE dbo.Invoices (
    invoice_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY,
    stay_id INT NOT NULL,
    billing_month DATE NOT NULL,
    amount DECIMAL(12,2) NOT NULL,
    due_date DATE NOT NULL,
    created_at DATETIME2(0) NOT NULL CONSTRAINT DF_Invoices_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT FK_Invoices_Stays FOREIGN KEY (stay_id) REFERENCES dbo.Stays(stay_id),
    CONSTRAINT UQ_Invoices_StayMonth UNIQUE (stay_id, billing_month),
    CONSTRAINT CK_Invoices_FirstDay CHECK (DAY(billing_month) = 1),
    CONSTRAINT CK_Invoices_Amount CHECK (amount > 0),
    CONSTRAINT CK_Invoices_DueDate CHECK (due_date >= billing_month)
);

CREATE TABLE dbo.Payments (
    payment_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    invoice_id INT NOT NULL,
    amount DECIMAL(12,2) NOT NULL,
    paid_at DATETIME2(0) NOT NULL CONSTRAINT DF_Payments_PaidAt DEFAULT (SYSDATETIME()),
    payment_method VARCHAR(15) NOT NULL,
    reference_code VARCHAR(50) NULL,
    CONSTRAINT FK_Payments_Invoices FOREIGN KEY (invoice_id) REFERENCES dbo.Invoices(invoice_id),
    CONSTRAINT CK_Payments_Amount CHECK (amount > 0),
    CONSTRAINT CK_Payments_Method CHECK (payment_method IN ('CASH', 'BANK_TRANSFER'))
);

CREATE INDEX IX_Payments_Invoice ON dbo.Payments(invoice_id);
CREATE UNIQUE INDEX UX_Payments_Reference ON dbo.Payments(reference_code) WHERE reference_code IS NOT NULL;

CREATE TABLE dbo.MaintenanceRequests (
    request_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenanceRequests PRIMARY KEY,
    room_id INT NOT NULL,
    reported_by_student_id INT NULL,
    title NVARCHAR(150) NOT NULL,
    description NVARCHAR(1000) NULL,
    priority VARCHAR(6) NOT NULL CONSTRAINT DF_MaintenanceRequests_Priority DEFAULT ('MEDIUM'),
    request_status VARCHAR(11) NOT NULL CONSTRAINT DF_MaintenanceRequests_Status DEFAULT ('OPEN'),
    created_at DATETIME2(0) NOT NULL CONSTRAINT DF_MaintenanceRequests_CreatedAt DEFAULT (SYSDATETIME()),
    resolved_at DATETIME2(0) NULL,
    CONSTRAINT FK_MaintenanceRequests_Rooms FOREIGN KEY (room_id) REFERENCES dbo.Rooms(room_id),
    CONSTRAINT FK_MaintenanceRequests_Students FOREIGN KEY (reported_by_student_id) REFERENCES dbo.Students(student_id),
    CONSTRAINT CK_MaintenanceRequests_Priority CHECK (priority IN ('LOW', 'MEDIUM', 'HIGH')),
    CONSTRAINT CK_MaintenanceRequests_Status CHECK (request_status IN ('OPEN', 'IN_PROGRESS', 'RESOLVED')),
    CONSTRAINT CK_MaintenanceRequests_ResolvedAt CHECK (
        (request_status = 'RESOLVED' AND resolved_at IS NOT NULL)
        OR (request_status <> 'RESOLVED' AND resolved_at IS NULL)
    )
);
GO

CREATE OR ALTER VIEW dbo.vw_RoomOccupancy
AS
SELECT
    b.building_code,
    r.room_id,
    r.room_number,
    r.room_status,
    COUNT(bed.bed_id) AS total_beds,
    SUM(CASE WHEN s.stay_id IS NOT NULL THEN 1 ELSE 0 END) AS occupied_beds,
    SUM(CASE WHEN s.stay_id IS NULL AND bed.bed_status = 'AVAILABLE' AND r.room_status = 'AVAILABLE'
             THEN 1 ELSE 0 END) AS available_beds
FROM dbo.Rooms AS r
JOIN dbo.Buildings AS b ON b.building_id = r.building_id
LEFT JOIN dbo.Beds AS bed ON bed.room_id = r.room_id
LEFT JOIN dbo.Stays AS s ON s.bed_id = bed.bed_id AND s.stay_status = 'ACTIVE'
GROUP BY b.building_code, r.room_id, r.room_number, r.room_status;
GO

CREATE OR ALTER VIEW dbo.vw_InvoiceBalances
AS
SELECT
    i.invoice_id,
    st.student_code,
    st.full_name,
    i.billing_month,
    i.due_date,
    i.amount,
    COALESCE(SUM(p.amount), 0) AS paid_amount,
    i.amount - COALESCE(SUM(p.amount), 0) AS balance_due,
    CASE
        WHEN COALESCE(SUM(p.amount), 0) >= i.amount THEN 'PAID'
        WHEN COALESCE(SUM(p.amount), 0) > 0 THEN 'PARTIAL'
        ELSE 'UNPAID'
    END AS payment_status
FROM dbo.Invoices AS i
JOIN dbo.Stays AS s ON s.stay_id = i.stay_id
JOIN dbo.Students AS st ON st.student_id = s.student_id
LEFT JOIN dbo.Payments AS p ON p.invoice_id = i.invoice_id
GROUP BY i.invoice_id, st.student_code, st.full_name, i.billing_month, i.due_date, i.amount;
GO
