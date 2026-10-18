
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactorEdge.Infrastructure.Persistence.Configurations.Tenant
{
    public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
    {
        public void Configure(EntityTypeBuilder<Device> builder)
        {
            builder.ToTable("Devices");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).ValueGeneratedNever();

            builder.Property(x => x.DeviceCode)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(x => x.DeviceCode).IsUnique();

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.DeviceType)
                .HasConversion<int>()
                .HasDefaultValue(DeviceType.ProductionLine)
                .IsRequired();

            builder.Property(x => x.Protocol)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.Host)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(x => x.Port).IsRequired();

            builder.Property(x => x.Topic).HasMaxLength(500);

            builder.Property(x => x.RegisterMapJson)
                .HasColumnType("jsonb");

            builder.Property(x => x.ConnectionStatus)
                .HasConversion<int>();

            builder.Property(x => x.CreatedAtUtc)
                .HasColumnType("timestamp with time zone");

            builder.Property(x => x.LastSeenUtc)
                .HasColumnType("timestamp with time zone");
        }
    }
}
