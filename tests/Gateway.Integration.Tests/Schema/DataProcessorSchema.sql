-- The schema this service reads but does not own.
--
-- Transcribed from DataProcessor's InitialCreate migration
-- (src/DataProcessor.Infrastructure/Migrations/20260703115548_InitialCreate.cs). Regenerate with:
--
--   dotnet ef migrations script --idempotent --project src/DataProcessor.Infrastructure \
--       --startup-project src/DataProcessor.AppHost --output DataProcessorSchema.sql
--
-- run from the DataProcessor repository. Keeping this file a faithful copy is the whole point of
-- the integration suite: if the writer's schema moves and this does not, these tests still pass
-- and production breaks. A scheduled CI job that regenerates and diffs it is the next step.

IF OBJECT_ID(N'[MetricReadings]', N'U') IS NULL
BEGIN
    CREATE TABLE [MetricReadings] (
        [Id] bigint NOT NULL IDENTITY(1, 1),
        [Room] nvarchar(200) NOT NULL,
        [IngestedAtUtc] datetime2 NOT NULL,
        [ReceivedAtUtc] datetime2 NOT NULL,
        [ReadingType] nvarchar(20) NOT NULL,
        [Co2] float NULL,
        [Pm25] float NULL,
        [Humidity] float NULL,
        [EnergyAmount] float NULL,
        [IsMotionDetected] bit NULL,
        CONSTRAINT [PK_MetricReadings] PRIMARY KEY ([Id])
    );

    CREATE INDEX [IX_MetricReadings_Room_ReceivedAtUtc]
        ON [MetricReadings] ([Room], [ReceivedAtUtc]);
END;
