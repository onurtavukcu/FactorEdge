using FactorEdge.ModbusWorker;
using FactorEdge.ModbusWorker.Services;
using FactorEdge.Application;
using FactorEdge.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ModbusReader>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
