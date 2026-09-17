-- ============================================================================
-- ESL User Login History & Duration Report - Stored Procedures
-- Database: ESLV1 (or target ESL database)
-- ============================================================================

USE [ESLV1];
GO

-- ============================================================================
-- Stored Procedure 1: [dbo].[ESL_SP_GetUserLoginHistoryReport]
-- Description:
--   Returns 2 Result Sets for the User Login History Report:
--   1. Dataset 1: User-wise aggregated summary (Username, Name, Total Logins,
--                 Total Duration Seconds & Formatted, Last Login, Online Status)
--   2. Dataset 2: Detailed session audit records (Id, UserID, Username, Name,
--                 Login Time, Logout Time, Session Duration, Termination Reason, Status)
--
-- Parameters:
--   @FromDate : Start Date (e.g. '2026-09-10 00:00:00')
--   @ToDate   : End Date   (e.g. '2026-09-17 23:59:59')
--   @UserName : Optional Username filter (Pass NULL, '', or 'ALL' for all users)
-- ============================================================================

CREATE OR ALTER PROCEDURE [dbo].[ESL_SP_GetUserLoginHistoryReport]
    @FromDate DATETIME,
    @ToDate   DATETIME,
    @UserName NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Adjust @ToDate to end of day if time was not provided (00:00:00)
    IF CONVERT(TIME, @ToDate) = '00:00:00'
    BEGIN
        SET @ToDate = DATEADD(MILLISECOND, -3, DATEADD(DAY, 1, CAST(CAST(@ToDate AS DATE) AS DATETIME)));
    END

    -- ------------------------------------------------------------------------
    -- Dataset 1: User-wise Aggregated Duration Summary
    -- ------------------------------------------------------------------------
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

    -- ------------------------------------------------------------------------
    -- Dataset 2: Detailed Session Audit Log Records
    -- ------------------------------------------------------------------------
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


-- ============================================================================
-- Stored Procedure 2: [dbo].[ESL_SP_GetLoginReportUsers]
-- Description:
--   Populates the 'User Filter' dropdown on the report UI.
-- ============================================================================

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
