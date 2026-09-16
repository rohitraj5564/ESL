CREATE OR ALTER PROCEDURE [dbo].[GetLatestLocoLadleMapping]
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH LocoEvents AS
    (
        SELECT 
            ISNULL(M.LocoAssetSerialNo, M.LocoTagId) AS LocoKey,
            ISNULL(M.LocoEventID, CAST(M.MappingID AS varchar(50))) AS EventID,
            MAX(M.CreatedOn) AS MaxCreatedOn
        FROM LocoLadleMappingLog M
        GROUP BY 
            ISNULL(M.LocoAssetSerialNo, M.LocoTagId),
            ISNULL(M.LocoEventID, CAST(M.MappingID AS varchar(50)))
    ),
    LocoLatestEvent AS
    (
        SELECT 
            LocoKey,
            EventID,
            ROW_NUMBER() OVER (PARTITION BY LocoKey ORDER BY MaxCreatedOn DESC) AS EventRN
        FROM LocoEvents
    ),
    LatestRecords AS
    (
        SELECT 
            M.*,
            ROW_NUMBER() OVER
            (
                PARTITION BY 
                    ISNULL(M.LocoAssetSerialNo, M.LocoTagId),
                    ISNULL(M.LadleRfidTag, CAST(M.MappingID AS varchar(50)))
                ORDER BY M.CreatedOn DESC
            ) AS LadleRN
        FROM LocoLadleMappingLog M
        INNER JOIN LocoLatestEvent E
            ON ISNULL(M.LocoAssetSerialNo, M.LocoTagId) = E.LocoKey
           AND ISNULL(M.LocoEventID, CAST(M.MappingID AS varchar(50))) = E.EventID
           AND E.EventRN = 1
    )

    SELECT
        M.LocoEventID,
        M.LocoTagId,
        Loco.ADescription AS LocoName,
        Loco.ASerialNo AS LocoSerialNo,
        M.LadleRfidEventID,
        M.LadleRfidTag,
        Ladle.ADescription AS LadleName,
        Ladle.ASerialNo AS LadleSerialNo,
        M.LadleNumber,
        M.TouchPointID,
        M.TouchPointType,
        M.LocoEventTime,
        M.LadleRfidEventTime,
        M.CameraEventTime,
        M.MappingStartTime,
        M.MappingEndTime,
        M.ConfidenceScore,
        M.MappingStatus,
        M.MappingReason,
        M.CreatedOn

    FROM LatestRecords M

    LEFT JOIN ESLV1.dbo.AssetMaster Loco
        ON (Loco.ASerialNo = M.LocoAssetSerialNo OR (M.LocoAssetSerialNo IS NULL AND Loco.ATagID = M.LocoTagId))
        AND Loco.ATypeID = 3

    LEFT JOIN ESLV1.dbo.AssetMaster Ladle
        ON Ladle.ATagID = M.LadleRfidTag
        AND Ladle.ATypeID = 1

    WHERE M.LadleRN = 1
      AND M.MappingStatus IN ('CONFIRMED', 'UNMAPPED')

    ORDER BY
        Loco.ADescription,
        M.LadleNumber;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ESL_WEB_SP_GetLatestLocoLadleMapping]
AS
BEGIN
    SET NOCOUNT ON;
    EXEC [dbo].[GetLatestLocoLadleMapping];
END
GO
