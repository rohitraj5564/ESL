-- ============================================================================
-- ESL Complete User Login History, Session Tracking & Reports Setup Script
-- Target Database: ESLV1 / ESLLadleDB_Prod
-- ============================================================================

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

    CREATE INDEX [IX_UserLoginHistory_LoginDateTime] 
        ON [dbo].[UserLoginHistory] ([LoginDateTime]);
END
GO

-- 2. Procedure to record user logout (Closes active session and calculates duration)
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

    -- Reset IsLogin flag in Users table
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

-- 3. Web Logout Procedure
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

-- 4. PDA Logout Procedure
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

-- 5. Web Dashboard Login Procedure (Records login session in UserLoginHistory)
CREATE OR ALTER PROCEDURE [dbo].[ESL_WEB_SP_DashboardLogin]
    @UserName NVARCHAR(100),
    @Password NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(100), 
            @LocationId INT, 
            @LocationName NVARCHAR(50), 
            @userFirstName NVARCHAR(50), 
            @userLastName NVARCHAR(50), 
            @userFullName NVARCHAR(150), 
            @SessionToken NVARCHAR(100);

    IF EXISTS (SELECT 1 FROM [Users] WHERE [UserName] = @UserName)
    BEGIN
        IF EXISTS (SELECT 1 FROM [Users] WHERE [UserName] = @UserName AND [IsActive] = 1)
        BEGIN
            -- Terminate previous active session if re-logging in
            IF EXISTS (SELECT 1 FROM [Users] WHERE [UserName] = @UserName AND [IsLogin] = 1) 
               OR EXISTS (SELECT 1 FROM [UserLoginHistory] WHERE [Username] = @UserName AND [IsLoggedIn] = 1)
            BEGIN
                EXEC [dbo].[ESL_SP_RecordUserLogout] 
                    @UserName = @UserName, 
                    @LogoutReason = 'Previous Session Terminated on Re-Login';
            END;

            -- Retrieve user info
            SELECT @UserId = CAST([UserID] AS NVARCHAR(100)), 
                   @LocationId = [LocationId], 
                   @userFirstName = [FirstName],
                   @userLastName = [LastName],
                   @userFullName = LTRIM(RTRIM(ISNULL([FirstName], '') + ' ' + ISNULL([LastName], '')))
            FROM [Users] 
            WHERE [UserName] = @UserName AND [IsActive] = 1;

            IF @userFullName = '' OR @userFullName IS NULL
                SET @userFullName = @UserName;

            SET @SessionToken = CAST(NEWID() AS NVARCHAR(100));

            -- Update current login status in Users table
            UPDATE [Users] 
            SET [IsLogin] = 1, 
                [SessionToken] = @SessionToken,
                [LastActivityTime] = GETDATE()
            WHERE [UserName] = @UserName;

            -- Insert session into UserLoginHistory
            INSERT INTO [dbo].[UserLoginHistory] (
                [UserID], 
                [Username], 
                [Name], 
                [LoginDateTime], 
                [LogoutDateTime], 
                [TotalDuration], 
                [TotalDurationSeconds], 
                [ServerDateTime], 
                [LogoutReason], 
                [IsLoggedIn], 
                [SessionToken]
            )
            VALUES (
                @UserId, 
                @UserName, 
                @userFullName, 
                GETDATE(), 
                NULL, 
                NULL, 
                NULL, 
                GETDATE(), 
                NULL, 
                1, 
                @SessionToken
            );

            SELECT @LocationName = LocationName 
            FROM Locations 
            WHERE LocationId = @LocationId;

            -- Return login response
            SELECT @UserId AS userID, 
                   @UserName AS userName,
                   @LocationId AS userLocationId, 
                   @LocationName AS userLocationName, 
                   @userFirstName AS userFirstName,
                   @SessionToken AS sessionToken;
        END
        ELSE
        BEGIN
            RAISERROR ('This User is Inactive', 16, 1);
        END
    END
    ELSE
    BEGIN
        RAISERROR ('Invalid credentials.', 16, 1);
    END
END
GO

