using FactorEdge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactorEdge.Infrastructure.Persistence.Configurations.Tenant
{
    public sealed class DeviceTelemetryConfiguration : IEntityTypeConfiguration<DeviceTelemetry>
    {
        public void Configure(EntityTypeBuilder<DeviceTelemetry> builder)
        {
            builder.ToTable("DeviceTelemetries");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.ProductionCount).HasConversion<long>();

            builder.Property(x => x.ScrapCount).HasConversion<long>();

            builder.Property(x => x.MotorTemperature).IsRequired();

            builder.Property(x => x.MotorCurrent).IsRequired();

            builder.Property(x => x.TimestampUtc).HasColumnType("timestamp with time zone").IsRequired();

            builder.Property(x => x.ReceivedAtUtc).HasColumnType("timestamp with time zone").IsRequired();

            builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");

            builder.HasIndex(x => new
            {
                x.DeviceId,
                x.TimestampUtc
            });
        }
    }
}
