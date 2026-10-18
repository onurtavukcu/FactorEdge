using FactorEdge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactorEdge.Infrastructure.Persistence.Configurations.Base;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserName).IsRequired().HasMaxLength(100);

        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);

        builder.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(256);

        builder.HasIndex(x => x.NormalizedEmail).IsUnique();

        builder.Property(x => x.PasswordHash).IsRequired();

        builder.Property(x => x.Role).HasConversion<int>();

        builder.Property(x => x.IsActive).IsRequired();

        builder.HasOne<Domain.Entities.Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Users_Role_TenantId",
            """
            ("Role" = 1 AND "TenantId" IS NULL)
            OR
            ("Role" IN (2, 3) AND "TenantId" IS NOT NULL)
            """));
    }
}
