using FactorEdge.CameraWorker;
using FactorEdge.CameraWorker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<CameraMqttPublisher>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();