-- 6. PDA Login Procedure (Records login session in UserLoginHistory)
CREATE OR ALTER PROCEDURE [dbo].[ESL_PDA_SP_LoginData]
    @UserName NVARCHAR(100),
    @Password NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId NVARCHAR(100), 
            @LocationId INT, 
            @LocationName NVARCHAR(50), 
            @userFirstName NVARCHAR(50), 
            @userLastName NVARCHAR(50), 
            @userFullName NVARCHAR(150), 
            @SessionToken NVARCHAR(100);

    IF EXISTS (SELECT 1 FROM [Users] WHERE [UserName] = @UserName)
    BEGIN
        IF EXISTS (SELECT 1 FROM [Users] WHERE [UserName] = @UserName AND [IsActive] = 1)
        BEGIN
            IF EXISTS (SELECT 1 FROM [Users] WHERE [UserName] = @UserName AND [IsLogin] = 1) 
               OR EXISTS (SELECT 1 FROM [UserLoginHistory] WHERE [Username] = @UserName AND [IsLoggedIn] = 1)
            BEGIN
                EXEC [dbo].[ESL_SP_RecordUserLogout] 
                    @UserName = @UserName, 
                    @LogoutReason = 'Previous Session Terminated on Re-Login';
            END;

            SELECT @UserId = CAST([UserID] AS NVARCHAR(100)), 
                   @LocationId = [LocationId], 
                   @userFirstName = [FirstName],
                   @userLastName = [LastName],
                   @userFullName = LTRIM(RTRIM(ISNULL([FirstName], '') + ' ' + ISNULL([LastName], '')))
            FROM [Users] 
            WHERE [UserName] = @UserName AND [IsActive] = 1;

            IF @userFullName = '' OR @userFullName IS NULL
                SET @userFullName = @UserName;

            SET @SessionToken = CAST(NEWID() AS NVARCHAR(100));

            UPDATE [Users] 
            SET [IsLogin] = 1, 
                [SessionToken] = @SessionToken,
                [LastActivityTime] = GETDATE()
            WHERE [UserName] = @UserName;

            INSERT INTO [dbo].[UserLoginHistory] (
                [UserID], 
                [Username], 
                [Name], 
                [LoginDateTime], 
                [LogoutDateTime], 
                [TotalDuration], 
                [TotalDurationSeconds], 
                [ServerDateTime], 
                [LogoutReason], 
                [IsLoggedIn], 
                [SessionToken]
            )
            VALUES (
                @UserId, 
                @UserName, 
                @userFullName, 
                GETDATE(), 
                NULL, 
                NULL, 
                NULL, 
                GETDATE(), 
                NULL, 
                1, 
                @SessionToken
            );

            SELECT @LocationName = LocationName 
            FROM Locations 
            WHERE LocationId = @LocationId;

            SELECT @UserId AS userID, 
                   @UserName AS userName,
                   @LocationId AS userLocationId, 
                   @LocationName AS userLocationName, 
                   @userFirstName AS userFirstName,
                   @SessionToken AS sessionToken;

            SELECT DISTINCT MLM.ModuleID AS moduleID, M.ModuleName AS moduleName
            FROM ModuleLocationMapping AS MLM 
            LEFT JOIN [Users] AS U ON MLM.LocationID = U.LocationId 
            LEFT JOIN Module M ON MLM.ModuleID = M.ModuleID
            WHERE M.IsActive = 1 AND @LocationId = MLM.LocationID;

            SELECT LocationId AS locationID, LocationName AS locationName 
            FROM Locations;
        END
        ELSE
        BEGIN
            RAISERROR ('This User is Inactive', 16, 1);
        END
    END
    ELSE
    BEGIN
        RAISERROR ('Invalid credentials.', 16, 1);
    END
END
GO

