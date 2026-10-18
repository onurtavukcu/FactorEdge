using FactorEdge.Application.Features.Users.Models;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Contracts.Enums;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.Infrastructure.Persistence.Repositories;

public sealed class TenantDashboardRepository(TenantDbContext dbContext) : ITenantDashboardRepository
{
    public Task<int> CountDevicesAsync(CancellationToken ct = default) 
    {
        return dbContext.Devices.CountAsync(ct);
    }

    public Task<int> CountTelemetryAsync(CancellationToken ct = default)
    {
        return dbContext.DeviceTelemetries.CountAsync(ct);
    }

    public Task<int> CountEventLogsAsync(CancellationToken ct = default)
    {
        return dbContext.DeviceEventLogs.CountAsync(ct);
    }

    public async Task<ProductionEfficiencyDto> GetEfficiencyLastHourAsync(
    CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddHours(-1);

        var telemetry = await dbContext.DeviceTelemetries
            .AsNoTracking()
            .Where(x => x.ReceivedAtUtc >= since)
            .OrderBy(x => x.DeviceId)
            .ThenBy(x => x.ReceivedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.DeviceId,
                x.ProductionCount,
                x.ScrapCount,
                x.Status,
                x.ReceivedAtUtc
            })
            .ToListAsync(ct);

        long totalProduction = 0;
        long totalScrap = 0;

        double observedSeconds = 0;
        double runningSeconds = 0;

        // Simülatörler yaklaşık saniyede bir veri üretiyor.
        // 10 saniyeden uzun boşlukları bağlantı kesintisi kabul ediyoruz.
        const double maxSampleGapSeconds = 10;

        foreach (var deviceGroup in telemetry.GroupBy(x => x.DeviceId))
        {
            var samples = deviceGroup.ToList();

            for (int i = 1; i < samples.Count; i++)
            {
                var previous = samples[i - 1];
                var current = samples[i];

                long productionDelta =
                    Convert.ToInt64(current.ProductionCount) -
                    Convert.ToInt64(previous.ProductionCount);

                long scrapDelta =
                    Convert.ToInt64(current.ScrapCount) -
                    Convert.ToInt64(previous.ScrapCount);

                // Sayaç sıfırlandıysa negatif farkı dahil etme.
                if (productionDelta >= 0)
                    totalProduction += productionDelta;

                if (scrapDelta >= 0)
                    totalScrap += scrapDelta;

                double seconds =
                    (current.ReceivedAtUtc - previous.ReceivedAtUtc)
                    .TotalSeconds;

                if (seconds <= 0 || seconds > maxSampleGapSeconds)
                    continue;

                observedSeconds += seconds;

                // ProductionLineStatus.Running = 1
                // Mevcut enum değerinin gerçekten 1 olduğunu doğrula.
                if (previous.Status == (int)ProductionLineStatus.Running)
                    runningSeconds += seconds;
            }
        }

        return new ProductionEfficiencyDto
        {
            ProductionCount = totalProduction,
            ScrapCount = totalScrap,

            ScrapRate = totalProduction > 0
                ? Math.Round(totalScrap * 100.0 / totalProduction, 2)
                : 0,

            RunningRate = observedSeconds > 0
                ? Math.Round(runningSeconds * 100.0 / observedSeconds, 2)
                : 0,

            ObservedMinutes = Math.Round(observedSeconds / 60.0, 1)
        };
    }
}