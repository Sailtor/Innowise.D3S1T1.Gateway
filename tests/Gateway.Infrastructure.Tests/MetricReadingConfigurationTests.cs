using Gateway.Domain.Entities;
using Gateway.Domain.Enums;
using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Gateway.Infrastructure.Tests;

/// <summary>
/// Asserts the read model still describes the table DataProcessor writes. These run without a
/// database - EF builds the model from the configuration alone - so they catch a mapping typo
/// immediately, long before the Phase 6 container test replays the real schema.
/// </summary>
public class MetricReadingConfigurationTests
{
    private readonly IModel model = BuildModel();

    private IEntityType BaseType => model.FindEntityType(typeof(MetricReading))!;

    [Fact]
    public void MapsTheHierarchyToTheMetricReadingsTable()
    {
        Assert.Equal("MetricReadings", BaseType.GetTableName());
    }

    [Fact]
    public void MapsRoomAsRequiredWithTheWritersMaxLength()
    {
        IProperty room = BaseType.GetProperty(nameof(MetricReading.Room));

        Assert.False(room.IsColumnNullable());
        Assert.Equal(200, room.GetMaxLength());
    }

    [Theory]
    [InlineData(nameof(MetricReading.IngestedAtUtc))]
    [InlineData(nameof(MetricReading.ReceivedAtUtc))]
    public void MapsTimestampsAsDateTime2(string propertyName)
    {
        Assert.Equal("datetime2", BaseType.GetProperty(propertyName).GetColumnType());
    }

    [Theory]
    [InlineData(nameof(MetricReading.IngestedAtUtc))]
    [InlineData(nameof(MetricReading.ReceivedAtUtc))]
    public void MaterialisesTimestampsAsUtcBecauseDateTime2CarriesNoOffset(string propertyName)
    {
        ValueConverter converter = BaseType.GetProperty(propertyName).GetValueConverter()!;

        DateTime stored = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Unspecified);
        DateTime materialised = (DateTime)converter.ConvertFromProvider(stored)!;

        Assert.Equal(DateTimeKind.Utc, materialised.Kind);
    }

    [Fact]
    public void MapsTheDiscriminatorOntoTheReadingTypeColumn()
    {
        IProperty discriminator = BaseType.FindDiscriminatorProperty()!;

        Assert.Equal(nameof(MetricReading.ReadingType), discriminator.Name);
        Assert.Equal("ReadingType", discriminator.GetColumnName());
        Assert.Equal(20, discriminator.GetMaxLength());
    }

    [Theory]
    [InlineData(MetricReadingType.Energy, "Energy")]
    [InlineData(MetricReadingType.AirQuality, "AirQuality")]
    [InlineData(MetricReadingType.Motion, "Motion")]
    public void StoresTheDiscriminatorAsTheEnumName(MetricReadingType readingType, string expected)
    {
        // HasConversion<string>() sets the provider CLR type and leaves GetValueConverter() null;
        // the EnumToStringConverter that actually runs lives on the type mapping.
        ValueConverter converter = BaseType.FindDiscriminatorProperty()!.GetTypeMapping().Converter!;

        Assert.Equal(expected, (string?)converter.ConvertToProvider(readingType));
    }

    [Theory]
    [InlineData(typeof(EnergyReading), MetricReadingType.Energy)]
    [InlineData(typeof(AirQualityReading), MetricReadingType.AirQuality)]
    [InlineData(typeof(MotionReading), MetricReadingType.Motion)]
    public void MapsEachDerivedTypeToItsDiscriminatorValue(Type clrType, MetricReadingType expected)
    {
        object? discriminatorValue = model.FindEntityType(clrType)!.GetDiscriminatorValue();

        Assert.Equal(expected, (MetricReadingType)discriminatorValue!);
    }

    [Theory]
    [InlineData(typeof(EnergyReading), nameof(EnergyReading.EnergyAmount))]
    [InlineData(typeof(AirQualityReading), nameof(AirQualityReading.Co2))]
    [InlineData(typeof(AirQualityReading), nameof(AirQualityReading.Pm25))]
    [InlineData(typeof(AirQualityReading), nameof(AirQualityReading.Humidity))]
    [InlineData(typeof(MotionReading), nameof(MotionReading.IsMotionDetected))]
    public void MapsDerivedPropertiesToNullableColumnsNamedAfterTheProperty(Type clrType, string propertyName)
    {
        IProperty property = model.FindEntityType(clrType)!.GetProperty(propertyName);

        Assert.Equal(propertyName, property.GetColumnName());

        // Table-per-hierarchy: every derived column has to be nullable because rows of the other
        // two types leave it empty. Note this is IsColumnNullable, not IsNullable - the CLR types
        // are non-nullable, and TPH nullability exists only at the column level. This is exactly
        // why Phase 4 aggregations must pre-filter by reading type.
        Assert.True(property.IsColumnNullable());
    }

    [Fact]
    public void SaveChangesThrowsBecauseTheContextIsReadOnly()
    {
        using MetricsReadDbContext context = CreateContext();

        Assert.Throws<InvalidOperationException>(() =>
        {
            context.SaveChanges();
        });
    }

    [Fact]
    public async Task SaveChangesAsyncThrowsBecauseTheContextIsReadOnly()
    {
        await using MetricsReadDbContext context = CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static MetricsReadDbContext CreateContext()
    {
        DbContextOptions<MetricsReadDbContext> options =
            new DbContextOptionsBuilder<MetricsReadDbContext>()
                .UseSqlServer("Server=(local);Database=GatewayModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;

        return new MetricsReadDbContext(options);
    }

    private static IModel BuildModel()
    {
        using MetricsReadDbContext context = CreateContext();

        return context.Model;
    }
}
