
using FactorEdge.Application;
using FactorEdge.Infrastructure;
using FactorEdge.MqttWorker;
using FactorEdge.MqttWorker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Üretim hattı MQTT Consumer
builder.Services.AddSingleton<MqttSubscriber>();
builder.Services.AddHostedService<Worker>();

// Kamera MQTT Consumer
builder.Services.AddSingleton<CameraMqttSubscriber>();
builder.Services.AddHostedService<CameraSubscriberWorker>();

// Kamera bağlantı / offline kontrolü
builder.Services.AddHostedService<CameraOfflineMonitoringService>();

var host = builder.Build();
host.Run();
