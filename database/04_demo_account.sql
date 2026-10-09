USE [KTX_Demo];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.UserAccounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserAccounts (
        user_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserAccounts PRIMARY KEY,
        username NVARCHAR(50) NOT NULL CONSTRAINT UQ_UserAccounts_Username UNIQUE,
        full_name NVARCHAR(100) NOT NULL,
        role_name VARCHAR(20) NOT NULL,
        password_salt VARBINARY(16) NOT NULL,
        password_hash VARBINARY(32) NOT NULL,
        password_iterations INT NOT NULL,
        is_active BIT NOT NULL CONSTRAINT DF_UserAccounts_IsActive DEFAULT (1),
        created_at DATETIME2(0) NOT NULL CONSTRAINT DF_UserAccounts_CreatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT CK_UserAccounts_Role CHECK (role_name IN ('ADMIN', 'STAFF')),
        CONSTRAINT CK_UserAccounts_Iterations CHECK (password_iterations >= 100000)
    );
END;
GO

-- Demo only: admin / Admin@123456, PBKDF2-HMAC-SHA256 with 210,000 iterations.
IF NOT EXISTS (SELECT 1 FROM dbo.UserAccounts WHERE username = N'admin')
BEGIN
    INSERT dbo.UserAccounts
        (username, full_name, role_name, password_salt, password_hash, password_iterations)
    VALUES
        (N'admin', N'Quản trị viên Demo', 'ADMIN',
         0x7D93CACE2BF032521F8E47088E8CC12A,
         0x42D4BCA4FD494C9DAD61FB3357D95086630F327F40C592FDF50B2FDD52AF7F63,
         210000);
END;
GO
