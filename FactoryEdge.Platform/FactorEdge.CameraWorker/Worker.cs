
using FactorEdge.CameraWorker.Services;
using OpenCvSharp;
using System.Net.Http;

namespace FactorEdge.CameraWorker;

public sealed class Worker(ILogger<Worker> logger, CameraMqttPublisher mqttPublisher, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) 
    {
        var cameraId = configuration["Camera:Id"] ?? "camera-01";

        var streamUrl = configuration["Camera:StreamUrl"] ?? throw new InvalidOperationException("Camera:StreamUrl tanımlanmamış.");

        var intervalMs = Math.Max(100, configuration.GetValue("Camera:ReadIntervalMs", 300));

        var faceModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "haarcascade_frontalface_default.xml");

        var eyeModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "haarcascade_eye_tree_eyeglasses.xml");

        using var faceClassifier = new CascadeClassifier(faceModelPath);
        using var eyeClassifier = new CascadeClassifier(eyeModelPath);

        if (faceClassifier.Empty() || eyeClassifier.Empty())
            throw new InvalidOperationException("Yüz veya göz modeli yüklenemedi.");

        using var httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        var absentThreshold = TimeSpan.FromSeconds(3);
        var fatigueThreshold = TimeSpan.FromSeconds(5);
        var analysisInterval = TimeSpan.FromMilliseconds(intervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            string? currentStatus = null;
            DateTime lastPublishUtc = DateTime.MinValue;
            var heartbeatInterval = TimeSpan.FromSeconds(5);
            DateTime? faceMissingSince = null;
            DateTime? eyesMissingSince = null;

            try
            {
                logger.LogInformation("Kamera bağlantısı kuruluyor: {CameraId}", cameraId);

                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                connectCts.CancelAfter(TimeSpan.FromSeconds(15));

                using var response = await httpClient.GetAsync(streamUrl, HttpCompletionOption.ResponseHeadersRead, connectCts.Token);

                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(stoppingToken);

                logger.LogInformation("Kamera bağlandı: {CameraId}", cameraId);

                var buffer = new byte[16 * 1024];

                using var jpegBuffer = new MemoryStream();

                bool insideJpeg = false;
                byte previousByte = 0;
                DateTime lastAnalysisUtc = DateTime.MinValue;

                using var resized = new Mat();
                using var gray = new Mat();

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Akış uzun süre veri göndermezse yeniden bağlan.
                    using var readCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                    readCts.CancelAfter(TimeSpan.FromSeconds(15));

                    int bytesRead;

                    try
                    {
                        bytesRead = await stream.ReadAsync(buffer.AsMemory(), readCts.Token);
                    }
                    catch (OperationCanceledException)
                        when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogWarning("Kameradan veri gelmedi: {CameraId}", cameraId);

                        break;
                    }

                    if (bytesRead == 0)
                    {
                        logger.LogWarning("Kamera akışı kapandı: {CameraId}", cameraId);

                        break;
                    }

                    for (int i = 0; i < bytesRead; i++)
                    {
                        byte currentByte = buffer[i];

                        // JPEG başlangıcı: FF D8
                        if (!insideJpeg)
                        {
                            if (previousByte == 0xFF && currentByte == 0xD8)
                            {
                                jpegBuffer.SetLength(0);
                                jpegBuffer.WriteByte(0xFF);
                                jpegBuffer.WriteByte(0xD8);

                                insideJpeg = true;
                            }

                            previousByte = currentByte;
                            continue;
                        }

                        jpegBuffer.WriteByte(currentByte);

                        // Hatalı veya aşırı büyük kareye karşı koruma.
                        if (jpegBuffer.Length > 5 * 1024 * 1024)
                        {
                            jpegBuffer.SetLength(0);
                            insideJpeg = false;
                            previousByte = 0;
                            continue;
                        }

                        // JPEG bitişi: FF D9
                        bool frameCompleted = previousByte == 0xFF && currentByte == 0xD9;

                        previousByte = currentByte;

                        if (!frameCompleted)
                            continue;

                        insideJpeg = false;

                        var now = DateTime.UtcNow;

                        // Kareleri sürekli oku, analizi sınırla.
                        if (now - lastAnalysisUtc < analysisInterval)
                            continue;

                        lastAnalysisUtc = now;

                        using var frame = Cv2.ImDecode(jpegBuffer.ToArray(), ImreadModes.Color);

                        if (frame.Empty())
                            continue;

                        Cv2.Resize(frame, resized, new OpenCvSharp.Size(640, 480));

                        Cv2.CvtColor(resized, gray, ColorConversionCodes.BGR2GRAY);

                        Cv2.EqualizeHist(gray, gray);

                        var faces = faceClassifier.DetectMultiScale(gray, scaleFactor: 1.1, minNeighbors: 8, minSize: new OpenCvSharp.Size(100, 100)); 
                        string status;

                        if (faces.Length == 0)
                        {
                            faceMissingSince ??= now;
                            eyesMissingSince = null;

                            status = now - faceMissingSince.Value
                                >= absentThreshold
                                ? "Absent"
                                : currentStatus ?? "Unknown";
                        }
                        else
                        {
                            faceMissingSince = null;

                            var face = faces
                                .OrderByDescending(f => f.Width * f.Height).First();

                            var eyeRegion = new Rect(face.X, face.Y, face.Width, Math.Max(1, face.Height / 2));

                            using var eyeRoi = new Mat(gray, eyeRegion);

                            var eyes = eyeClassifier.DetectMultiScale(
                                eyeRoi,
                                scaleFactor: 1.1,
                                minNeighbors: 4,
                                minSize: new OpenCvSharp.Size(15, 15));

                            if (eyes.Length > 0)
                            {
                                eyesMissingSince = null;
                                status = "Normal";
                            }
                            else
                            {
                                eyesMissingSince ??= now;

                                status = now - eyesMissingSince.Value
                                    >= fatigueThreshold
                                    ? "Fatigued"
                                    : currentStatus == "Fatigued"
                                        ? "Fatigued"
                                        : "Normal";
                            }
                        }

                        var statusChanged = status != currentStatus;

                        var heartbeatDue = now - lastPublishUtc >= heartbeatInterval;

                        if (statusChanged)
                        {
                            logger.LogInformation("Camera: {CameraId} | Status: {Status}", cameraId, status);
                        }

                        if ((statusChanged || heartbeatDue) && status is ("Normal" or "Fatigued" or "Absent"))
                        {
                            try
                            {
                                await mqttPublisher.PublishAsync(cameraId, status, stoppingToken);

                                lastPublishUtc = DateTime.UtcNow;
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning("Camera MQTT publish başarısız: {Message}", ex.Message);
                            }
                        }

                        currentStatus = status;
                    }
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("CameraWorker hatası: {Message}", ex.Message);
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            await Task.Delay(3000, stoppingToken);
        }
    }
}
