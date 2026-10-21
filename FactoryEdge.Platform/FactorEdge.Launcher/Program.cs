
using System.Diagnostics;

var root = AppContext.BaseDirectory;

var apps = new[]
{
    ("MQTT Worker", "Workers/MQTT/FactorEdge.MqttWorker.exe"),
    ("Modbus Worker", "Workers/Modbus/FactorEdge.ModbusWorker.exe"),
    ("Web", "Web/FactorEdge.Web.exe")
};

var processes = new List<(string Name, Process Process)>();

Console.WriteLine("FactorEdge başlatılıyor...\n");

foreach (var (name, relativePath) in apps)
{
    var path = Path.GetFullPath(
        Path.Combine(root, relativePath));

    if (!File.Exists(path))
    {
        Console.WriteLine($"[HATA] {name} bulunamadı: {path}");
        continue;
    }

    try
    {
        var info = new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path)!,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        if (name == "Web")
        {
            info.ArgumentList.Add("--urls");
            info.ArgumentList.Add("http://localhost:5000");
        }

        var process = Process.Start(info);

        if (process != null)
        {
            processes.Add((name, process));

            Console.WriteLine(
                $"[OK] {name} başlatıldı. PID: {process.Id}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[HATA] {name}: {ex.Message}");
    }
}

Console.WriteLine("\nWeb uygulaması bekleniyor...");

using var client = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(2)
};

var url = "http://localhost:5000";
var ready = false;

for (int i = 0; i < 30; i++)
{
    try
    {
        using var response = await client.GetAsync(url);
        ready = true;
        break;
    }
    catch (HttpRequestException) { }
    catch (TaskCanceledException) { }

    await Task.Delay(1000);
}

if (ready)
{
    Process.Start(new ProcessStartInfo
    {
        FileName = url,
        UseShellExecute = true
    });

    Console.WriteLine("[OK] Web arayüzü açıldı.");
}
else
{
    Console.WriteLine("[HATA] Web uygulaması hazır değil.");
}

Console.WriteLine("\nFactorEdge servisleri başlatıldı.");
Console.WriteLine("Çıkmak için Enter tuşuna basın.");

Console.ReadLine();
