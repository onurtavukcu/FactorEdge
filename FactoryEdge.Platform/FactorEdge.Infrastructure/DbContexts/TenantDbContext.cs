using FactorEdge.Domain.Entities;
using FactorEdge.Infrastructure.Configurations;
using FactorEdge.Infrastructure.Persistence.Configurations.Tenant;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.Infrastructure.DbContexts
{
    public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
    {
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<DeviceTelemetry> DeviceTelemetries => Set<DeviceTelemetry>();
        public DbSet<DeviceEventLog> DeviceEventLogs => Set<DeviceEventLog>();
        public DbSet<DeviceCamera> DeviceCameras => Set<DeviceCamera>();
        public DbSet<CameraStatusHistory> CameraStatusHistories => Set<CameraStatusHistory>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new DeviceConfiguration());

            modelBuilder.ApplyConfiguration(new DeviceTelemetryConfiguration());

            modelBuilder.ApplyConfiguration(new DeviceEventLogConfiguration());
            modelBuilder.ApplyConfiguration(new DeviceCameraConfiguration());
            modelBuilder.ApplyConfiguration(new CameraStatusHistoryConfiguration());
        }
    }
}
