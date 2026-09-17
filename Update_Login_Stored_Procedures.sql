USE [ESLLadleDB_Prod];
GO

-- ============================================================
-- 1. Update ESL_WEB_SP_DashboardLogin to record login in UserLoginHistory
-- ============================================================
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
            -- Terminate any previous active session for this user
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

            -- Record login session in UserLoginHistory table
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

            -- Return login response expected by Web API
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

-- ============================================================
-- 2. Update ESL_PDA_SP_LoginData to record login in UserLoginHistory
-- ============================================================
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
