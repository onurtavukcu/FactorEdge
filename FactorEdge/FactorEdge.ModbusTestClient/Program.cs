using NModbus;
using System.Net.Sockets;

using var client = new TcpClient();

await client.ConnectAsync("127.0.0.1", 5020);

var factory = new ModbusFactory();
var master = factory.CreateMaster(client);

const byte unitId = 1;

// 30001–30004
var input = master.ReadInputRegisters(unitId, 0, 4);

// uint32 ABCD
uint production =
    ((uint)input[0] << 16) | input[1];

// float32 CDAB
uint temperatureBits =
    ((uint)input[3] << 16) | input[2];

float temperature =
    BitConverter.UInt32BitsToSingle(temperatureBits);

// 30010
var currentRegisters =
    master.ReadInputRegisters(unitId, 9, 1);

short currentRaw =
    unchecked((short)currentRegisters[0]);

double current = currentRaw * 0.01;

// 30020
var scrapRegisters =
    master.ReadInputRegisters(unitId, 19, 1);

// 40001
var statusRegisters =
    master.ReadHoldingRegisters(unitId, 0, 1);

Console.WriteLine($"Production : {production}");
Console.WriteLine($"Temperature: {temperature:F2} °C");
Console.WriteLine($"Current    : {current:F2} A");
Console.WriteLine($"Scrap      : {scrapRegisters[0]}");
Console.WriteLine($"Status     : {statusRegisters[0]}");

Console.ReadLine();