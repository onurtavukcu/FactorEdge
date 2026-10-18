using FactorEdge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactorEdge.Infrastructure.Persistence.Configurations.Tenant;

public sealed class DeviceCameraConfiguration : IEntityTypeConfiguration<DeviceCamera>
{
    public void Configure(EntityTypeBuilder<DeviceCamera> builder)
    {
        builder.ToTable("DeviceCameras");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StreamUrl)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(x => x.ContainerName)
            .HasMaxLength(256);

        builder.Property(x => x.LastOperatorStatus)
            .IsRequired()
            .HasMaxLength(32);

        builder.HasIndex(x => x.DeviceId)
            .IsUnique();

        builder.HasOne(x => x.Device)
            .WithOne()
            .HasForeignKey<DeviceCamera>(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}