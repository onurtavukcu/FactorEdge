using FactorEdge.Contracts.Enums;
using FactorEdge.ModbusSimulator.Services;

namespace FactorEdge.ModbusSimulator;

public sealed class Worker(
    ILogger<Worker> logger,
    ModbusServer server,
    ProductionLineSimulator simulator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) 
    {
        var serverTask = server.StartAsync(stoppingToken);

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            int elapsedSeconds = 0;

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (serverTask.IsCompleted)
                {
                    await serverTask;

                    throw new InvalidOperationException("Modbus server stopped unexpectedly.");
                }

                // 40 saniyelik tekrar eden senaryo
                int cycleSecond = elapsedSeconds % 40;

                ProductionLineStatus status =
                    cycleSecond switch
                    {
                        < 20 => ProductionLineStatus.Running,
                        < 30 => ProductionLineStatus.Stopped,
                        _ => ProductionLineStatus.Fault
                    };

                simulator.SetStatus(status);

                var data = simulator.Generate();

                server.UpdateRegisters(data);

                logger.LogInformation(
                    "Status: {Status} | " +
                    "Production: {Production} | " +
                    "Temperature: {Temperature} | " +
                    "Current: {Current} | " +
                    "Scrap: {Scrap}",
                    data.Status,
                    data.ProductionCount,
                    data.MotorTemperature,
                    data.MotorCurrent,
                    data.ScrapCount);

                elapsedSeconds++;
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanış
        }
        finally
        {
            await serverTask;
        }
    }
}