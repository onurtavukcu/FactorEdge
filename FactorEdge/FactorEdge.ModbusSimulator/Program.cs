using FactorEdge.ModbusSimulator;
using FactorEdge.ModbusSimulator.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ModbusServer>();
builder.Services.AddSingleton<ProductionLineSimulator>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
