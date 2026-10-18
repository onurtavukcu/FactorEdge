namespace FactorEdge.Application.Features.Telemetry;

public static class TemperatureAlarmPolicy
{
    public const double TriggerTemperature = 85.0;
    public const double RecoveryTemperature = 80.0;

    public static bool ShouldActivate(
        double temperature,
        bool isCurrentlyActive)
    {
        return !isCurrentlyActive
            && temperature >= TriggerTemperature;
    }

    public static bool ShouldRecover(
        double temperature,
        bool isCurrentlyActive)
    {
        return isCurrentlyActive
            && temperature <= RecoveryTemperature;
    }
}