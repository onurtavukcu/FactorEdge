using FactorEdge.MqttSimulator;
using FactorEdge.MqttSimulator.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ProductionLineSimulator>();
builder.Services.AddSingleton<MqttPublisher>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
