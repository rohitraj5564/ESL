CREATE OR ALTER PROCEDURE [dbo].[ESL_WEB_SP_CreateMovement_V1]
(
    @ID UNIQUEIDENTIFIER,
    @ladleMovementID NVARCHAR(100),
    @castID NVARCHAR(50),
    @ladleNo NVARCHAR(50),
    @movementTypeID INT,
    @sourceLocationName NVARCHAR(50),
    @destinationName NVARCHAR(50),
    @transactionDateTime DATETIME,
    @castNo NVARCHAR(20),
    @Status BIT = 0 OUT
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @sourceID INT
    DECLARE @destinationID INT

    DECLARE @locoID INT, @locoTagId NVARCHAR(100), @locoSerialNo NVARCHAR(50);
    DECLARE @ladleTagId NVARCHAR(100), @ladleNumber NVARCHAR(50);
    DECLARE @tpID SMALLINT, @tpType NVARCHAR(50);

    DECLARE @locoEventID UNIQUEIDENTIFIER = NULL, @locoEventTime DATETIME2 = NULL;
    DECLARE @ladleEventID UNIQUEIDENTIFIER = NULL, @ladleEventTime DATETIME2 = NULL;
    DECLARE @mappingTime DATETIME2 = ISNULL(@transactionDateTime, SYSDATETIME());

    BEGIN TRY
        BEGIN TRAN

        SELECT @sourceID = LocationId 
        FROM Locations 
        WHERE LocationName = @sourceLocationName

        IF @sourceID IS NULL
        BEGIN
            SET @Status = 0
            ROLLBACK
            RETURN
        END

        SELECT @destinationID = LocationId 
        FROM Locations 
        WHERE LocationName = @destinationName

        IF @destinationID IS NULL
        BEGIN
            SET @Status = 0
            ROLLBACK
            RETURN
        END

        IF NOT EXISTS (
            SELECT 1 
            FROM LadleMovementDetails 
            WHERE LadleNo = @ladleNo 
              AND LadleMovementID = @ladleMovementID
              AND IsActive = 1
        )
        BEGIN

            INSERT INTO LadleMovementDetails
            (
                ID,
                LadleMovementID,
                CastID,
                LadleNo,
                MovementTypeID,
                SourceLocationID,
                DestinationLocationID,
                IsActive,
                CreatedDateTime,
                ServerDateTime
            )
            VALUES
            (
                @ID,
                @ladleMovementID,
                @castID,
                @ladleNo,
                @movementTypeID,
                @sourceID,
                @destinationID,
                1,
                @transactionDateTime,
                GETDATE()
            )

            UPDATE CastTransaction 
            SET IsAssigned = 1 
            WHERE CastNo = @castNo 
              AND LadleNo = @ladleNo
              AND BookingLocationId IS NULL 

            SELECT @locoID = LM.LocoID
            FROM   LadleMovement LM
            WHERE  LM.ID = TRY_CAST(@ladleMovementID AS UNIQUEIDENTIFIER);

            SELECT @ladleTagId  = AM.ATagID,
                   @ladleNumber = AM.AName
            FROM   AssetMaster AM
            WHERE  AM.ADescription = @ladleNo
              AND  AM.ATypeID = 1
              AND  AM.IsActive = 1;

            SELECT @locoTagId    = LA.ATagID,
                   @locoSerialNo = LA.ASerialNo
            FROM   LocoMaster LMst
            INNER JOIN AssetMaster LA
                    ON LA.ADescription = LMst.LocoName
                   AND LA.ATypeID = 3
            WHERE  LMst.LocoID = @locoID;

            SELECT TOP 1 @tpID   = TP.TouchPointId,
                         @tpType = TP.TouchPointName
            FROM   TouchPoints TP
            WHERE  TP.LocationId = @sourceID
              AND  TP.IsActive = 1
            ORDER BY TP.TouchPointId;

            IF @ladleTagId IS NOT NULL AND @locoTagId IS NOT NULL
            BEGIN
                -- 1. Check if an earlier ladle in this same assignment batch already established a LocoEventID
                SELECT TOP 1 
                    @locoEventID = M.LocoEventID, 
                    @locoEventTime = M.LocoEventTime
                FROM LocoLadleMappingLog M
                WHERE M.LocoTagId = @locoTagId
                  AND M.MappingStatus = 'CONFIRMED'
                  AND M.IsManual = 1
                  AND M.CreatedOn >= DATEADD(MINUTE, -10, SYSDATETIME())
                  AND M.TouchPointID = ISNULL(@tpID, 0)
                ORDER BY M.CreatedOn DESC;

                -- 2. If not found in current batch, look up the latest event from ReadersTransactionLog for the Loco
                IF @locoEventID IS NULL
                BEGIN
                    SELECT TOP 1 
                        @locoEventID = ID, 
                        @locoEventTime = TransDatetime
                    FROM ReadersTransactionLog
                    WHERE TagId = @locoTagId
                    ORDER BY TransDatetime DESC;
                END

                -- 3. Fallback to generated GUID and assignment time
                IF @locoEventID IS NULL
                BEGIN
                    SET @locoEventID = NEWID();
                    SET @locoEventTime = @mappingTime;
                END

                -- 4. Look up the latest reader transaction for the Ladle RFID tag
                SELECT TOP 1 
                    @ladleEventID = ID, 
                    @ladleEventTime = TransDatetime
                FROM ReadersTransactionLog
                WHERE TagId = @ladleTagId
                ORDER BY TransDatetime DESC;

                -- 5. Fallback for Ladle RFID event
                IF @ladleEventID IS NULL
                BEGIN
                    SET @ladleEventID = NEWID();
                    SET @ladleEventTime = @mappingTime;
                END

                INSERT INTO LocoLadleMappingLog
                (
                    MappingID, TouchPointID, TouchPointType,
                    LocoTagId, LocoAssetSerialNo,
                    LadleRfidTag, LadleNumber,
                    LocoEventID, LadleRfidEventID, CameraEventID,
                    LocoEventTime, LadleRfidEventTime, CameraEventTime,
                    MappingStartTime, MappingEndTime,
                    ConfidenceScore, MappingStatus, IsManual,
                    MappingReason, CreatedOn
                )
                VALUES
                (
                    NEWID(),
                    ISNULL(@tpID, 0),                        
                    ISNULL(@tpType, @sourceLocationName),
                    @locoTagId, @locoSerialNo,
                    @ladleTagId, @ladleNumber,
                    @locoEventID, @ladleEventID, NULL,                      
                    @locoEventTime, @ladleEventTime, NULL,
                    @mappingTime, @mappingTime,
                    100, 'CONFIRMED', 1,                    
                    'Manual assignment from WB Dashboard: '
                        + @sourceLocationName + ' -> ' + @destinationName,
                    SYSDATETIME()
                );
            END

            SET @Status = 1
        END
        ELSE
        BEGIN
            SET @Status = 0
        END

        COMMIT
    END TRY

    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK

        SET @Status = 0
        DECLARE @ErrMsg  NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrLine INT            = ERROR_LINE();
        RAISERROR('ESL_WEB_SP_CreateMovement_V1 failed at line %d: %s', 16, 1, @ErrLine, @ErrMsg);
    END CATCH
END
GO
