CREATE OR ALTER PROCEDURE [dbo].[ESL_WEB_SP_GetManualVsAutoAssignmentReport]
(
    @FromDate DATETIME = NULL,
    @ToDate   DATETIME = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    -- Default to rolling 24 hours if FromDate is not specified
    IF @FromDate IS NULL
        SET @FromDate = DATEADD(HOUR, -24, SYSDATETIME());

    -- Default ToDate to current timestamp if not specified
    IF @ToDate IS NULL
        SET @ToDate = SYSDATETIME();
    ELSE IF CAST(@ToDate AS TIME) = '00:00:00'
        SET @ToDate = DATEADD(SECOND, 86399, CAST(CAST(@ToDate AS DATE) AS DATETIME));

    SELECT 
        M.MappingID,
        M.TouchPointID,
        M.TouchPointType,
        ISNULL(Loco.ADescription, 'Loco ' + ISNULL(M.LocoAssetSerialNo, '')) AS LocoName,
        M.LocoAssetSerialNo,
        M.LocoTagId,
        ISNULL(Ladle.ADescription, 'Ladle ' + ISNULL(M.LadleNumber, '')) AS LadleName,
        M.LadleNumber,
        M.LadleRfidTag,
        M.LocoEventID,
        M.LadleRfidEventID,
        M.CameraEventID,
        M.LocoEventTime,
        M.LadleRfidEventTime,
        M.CameraEventTime,
        M.MappingStartTime,
        M.MappingEndTime,
        M.ConfidenceScore,
        M.MappingStatus,
        M.MappingReason,
        M.CreatedOn,
        M.IsManual,
        CASE WHEN M.IsManual = 1 THEN 'Manual' ELSE 'Automatic' END AS AssignmentType
    FROM LocoLadleMappingLog M
    LEFT JOIN AssetMaster Loco
        ON (Loco.ASerialNo = M.LocoAssetSerialNo OR (M.LocoAssetSerialNo IS NULL AND Loco.ATagID = M.LocoTagId))
        AND Loco.ATypeID = 3
    LEFT JOIN AssetMaster Ladle
        ON Ladle.ATagID = M.LadleRfidTag
       AND Ladle.ATypeID = 1
    WHERE M.CreatedOn >= @FromDate 
      AND M.CreatedOn <= @ToDate
    ORDER BY M.CreatedOn DESC;
END
GO
