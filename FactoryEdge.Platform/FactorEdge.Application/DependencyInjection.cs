using FactorEdge.Application.Features.Camera.Service;
using FactorEdge.Application.Features.Device.Services;
using FactorEdge.Application.Interfaces.Services;
using FactorEdge.Application.Interfaces.Services.Camera;
using FactorEdge.Application.Interfaces.Services.Device;
using Microsoft.Extensions.DependencyInjection;

namespace FactorEdge.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IDeviceService, DeviceService>();
            services.AddScoped<IDeviceTelemetryService, DeviceTelemetryService>();
            services.AddScoped<IDeviceConfigurationService, DeviceConfigurationService>();
            services.AddScoped<IDeviceConnectionStatusService, DeviceConnectionStatusService>();
            services.AddScoped<IDeviceEventLogService, DeviceEventLogService>();
            services.AddScoped<ICameraService, CameraService>();

            return services;
        }
    }
}
