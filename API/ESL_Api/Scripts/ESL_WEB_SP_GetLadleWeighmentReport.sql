USE [ESLV1]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:        Antigravity
-- Create date:   2026-09-07
-- Description:   Fetch Ladle Weighment Report by Date Range from [LadleWeightmentTransaction]
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[ESL_WEB_SP_GetLadleWeighmentReport]
    @FromDate DATETIME = NULL,
    @ToDate DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Default to last 24 hours if not provided
    IF @FromDate IS NULL
        SET @FromDate = DATEADD(DAY, -1, CAST(GETDATE() AS DATE));
    
    IF @ToDate IS NULL
        SET @ToDate = DATEADD(DAY, 1, CAST(GETDATE() AS DATE));

    SELECT 
        ROW_NUMBER() OVER (ORDER BY [TripDateTime] DESC, [ID] DESC) AS [SLNo],
        [ID],
        [TripID],
        [TripDateTime],
        [TripNumber],
        [LadleNo],
        [TransactionNumber] AS [TransactionNo],
        [ConsumptionNumber] AS [ConsumptionNo],
        COALESCE(
            NULLIF(LTRIM(RTRIM([CastNumber])), ''),
            MAX(NULLIF(LTRIM(RTRIM([CastNumber])), '')) OVER (
                PARTITION BY NULLIF(LTRIM(RTRIM([ConsumptionNumber])), '')
            )
        ) AS [CastNo],
        [SenderLocation],
        [ReceiverLocation],
        [TareWeight],
        [TareWeightDateTime],
        [GrossWeight],
        [GrossWeightDateTime],
        [NetWeight],
        [Weight],
        [WeightDateTime],
        [WeighbridgeID],
        [TransactionType],
        [IsManual],
        [ProcessState],
        [ServerDateTime],
        [UpdatedDateTime],
        [ConsumptionType]
    FROM [dbo].[LadleWeightmentTransaction] WITH (NOLOCK)
    WHERE [TripDateTime] >= @FromDate AND [TripDateTime] <= @ToDate
    ORDER BY [TripDateTime] DESC, [ID] DESC;
END
GO