-- 7. User Login History Report Procedure (Used by User Login History UI Report)
CREATE OR ALTER PROCEDURE [dbo].[ESL_SP_GetUserLoginHistoryReport]
    @FromDate DATETIME,
    @ToDate   DATETIME,
    @UserName NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF CONVERT(TIME, @ToDate) = '00:00:00'
    BEGIN
        SET @ToDate = DATEADD(MILLISECOND, -3, DATEADD(DAY, 1, CAST(CAST(@ToDate AS DATE) AS DATETIME)));
    END

    -- Dataset 1: Aggregated User Summary
    SELECT 
        MAX(h.Username)                             AS Username,
        ISNULL(MAX(h.Name), MAX(h.Username))         AS Name,
        COUNT(h.Id)                                 AS TotalLogins,
        SUM(
            CASE 
                WHEN h.TotalDurationSeconds IS NOT NULL THEN h.TotalDurationSeconds
                WHEN h.IsLoggedIn = 1                   THEN DATEDIFF(SECOND, h.LoginDateTime, GETDATE())
                ELSE 0 
            END
        )                                           AS TotalDurationSeconds,
        RIGHT('00' + CAST(
            SUM(
                CASE 
                    WHEN h.TotalDurationSeconds IS NOT NULL THEN h.TotalDurationSeconds
                    WHEN h.IsLoggedIn = 1                   THEN DATEDIFF(SECOND, h.LoginDateTime, GETDATE())
                    ELSE 0 
                END
            ) / 3600 AS VARCHAR(10)), 2) + 'h ' +
        RIGHT('00' + CAST(
            (SUM(
                CASE 
                    WHEN h.TotalDurationSeconds IS NOT NULL THEN h.TotalDurationSeconds
                    WHEN h.IsLoggedIn = 1                   THEN DATEDIFF(SECOND, h.LoginDateTime, GETDATE())
                    ELSE 0 
                END
            ) % 3600) / 60 AS VARCHAR(10)), 2) + 'm ' +
        RIGHT('00' + CAST(
            (SUM(
                CASE 
                    WHEN h.TotalDurationSeconds IS NOT NULL THEN h.TotalDurationSeconds
                    WHEN h.IsLoggedIn = 1                   THEN DATEDIFF(SECOND, h.LoginDateTime, GETDATE())
                    ELSE 0 
                END
            ) % 60) AS VARCHAR(10)), 2) + 's'        AS TotalDurationFormatted,
        MAX(h.LoginDateTime)                        AS LastLoginDateTime,
        CAST(MAX(CAST(h.IsLoggedIn AS INT)) AS BIT) AS IsCurrentlyOnline
    FROM [dbo].[UserLoginHistory] h WITH (NOLOCK)
    WHERE 
        h.LoginDateTime >= @FromDate 
        AND h.LoginDateTime <= @ToDate
        AND (
            @UserName IS NULL 
            OR @UserName = '' 
            OR @UserName = 'ALL' 
            OR UPPER(LTRIM(RTRIM(h.Username))) = UPPER(LTRIM(RTRIM(@UserName)))
        )
    GROUP BY UPPER(LTRIM(RTRIM(h.Username)))
    ORDER BY TotalDurationSeconds DESC;

    -- Dataset 2: Session Detail Audit Records
    SELECT 
        h.Id,
        h.UserID,
        h.Username,
        h.Name,
        h.LoginDateTime,
        h.LogoutDateTime,
        CASE 
            WHEN h.TotalDuration IS NOT NULL THEN h.TotalDuration
            WHEN h.IsLoggedIn = 1            THEN 
                RIGHT('00' + CAST(DATEDIFF(SECOND, h.LoginDateTime, GETDATE()) / 3600 AS VARCHAR(10)), 2) + 'h ' +
                RIGHT('00' + CAST((DATEDIFF(SECOND, h.LoginDateTime, GETDATE()) % 3600) / 60 AS VARCHAR(10)), 2) + 'm ' +
                RIGHT('00' + CAST((DATEDIFF(SECOND, h.LoginDateTime, GETDATE()) % 60) AS VARCHAR(10)), 2) + 's (Active)'
            ELSE '-' 
        END AS TotalDuration,
        CASE 
            WHEN h.TotalDurationSeconds IS NOT NULL THEN h.TotalDurationSeconds
            WHEN h.IsLoggedIn = 1                   THEN DATEDIFF(SECOND, h.LoginDateTime, GETDATE())
            ELSE 0 
        END AS TotalDurationSeconds,
        ISNULL(
            h.LogoutReason, 
            CASE WHEN h.IsLoggedIn = 1 THEN 'Active Session' ELSE 'Unknown' END
        ) AS LogoutReason,
        h.IsLoggedIn,
        h.ServerDateTime
    FROM [dbo].[UserLoginHistory] h WITH (NOLOCK)
    WHERE 
        h.LoginDateTime >= @FromDate 
        AND h.LoginDateTime <= @ToDate
        AND (
            @UserName IS NULL 
            OR @UserName = '' 
            OR @UserName = 'ALL' 
            OR UPPER(LTRIM(RTRIM(h.Username))) = UPPER(LTRIM(RTRIM(@UserName)))
        )
    ORDER BY h.LoginDateTime DESC;
END;
GO

-- 8. Get Distinct Users for Report Dropdown
CREATE OR ALTER PROCEDURE [dbo].[ESL_SP_GetLoginReportUsers]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        Username,
        ISNULL(MAX(Name), Username) AS Name
    FROM [dbo].[UserLoginHistory] WITH (NOLOCK)
    GROUP BY Username
    ORDER BY Username ASC;
END;
GO
