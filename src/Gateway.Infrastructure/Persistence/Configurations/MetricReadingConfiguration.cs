using Gateway.Domain.Entities;
using Gateway.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gateway.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mirrors MetricReadingRecordConfiguration in DataProcessor. Any divergence here is a silent
/// runtime failure, which is why the mapping is asserted by Gateway.Infrastructure.Tests and,
/// from Phase 6, replayed against the real DataProcessor schema in a container.
/// </summary>
public class MetricReadingConfiguration : IEntityTypeConfiguration<MetricReading>
{
    public void Configure(EntityTypeBuilder<MetricReading> builder)
    {
        builder.ToTable("MetricReadings");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .UseIdentityColumn();

        builder.Property(r => r.Room)
            .HasMaxLength(200)
            .IsRequired();

        // datetime2 stores no offset, so EF materialises DateTimeKind.Unspecified. Left alone, the
        // GraphQL DateTime scalar would stamp these with the server's local offset - correct only
        // by accident in a UTC container. Pinning Kind on the way out makes it correct on purpose.
        // Provider-side conversion is the identity, so the column expression in SQL is unchanged
        // and Phase 4's DateDiff bucketing still translates.
        builder.Property(r => r.IngestedAtUtc)
            .HasColumnType("datetime2")
            .HasConversion(
                utc => utc,
                stored => DateTime.SpecifyKind(stored, DateTimeKind.Utc))
            .IsRequired();

        builder.Property(r => r.ReceivedAtUtc)
            .HasColumnType("datetime2")
            .HasConversion(
                utc => utc,
                stored => DateTime.SpecifyKind(stored, DateTimeKind.Utc))
            .IsRequired();

        // Descriptive only: this context never migrates. It records the index the read queries
        // depend on, so the mapping stays an accurate mirror of the writer's schema.
        builder.HasIndex(r => new { r.Room, r.ReceivedAtUtc });

        // The writer keeps the discriminator as a shadow property; the read model maps it to a real
        // one so it can be filtered, sorted and projected. Same column, same string values.
        builder.Property(r => r.ReadingType)
            .HasColumnName("ReadingType")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasDiscriminator(r => r.ReadingType)
            .HasValue<EnergyReading>(MetricReadingType.Energy)
            .HasValue<AirQualityReading>(MetricReadingType.AirQuality)
            .HasValue<MotionReading>(MetricReadingType.Motion);
    }
}
