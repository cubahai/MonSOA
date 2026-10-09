USE [KTX_Demo];
GO

SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.UserAccounts', 'language_code') IS NULL
    ALTER TABLE dbo.UserAccounts ADD language_code VARCHAR(2) NOT NULL
        CONSTRAINT DF_UserAccounts_Language DEFAULT ('vi');
IF COL_LENGTH('dbo.UserAccounts', 'theme_code') IS NULL
    ALTER TABLE dbo.UserAccounts ADD theme_code VARCHAR(6) NOT NULL
        CONSTRAINT DF_UserAccounts_Theme DEFAULT ('light');
IF COL_LENGTH('dbo.UserAccounts', 'failed_attempts') IS NULL
    ALTER TABLE dbo.UserAccounts ADD failed_attempts INT NOT NULL
        CONSTRAINT DF_UserAccounts_FailedAttempts DEFAULT (0);
IF COL_LENGTH('dbo.UserAccounts', 'locked_until') IS NULL
    ALTER TABLE dbo.UserAccounts ADD locked_until DATETIME2(0) NULL;
IF COL_LENGTH('dbo.UserAccounts', 'auth_version') IS NULL
    ALTER TABLE dbo.UserAccounts ADD auth_version INT NOT NULL
        CONSTRAINT DF_UserAccounts_AuthVersion DEFAULT (1);
IF COL_LENGTH('dbo.UserAccounts', 'totp_secret') IS NULL
    ALTER TABLE dbo.UserAccounts ADD totp_secret VARBINARY(MAX) NULL;
IF COL_LENGTH('dbo.UserAccounts', 'totp_enabled') IS NULL
    ALTER TABLE dbo.UserAccounts ADD totp_enabled BIT NOT NULL
        CONSTRAINT DF_UserAccounts_TotpEnabled DEFAULT (0);
IF COL_LENGTH('dbo.UserAccounts', 'totp_pending_secret') IS NULL
    ALTER TABLE dbo.UserAccounts ADD totp_pending_secret VARBINARY(MAX) NULL;
IF COL_LENGTH('dbo.UserAccounts', 'totp_pending_expires_at') IS NULL
    ALTER TABLE dbo.UserAccounts ADD totp_pending_expires_at DATETIME2(0) NULL;
IF COL_LENGTH('dbo.UserAccounts', 'totp_last_step') IS NULL
    ALTER TABLE dbo.UserAccounts ADD totp_last_step BIGINT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UserAccounts_Language')
    ALTER TABLE dbo.UserAccounts ADD CONSTRAINT CK_UserAccounts_Language
        CHECK (language_code IN ('vi', 'en', 'zh'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UserAccounts_Theme')
    ALTER TABLE dbo.UserAccounts ADD CONSTRAINT CK_UserAccounts_Theme
        CHECK (theme_code IN ('light', 'dark', 'system'));
GO

IF OBJECT_ID(N'dbo.UserRecoveryCodes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRecoveryCodes (
        recovery_code_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserRecoveryCodes PRIMARY KEY,
        user_id INT NOT NULL,
        code_hash VARBINARY(32) NOT NULL,
        created_at DATETIME2(0) NOT NULL CONSTRAINT DF_UserRecoveryCodes_CreatedAt DEFAULT (SYSUTCDATETIME()),
        used_at DATETIME2(0) NULL,
        CONSTRAINT FK_UserRecoveryCodes_UserAccounts FOREIGN KEY (user_id)
            REFERENCES dbo.UserAccounts(user_id) ON DELETE CASCADE,
        CONSTRAINT UQ_UserRecoveryCodes_Hash UNIQUE (code_hash)
    );
    CREATE INDEX IX_UserRecoveryCodes_Unused ON dbo.UserRecoveryCodes(user_id, used_at);
END;
GO
