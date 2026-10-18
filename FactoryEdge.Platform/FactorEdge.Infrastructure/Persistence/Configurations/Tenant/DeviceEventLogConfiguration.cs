using FactorEdge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactorEdge.Infrastructure.Persistence.Configurations.Tenant
{
    public sealed class DeviceEventLogConfiguration
    : IEntityTypeConfiguration<DeviceEventLog>
    {
        public void Configure(EntityTypeBuilder<DeviceEventLog> builder)
        {
            builder.ToTable("DeviceEventLogs");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.EventType).HasConversion<int>().IsRequired();

            builder.Property(x => x.OldValue).HasMaxLength(100);

            builder.Property(x => x.NewValue).HasMaxLength(100).IsRequired();

            builder.Property(x => x.Description).HasMaxLength(500);

            builder.Property(x => x.OccurredAtUtc).HasColumnType("timestamp with time zone").IsRequired();

            builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new
            {
                x.DeviceId,
                x.OccurredAtUtc
            });
        }
    }
}
