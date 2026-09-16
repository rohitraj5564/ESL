-- ============================================================
-- ESL User Login History & Session Management Script
-- Database: ESLV1
-- ============================================================

USE [ESLV1];
GO

-- 1. Create UserLoginHistory table if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserLoginHistory' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[UserLoginHistory]
    (
        [Id]                   BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserID]               NVARCHAR(100) NULL,
        [Username]             NVARCHAR(100) NOT NULL,
        [Name]                 NVARCHAR(150) NULL,
        [LoginDateTime]        DATETIME NOT NULL CONSTRAINT DF_UserLoginHistory_LoginDateTime DEFAULT (GETDATE()),
        [LogoutDateTime]       DATETIME NULL,
        [TotalDuration]        NVARCHAR(50) NULL,
        [TotalDurationSeconds] INT NULL,
        [ServerDateTime]       DATETIME NOT NULL CONSTRAINT DF_UserLoginHistory_ServerDateTime DEFAULT (GETDATE()),
        [LogoutReason]         NVARCHAR(100) NULL,
        [IsLoggedIn]           BIT NOT NULL CONSTRAINT DF_UserLoginHistory_IsLoggedIn DEFAULT (1),
        [SessionToken]         NVARCHAR(100) NULL
    );

    CREATE INDEX [IX_UserLoginHistory_Username_IsLoggedIn] 
        ON [dbo].[UserLoginHistory] ([Username], [IsLoggedIn]);

    CREATE INDEX [IX_UserLoginHistory_UserID_IsLoggedIn] 
        ON [dbo].[UserLoginHistory] ([UserID], [IsLoggedIn]);
END
GO

-- 2. Create or alter procedure to record user logout
CREATE OR ALTER PROCEDURE [dbo].[ESL_SP_RecordUserLogout]
    @UserID NVARCHAR(100) = NULL,
    @UserName NVARCHAR(100) = NULL,
    @LogoutReason NVARCHAR(100) = 'Manual Logout'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @LogoutTime DATETIME = GETDATE();

    -- Resolve Username and UserID if one of them is missing
    IF (@UserName IS NULL OR @UserName = '') AND (@UserID IS NOT NULL AND @UserID <> '')
    BEGIN
        SELECT TOP 1 @UserName = [UserName] FROM [dbo].[Users] WHERE [UserID] = @UserID;
    END

    IF (@UserID IS NULL OR @UserID = '') AND (@UserName IS NOT NULL AND @UserName <> '')
    BEGIN
        SELECT TOP 1 @UserID = CAST([UserID] AS NVARCHAR(100)) FROM [dbo].[Users] WHERE [UserName] = @UserName;
    END

    -- Update open sessions in UserLoginHistory
    UPDATE [dbo].[UserLoginHistory]
    SET 
        [LogoutDateTime] = @LogoutTime,
        [TotalDurationSeconds] = DATEDIFF(SECOND, [LoginDateTime], @LogoutTime),
        [TotalDuration] = RIGHT('00' + CAST(DATEDIFF(SECOND, [LoginDateTime], @LogoutTime) / 3600 AS VARCHAR(10)), 2) + 'h ' +
                          RIGHT('00' + CAST((DATEDIFF(SECOND, [LoginDateTime], @LogoutTime) % 3600) / 60 AS VARCHAR(10)), 2) + 'm ' +
                          RIGHT('00' + CAST(DATEDIFF(SECOND, [LoginDateTime], @LogoutTime) % 60 AS VARCHAR(10)), 2) + 's',
        [LogoutReason] = @LogoutReason,
        [IsLoggedIn] = 0
    WHERE 
        [IsLoggedIn] = 1
        AND (
            (@UserName IS NOT NULL AND [Username] = @UserName)
            OR (@UserID IS NOT NULL AND [UserID] = @UserID)
        );

    -- Also reset IsLogin flag in Users table
    IF (@UserName IS NOT NULL AND @UserName <> '')
    BEGIN
        UPDATE [dbo].[Users]
        SET [IsLogin] = 0, [SessionToken] = NULL
        WHERE [UserName] = @UserName;
    END
    ELSE IF (@UserID IS NOT NULL AND @UserID <> '')
    BEGIN
        UPDATE [dbo].[Users]
        SET [IsLogin] = 0, [SessionToken] = NULL
        WHERE [UserID] = @UserID;
    END
END
GO

-- 3. Update ESL_WEB_SP_Logout
CREATE OR ALTER PROCEDURE [dbo].[ESL_WEB_SP_Logout]
    @UserId NVARCHAR(100),
    @Reason NVARCHAR(100) = 'Manual Logout',
    @UserName NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC [dbo].[ESL_SP_RecordUserLogout] 
        @UserID = @UserId, 
        @UserName = @UserName, 
        @LogoutReason = @Reason;
END
GO

-- 4. Update ESL_PDA_SP_Logout
CREATE OR ALTER PROCEDURE [dbo].[ESL_PDA_SP_Logout]
    @UserId NVARCHAR(100),
    @Reason NVARCHAR(100) = 'Manual Logout',
    @UserName NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC [dbo].[ESL_SP_RecordUserLogout] 
        @UserID = @UserId, 
        @UserName = @UserName, 
        @LogoutReason = @Reason;
END
GO
