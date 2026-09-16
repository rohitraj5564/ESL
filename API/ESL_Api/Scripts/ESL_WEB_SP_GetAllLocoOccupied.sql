CREATE OR ALTER PROCEDURE [dbo].[ESL_WEB_SP_GetAllLocoOccupied]
(
    @OnlyAvailable BIT = 0,
    @userID NVARCHAR(100) = NULL,
    @StaleHours INT = 0
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @currentLocoID INT = NULL;

    IF @userID IS NOT NULL
    BEGIN
        SELECT @currentLocoID = L.LocoID
        FROM [dbo].[Users] U
        INNER JOIN [dbo].[LocoMaster] L
            ON U.FirstName = L.LocoName
        WHERE U.UserID = @userID;
    END

    ;WITH
    TripLadles AS
    (
        SELECT LM2.LocoID, LMD.LadleNo
        FROM   LadleMovement AS LM2
        INNER JOIN LadleMovementDetails AS LMD
                ON LM2.ID = LMD.LadleMovementID
        WHERE  LM2.IsActive = 1
          AND  LMD.IsActive = 1
          AND  ISNULL(LMD.IsTransferred, 0) = 0
    ),

    LocoEvents AS
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
    LatestMapping AS
    (
        SELECT
            M.LocoTagId,
            M.LocoAssetSerialNo,
            M.LadleRfidTag,
            M.MappingStatus,
            M.CreatedOn,
            ROW_NUMBER() OVER
            (
                PARTITION BY 
                    ISNULL(M.LocoAssetSerialNo, M.LocoTagId),
                    ISNULL(M.LadleRfidTag, CAST(M.MappingID AS varchar(50)))
                ORDER BY M.CreatedOn DESC
            ) AS RN
        FROM LocoLadleMappingLog M
        INNER JOIN LocoLatestEvent E
            ON ISNULL(M.LocoAssetSerialNo, M.LocoTagId) = E.LocoKey
           AND ISNULL(M.LocoEventID, CAST(M.MappingID AS varchar(50))) = E.EventID
           AND E.EventRN = 1
    ),

    AutoLadles AS
    (
        SELECT LMst.LocoID, LadleAsset.ADescription AS LadleNo
        FROM   LatestMapping LMap
        INNER JOIN AssetMaster LocoAsset
                ON (LocoAsset.ASerialNo = LMap.LocoAssetSerialNo OR (LMap.LocoAssetSerialNo IS NULL AND LocoAsset.ATagID = LMap.LocoTagId))
               AND LocoAsset.ATypeID = 3
        INNER JOIN LocoMaster LMst
                ON LMst.LocoName = LocoAsset.ADescription
        INNER JOIN AssetMaster LadleAsset
                ON LadleAsset.ATagID = LMap.LadleRfidTag
               AND LadleAsset.ATypeID = 1
        WHERE  LMap.RN = 1
          AND  LMap.MappingStatus = 'CONFIRMED'
          AND  (
                   @StaleHours = 0
                OR LMap.CreatedOn >= DATEADD(HOUR, -@StaleHours, SYSDATETIME())
               )
    ),

    AllLadles AS
    (
        SELECT LocoID, LadleNo FROM TripLadles
        UNION
        SELECT LocoID, LadleNo FROM AutoLadles
    ),

    LadleStats AS
    (
        SELECT LocoID, COUNT(*) AS LadleCount
        FROM   AllLadles
        GROUP BY LocoID
    )

    SELECT 
        LM.LocoID AS LocoID, 
        LM.LocoName AS LocoName, 
        CAST(
            CASE 
                WHEN ISNULL(LM.IsOccupied, 0) = 1
                  OR ISNULL(LadleStats.LadleCount, 0) > 0
                THEN 1 ELSE 0 
            END AS BIT
        ) AS IsOccupied,
        LM.IsBooked AS IsBooked,
        ISNULL(LadleStats.LadleCount, 0) AS LadleCount,
        ISNULL(CONVERT(NVARCHAR(30), LM.LastOccupiedDateTime, 120), 'NA') AS LastOccupiedDateTime
    FROM 
        LocoMaster AS LM
    LEFT JOIN LadleStats 
            ON LM.LocoID = LadleStats.LocoID
    WHERE 
        LM.IsActive = 1
        AND (@currentLocoID IS NULL OR LM.LocoID <> @currentLocoID)
        AND (
            @OnlyAvailable = 0
            OR (
                   ISNULL(LM.IsOccupied, 0) = 0
               AND ISNULL(LadleStats.LadleCount, 0) = 0   
               )
        );
END
GO
