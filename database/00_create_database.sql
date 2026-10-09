-- Run with SQL Server Management Studio or sqlcmd before the other scripts.
IF DB_ID(N'KTX_Demo') IS NULL
BEGIN
    CREATE DATABASE [KTX_Demo];
END;
GO
