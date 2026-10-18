
using FactorEdge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactorEdge.Infrastructure.Configurations;

public sealed class CameraStatusHistoryConfiguration
    : IEntityTypeConfiguration<CameraStatusHistory>
{
    public void Configure(
        EntityTypeBuilder<CameraStatusHistory> builder)
    {
        builder.ToTable("CameraStatusHistories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.DeviceId,
            x.CreatedAtUtc
        });

        builder.HasOne(x => x.Device)
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
