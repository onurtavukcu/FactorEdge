namespace FactorEdge.Domain.Enums
{
    public enum DeviceEventType
    {
        InitialStatusDetected = 1,
        MachineStatusChanged = 2,
        ConnectionChanged = 3,
        DeviceActivated = 4,
        DeviceDeactivated = 5,
        DeviceSoftDeleted = 6,
        TemperatureAlarmTriggered = 7,
        TemperatureAlarmRecovered = 8
    }
}
