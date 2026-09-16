CREATE OR ALTER PROCEDURE [dbo].[ESL_WEB_SP_TransferLoco]
(
    @tripID         NVARCHAR(50),
    @currentLoco    NVARCHAR(50),
    @newLoco        NVARCHAR(50),
    @status         BIT OUTPUT,
    @message        NVARCHAR(200) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @currentLocoID INT
    DECLARE @newLocoID INT
    DECLARE @ladleMovementID UNIQUEIDENTIFIER
    DECLARE @newTripID NVARCHAR(50)

    DECLARE @oldLocoTagId NVARCHAR(100)
    DECLARE @newLocoTagId NVARCHAR(100), @newLocoSerialNo NVARCHAR(50)
    DECLARE @newLocoEventID UNIQUEIDENTIFIER = NULL, @newLocoEventTime DATETIME2 = NULL

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @currentLocoID = LocoID FROM LocoMaster WHERE LocoName = @currentLoco;
        SELECT @newLocoID = LocoID FROM LocoMaster WHERE LocoName = @newLoco;

        IF @currentLocoID IS NULL OR @newLocoID IS NULL
        BEGIN
            SET @status = 0;
            SET @message = 'Invalid Loco name.';
            ROLLBACK TRANSACTION;
            RETURN;
        END

        IF EXISTS (
            SELECT 1 FROM LadleMovement
            WHERE LocoID = @newLocoID
            AND IsActive = 1
            AND IsAssigned = 1
        )
        BEGIN
            SET @status = 0;
            SET @message = 'New Loco is already occupied with another trip.';
            ROLLBACK TRANSACTION;
            RETURN;
        END

        SELECT @ladleMovementID = ID
        FROM LadleMovement
        WHERE TripID = @tripID
        AND LocoID = @currentLocoID
        AND IsActive = 1;

        IF @ladleMovementID IS NULL
        BEGIN
            SET @status = 0;
            SET @message = 'Trip not found for current Loco.';
            ROLLBACK TRANSACTION;
            RETURN;
        END

        SET @newTripID = FORMAT(GETDATE(), 'yyyyMMdd') +
                         RIGHT('0000' + CAST(@newLocoID AS NVARCHAR), 2) +
                         '0001';

        -- Update movement
        UPDATE LadleMovement
        SET
            LocoID      = @newLocoID,
            TripID      = @newTripID
        WHERE ID = @ladleMovementID;

        -- Update old loco status
        UPDATE LocoMaster
        SET
            IsOccupied          = 0,
            IsBooked            = 0,
            UpdatedDateTime     = GETDATE()
        WHERE LocoID = @currentLocoID;

        -- Update new loco status
        UPDATE LocoMaster
        SET
            IsOccupied              = 1,
            IsBooked                = 0,
            LastOccupiedDateTime    = GETDATE()
        WHERE LocoID = @newLocoID;

        -- Asset lookups for LocoLadleMappingLog
        SELECT @oldLocoTagId = AM.ATagID
        FROM   AssetMaster AM
        WHERE  AM.ADescription = @currentLoco
          AND  AM.ATypeID = 3
          AND  AM.IsActive = 1;

        SELECT @newLocoTagId    = AM.ATagID,
               @newLocoSerialNo = AM.ASerialNo
        FROM   AssetMaster AM
        WHERE  AM.ADescription = @newLoco
          AND  AM.ATypeID = 3
          AND  AM.IsActive = 1;

        -- Check latest reader transaction for new loco
        IF @newLocoTagId IS NOT NULL
        BEGIN
            SELECT TOP 1 
                @newLocoEventID   = ID,
                @newLocoEventTime = TransDatetime
            FROM ReadersTransactionLog
            WHERE TagId = @newLocoTagId
            ORDER BY TransDatetime DESC;
        END

        IF @newLocoEventID IS NULL
        BEGIN
            SET @newLocoEventID   = NEWID();
            SET @newLocoEventTime = SYSDATETIME();
        END

        -- Update existing mappings for the transferred trip's ladles in LocoLadleMappingLog
        UPDATE M
        SET 
            M.LocoTagId         = @newLocoTagId,
            M.LocoAssetSerialNo = @newLocoSerialNo,
            M.LocoEventID       = @newLocoEventID,
            M.LocoEventTime     = @newLocoEventTime,
            M.MappingReason     = 'Transfer from ' + @currentLoco + ' to ' + @newLoco + ' (Trip ' + @newTripID + ')',
            M.CreatedOn         = SYSDATETIME()
        FROM LocoLadleMappingLog M
        INNER JOIN AssetMaster LadleAsset 
                ON LadleAsset.ATagID = M.LadleRfidTag 
               AND LadleAsset.ATypeID = 1
        WHERE LadleAsset.ADescription IN (
            SELECT LMD.LadleNo 
            FROM LadleMovementDetails LMD
            WHERE LMD.LadleMovementID = @ladleMovementID
              AND LMD.IsActive = 1
        )
        AND M.LocoTagId = @oldLocoTagId
        AND M.MappingStatus = 'CONFIRMED';

        SET @status = 1;
        SET @message = 'Loco transferred successfully.';

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        SET @status = 0;
        SET @message = 'Error: ' + ERROR_MESSAGE();
    END CATCH
END
GO
