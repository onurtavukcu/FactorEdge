# FactorEdge — Mini Akıllı Fabrika

> **Case study / teknik demo dokümantasyonu**  
> Kapsam: Ubuntu üzerindeki MQTT ve Modbus simülatörleri, gerçek IP kamerayı işleyen CameraWorker, Windows üzerinde çalışan MQTT/Modbus tüketici servisleri ve ASP.NET Core web platformu.

## 1. Kurulum ve çalıştırma adımları

### Önkoşullar

- **Windows host:** Uygun .NET SDK/runtime (projede .NET 10 kullanıldı), Visual Studio veya CLI, erişilebilir PostgreSQL.
- **Ubuntu sanal makine:** Docker, host bilgisayardan erişilebilen ağ bağlantısı, sahayı temsil eden container'lar.
- **MQTT:** Ubuntu'da çalışan Mosquitto broker (`1883`); hostun broker'a erişebilmesi gerekir.
- **Modbus:** Simülatör container'ının `5020` iç portunun Ubuntu hostunda yayınlanan **gerçek host portu**. Birden çok container için farklı host portları kullanılabilir.
- **Kamera (bonus):** Ubuntu'nun erişebildiği gerçek IP kameranın MJPEG URL'si (örneğin telefondaki IP Webcam uygulaması).
- Veritabanı bağlantıları, broker adresleri ve cihaz URL'leri çalıştırılan ortama göre yapılandırılmalıdır. Parolalar, token'lar ve anahtarlar repoya yazılmamalıdır.

### A. Ubuntu / edge servislerini hazırlama

1. Ubuntu VM'yi başlatın; Windows ↔ Ubuntu ağ bağlantısını doğrulayın.
2. Docker servisinin çalıştığını doğrulayın; mevcut Mosquitto container'ının ayakta olduğundan emin olun.
3. Simülatör ve kamera image'larının/oluşturma script'lerinin bulunduğu `~/factoredge-deploy/` dizinini kontrol edin.
4. Simülatörleri uygun tenant/cihaz bilgileriyle **platform üzerindeki cihaz oluşturma/provisioning akışı üzerinden** başlatın. Provisioning ilgili Docker container'ını Ubuntu'da oluşturur. Alternatif manuel kurulumda ilgili script'in argümanları kendi dosyasından doğrulanmalıdır.
5. Modbus için platformdaki IP/host portun, Docker'ın `5020` iç portuna olan eşlemesiyle aynı olduğunu; MQTT için broker/topic ayarlarının eşleştiğini doğrulayın.

Tanılama komutları (her biri tek satır):

```bash
docker ps
docker logs --tail 50 factoredge-mosquitto
ls -la ~/factoredge-deploy/scripts
```

> Örnek laboratuvar IP'si `192.168.223.131` idi; bu adres taşınabilir bir varsayılan değildir. Yeni kurulumda sanal makinenin güncel IP'sini kullanın. Kamera/Modbus/MQTT container adları dinamik olabileceği için gerçek adlarını `docker ps -a` ile belirleyin.

### B. Windows / merkezi platformu hazırlama

1. PostgreSQL servisini başlatın; connection string'i, kullanıcı yetkilerini ve `factoredge.base` için veritabanı oluşturma iznini kontrol edin.
2. Merkezi platformun konfigürasyonunu (PostgreSQL, broker, SSH/Ubuntu bağlantısı, gerekli seed ayarları) kullanılan geliştirme ortamına göre girin; gizli değerleri kaynak koduna sabitlemeyin.
3. **Önce `FactorEdge.Web`'i başlatın.** Web'in başlangıcında `InitializeDatabaseAsync()` çalışır: `BaseDbContext.Database.MigrateAsync()` merkezi `factoredge.base` veritabanına mevcut migration'ları uygular, ardından `SuperAdminSeeder.SeedAsync()` çağrılır.
4. Web üzerinden SuperAdmin hesabıyla giriş yapın; tenant oluşturma işlemini gerçekleştirin. Tenant bazlı veritabanı hazırlama/migration adımının güncel tenant provisioning kodunda doğrulanması gerekir.
5. **Ardından** `FactorEdge.MqttWorker` ve `FactorEdge.ModbusWorker` servislerini çalıştırın.
6. Arayüzden Modbus ve MQTT cihazlarını oluşturun/bağlayın; dashboard, bağlantı durumu ve geçmiş grafiğini gözlemleyin.
7. Kamera bonusu için kamera kaydını ve Ubuntu'daki CameraWorker container'ını oluşturun; normal/yorgun/kişi yok durumlarını izleyin.

Örnek CLI başlangıcı (solution dizininde, projelerin bağımlılıkları ve ayarları hazırken):

```powershell
dotnet run --project .\FactorEdge.Web\FactorEdge.Web.csproj
dotnet run --project .\FactorEdge.MqttWorker\FactorEdge.MqttWorker.csproj
dotnet run --project .\FactorEdge.ModbusWorker\FactorEdge.ModbusWorker.csproj
```

**Başlatma bağımlılığı:** Merkezi DB silinmişken Modbus/MQTT Worker önce başlatılırsa `3D000: database "factoredge.base" does not exist` hatası alınabilir. Önce Web'in migration/seed aşamasını tamamlaması beklenmelidir. Bu, reset sonrasında gözlenen ve doğrulanan bir durumdur.

### C. Sıfırdan kurulum hakkında sınır

Bu README, mevcut demo ortamının doğrulanan akışını belgeliyor; bütün altyapıyı tek komutla kuran bir installer olduğunu **iddia etmiyor**. Ubuntu image/build, SSH anahtarları, PostgreSQL kullanıcı izinleri, seed hesabı ve tenant DB provisioning parametreleri ortama bağlıdır. Bunlar olmadan yalnızca Web dosyalarını kopyalamak tüm platformu çalıştırmak için yeterli değildir.

## 2. Mimari: bileşenler ve veri akışı

```text
                     UBUNTU VM / DOCKER (EDGE)
    ┌──────────────────────────────────────────────────┐
    │ MQTT Simulator ──publish──┐                      │
    │                          ├──> Mosquitto :1883 ──┼──┐
    │ Gerçek IP Kamera          │                      │  │
    │       │ MJPEG             │                      │  │
    │       ▼                   │                      │  │
    │ CameraWorker (OpenCV) ────┘                      │  │
    │                                                  │  │
    │ Modbus Simulator (TCP; container içi :5020) ────┼──┼─┐
    └──────────────────────────────────────────────────┘  │ │
                                                         │ │
                    WINDOWS HOST / MERKEZİ PLATFORM      │ │
    ┌────────────────────────────────────────────────────┼─┼─┐
    │ FactorEdge.MqttWorker <────────────────────────────┘ │ │
    │   ├─ MQTT üretim telemetrisi                           │ │
    │   └─ Kamera durum aboneliği / offline takibi           │ │
    │ FactorEdge.ModbusWorker <──────────────────────────────┘ │
    │   └─ Modbus register polling                            │
    │                       │                                   │
    │                       ▼                                   │
    │ PostgreSQL: Base DB + tenant başına ayrı operasyonel DB  │
    │                       ▲                                   │
    │                       │                                   │
    │ FactorEdge.Web (ASP.NET Core MVC, SignalR)               │
    │   └─ Dashboard, cihaz/kamera, kullanıcı/tenant yönetimi  │
    └───────────────────────────────────────────────────────────┘
```

> Şema mantıksal veri akışını gösterir. Modbus istemcisi Mosquitto üzerinden geçmez; doğrudan Modbus TCP sunucusuna bağlanır. CameraWorker da bir simülatör değil, gerçek kameradan veri alan **edge işleme servisi**dir.

### Çalışma ve kod organizasyonu

| Çalışma alanı | Bileşen | Sorumluluk |
|---|---|---|
| Ubuntu / Docker | `FactorEdge.MqttSimulator` | İlişkili sentetik üretim telemetrisi üretip MQTT'ye yayınlar |
| Ubuntu / Docker | `FactorEdge.ModbusSimulator` | Üretim durumunu Modbus register haritasında sunar |
| Ubuntu / Docker | `FactorEdge.CameraWorker` | MJPEG okur, OpenCV analizi yapar, MQTT kamera durumu yayınlar |
| Ubuntu / Docker | `factoredge-mosquitto` | MQTT broker; publisher/subscriber iletişimi |
| Windows | `FactorEdge.MqttWorker` | MQTT verilerini ve kamera durumlarını tüketir; bağlantı/offline takibi |
| Windows | `FactorEdge.ModbusWorker` | Modbus kaynaklarını periyodik sorgular ve telemetriye çevirir |
| Windows | `FactorEdge.Web` | Web arayüzü, yönetim, tenant oluşturma, canlı gösterim |
| Windows / PostgreSQL | `FactorEdge.Infrastructure`, `Application`, `Domain`, `Contracts` | Kalıcılık, iş akışı, alan modelleri ve paylaşılan sözleşmeler |

**Önemli:** CameraWorker kodu platform solution'ından simülatör solution'ına *Existing Project* olarak eklenmiş olabilir; bu yalnızca IDE organizasyonudur. Projenin fiziksel klasörünü veya bağımsız Linux deployment'ını değiştirmez.

## 3. Veri modeli

**Merkezî veritabanı — `factoredge.base`**

- Tenant kayıtları / tenant konfigürasyonu
- Kullanıcılar ve merkezi kimlik/yetki işlemleri
- SuperAdmin başlangıç verisi

**Tenant başına ayrı veritabanı — `factoredge_tenant_...`**

- Cihaz tanımları, protokol ve bağlantı konfigürasyonu
- Birleştirilmiş üretim telemetrisi ve zaman damgaları
- Kamera durumu, son görülme ve durum değişikliği geçmişi

**Ortak telemetri alanları (mantıksal görünüm):** `DeviceId`, `TimestampUtc`, `ProductionCount`, `MotorTemperature` (°C), `MotorCurrent` (A), `ScrapCount`, `Status` (`Stopped`/`Running`/`Fault`). Buradaki alan adları kavramsal isimlerdir; gerçek entity ve kolon isimleri için `Domain` ile EF Core modelleri esas alınmalıdır.

**İlişki özeti:** `Tenant → Devices → Telemetry`; kamera cihazları ayrıca durum ve geçmiş kayıtlarına sahiptir. Cihazı tenant DB ile ilişkilendirme, farklı tenant'ların operasyonel verilerini ayırır.

## 4. MQTT topic şeması ve mesaj biçimi

### 4.1 MQTT üretim telemetrisi

**Topic şeması:**

```text
factory/{tenantId}/{deviceCode}/telemetry
```

- `{tenantId}`: ilgili tenant GUID'i
- `{deviceCode}`: kaynağın cihaz kodu
- Mesajlar JSON biçimindedir ve zaman damgası taşır.
- Publisher MQTT simülatörü; consumer `FactorEdge.MqttWorker`'dır. İletim Mosquitto üzerinden gerçekleşir.

**Örnek kavramsal JSON** (gerçek serializer property adları/sözleşmesi `Contracts` veya simulator koduyla karşılaştırılmalıdır):

```json
{
  "productionCount": 128,
  "motorTemperature": 73.4,
  "motorCurrent": 4.26,
  "scrapCount": 3,
  "status": "Running",
  "timestampUtc": "2026-10-14T09:00:00Z"
}
```

> Bu örnek payload **açıklayıcıdır**, koddan birebir doğrulanmış bir yayın çıktısı değildir. Demo öncesinde `mosquitto_sub` ile yakalanan gerçek mesajı README'ye koymak gerekir.

### 4.2 MQTT kamera durumu

**Topic şeması:**

```text
factoredge/{tenantId}/camera/{cameraId}/status
```

**Örnek mesaj:**

```json
{
  "cameraId": "00000000-0000-0000-0000-000000000001",
  "status": "Absent",
  "timestampUtc": "2026-10-14T09:00:00Z"
}
```

- Durum sınıfları: `Normal`, `Fatigued`, `Absent`.
- Güncel CameraWorker kodunda değişim veya yaklaşık **5 saniyelik heartbeat** süresi dolduğunda aynı durum yeniden yayınlanabilir.
- Windows tarafındaki subscriber geçerli kamera mesajlarında son durum/son görülme zamanını günceller. Ayrı offline kontrolü belirli süre mesaj gelmezse kamerayı kopuk olarak işaretler (mevcut kod incelemesinde **15 saniye eşik, 5 saniye tarama**).
- Kamera durumunun *içerik analizi* edge tarafında yapılır; broker'a ham video gönderilmez.

### 4.3 Modbus TCP — Bant 1

Case study'nin register haritası:

| Register | Fonksiyon | Tip | Word/byte sırası | Ölçek | Değer |
|---|---|---|---|---|---|
| `30001–30002` | `04` Input Registers | `uint32` | `ABCD` | 1 | Üretim adedi |
| `30003–30004` | `04` Input Registers | `float32` | `CDAB` | 1 | Motor sıcaklığı (°C) |
| `30010` | `04` Input Registers | `int16` | — | 0,01 | Motor akımı (A) |
| `30020` | `04` Input Registers | `uint16` | — | 1 | Fire adedi |
| `40001` | `03` Holding Registers | `uint16` | — | — | Durum: `0=Stopped`, `1=Running`, `2=Fault` |

**Unit ID:** `1`. **Simülatörün container iç portu:** `5020`. Hosttan erişimde Docker host-port eşlemesine dikkat edilir.

Modbus addressing ayrıntısı: `30001` gibi insan okunur register etiketleri uygulama/kütüphanede genellikle **zero-based offset** olarak kullanılır (`30001 → 0`, `30010 → 9`, `30020 → 19`, `40001 → 0`). `float32 CDAB` dönüşümü ile `0,01` akım ölçeği hem simülatörde hem tüketici tarafta eşleşmelidir.

### 4.4 Üretim davranışı

Simülatörler salt bağımsız rastgele sayılar üretmez. Üretim çalışırken üretim adedi artar, durduğunda sabit kalır; sıcaklık çalışma/arızaya göre yükselir ve duruşta soğur; motor akımı duruşta sıfıra yaklaşır; fire sayısı üretime bağlı olarak seyrek artar.

İncelenen demo senaryosunda yaklaşık **40 saniyelik** çevrim kullanıldı: **20 saniye Running → 10 saniye Stopped → 10 saniye Fault**. Üretim artışı ve rastlantısal fire üretimi senaryoyu gerçekçi kılmayı amaçlar. Bu süreler konfigürasyon/kodda değiştirilebilir; güncel çalışma koşulları esas alınmalıdır.

## 5. Verdiğim en önemli üç karar ve nedenleri

### Karar 1 — Simülatör/edge ve platformu ayrı süreçler olarak tasarlamak

**Neden:** Gerçek fabrika cihazının ağ üzerinden ulaşılabilir bağımsız bir kaynak olmasını temsil eder. Bir simülatör durdurulduğunda Web process'i kapanmaz; kaynak yeniden geldiğinde worker tekrar okuyabilir. CameraWorker da görüntü analizini merkezî Web sunucusuna yüklemez.

### Karar 2 — MQTT ve Modbus için ayrı consumer Worker kullanmak; ortak telemetri modeli

**Neden:** İki protokolün çalışma biçimi farklıdır: MQTT publish/subscribe, Modbus ise TCP polling/register okuma. Protokol özelindeki hata, bağlantı ve dönüşüm mantığı ayrı kalırken UI ortak veriyi okuyabilir. Yeni protokol eklenirse web ekranları doğrudan protokole bağımlı olmak zorunda kalmaz.

### Karar 3 — Merkezi kimlik/tenant kataloğu ve tenant başına ayrı operasyonel veritabanı

**Neden:** Tenant verilerini mantıksal ve veritabanı düzeyinde ayırmak; yönetim işlevlerini ortak bir noktada tutmak. Bunun karşılığı olarak DB provisioning, migration ve bağlantı yönetiminin dikkatli tasarlanması gerekir.

## 6. Bilerek yapmadıklarım ve nasıl yapardım?

Aşağıdakiler **mevcut sistemde tamamlandı** anlamına gelmez; demo ölçeğindeki sınırlar ve üretime geçiş önerileridir:

| Şu anki sınır / demo tercihi | Production yaklaşımı |
|---|---|
| Yerel ağda broker / geliştirme bağlantıları | MQTT TLS, istemci kimlik doğrulaması, topic bazlı ACL, sertifika yönetimi |
| Modbus TCP'nin ağ düzeyinde korumasının sınırlı oluşu | Ayrılmış OT ağı, VPN/secure gateway, firewall allowlist, read-only erişim |
| Ayrı ayrı VM/Docker ve platform başlatma adımları | Docker Compose/orchestrator, healthcheck, restart policy, secret/config yönetimi, sürümlü deployment |
| Mesaj ve bağlantı kesintilerinde veri kaybı riski | MQTT QoS/kalıcı oturum değerlendirmesi, local buffer/store-and-forward, retry/backoff, idempotent ingest |
| Demo ölçeğinde görüntü sınıflandırması | Daha sağlam CV modeli, hata/ışık testleri, kalibrasyon, güvenlik/gizlilik değerlendirmesi |
| Telemetri hacmi arttığında DB maliyeti | Retention politikası, partitioning/time-series stratejisi, indeks ve performans izleme |
| Protokol ve tenant özelinde manuel ilk kurulum kontrolleri | Otomatik provisioning testleri, migration kontrolü ve tekrarlanabilir altyapı kurulumu |

## 7. Yapay zekâ aracının eksik önerisini nasıl fark ettim?

**Somut geliştirme örneği — CameraWorker heartbeat ve Docker image sürümü**

Kamera aynı sınıfta (`Absent` gibi) uzun süre kaldığında merkezî platformda Offline görünme problemi yaşandı. İlk incelemede sorunun subscriber veya offline kontrol mantığından kaynaklanabileceği değerlendirildi. Bu yaklaşım tek başına yeterli değildi; gerçek MQTT trafiği ve çalışan container'ın sürümü de kontrol edilmeliydi.

**Nasıl doğruladım?**

1. Broker üzerindeki kamera topic'ini doğrudan dinleyerek mesajların gerçek geliş sıklığını gözlemledim.
2. Yerel CameraWorker kodunda aynı durum değişmese de yaklaşık 5 saniyede bir heartbeat publish edildiğini ve yerelde bunu gördüğümü doğruladım.
3. Ubuntu'da çalışan Docker image'ın yereldeki güncel publish çıktısını kullanıp kullanmadığını kontrol ettim.
4. CameraWorker'ı yeniden `publish` edip Docker image'ını tekrar oluşturdum; sonraki testte kamera durumu beklendiği gibi güncellendi.

**Ders:** Kaynak kodun doğru görünmesi, çalışan container'ın aynı sürümü kullandığını kanıtlamaz. AI önerisini veya ilk hipotezi doğrudan kabul etmek yerine **broker → publisher → çalışan Docker image → subscriber → veritabanı** zincirinde gözlenebilir veriyle ilerledim. Eski image'ın tek kök neden olduğunu sürüm diff'iyle kesin olarak kanıtladığımı iddia etmiyorum; ancak yeniden deployment sonrası davranış düzeldi.

## 8. Projenin amacı

FactorEdge, iki üretim bandını tek bir web arayüzünden izlemeyi hedefleyen bir .NET tabanlı IoT örneğidir. Birinci bant **Modbus TCP**, ikinci bant **MQTT** ile veri sağlar. İki protokolden gelen üretim verileri ortak telemetri modeline dönüştürülür; cihazların güncel durumu ve geçmiş verileri web arayüzünde gösterilir.

Bonus senaryoda gerçek bir IP kameranın MJPEG akışı Ubuntu'daki **CameraWorker** tarafından OpenCV ile analiz edilir. Merkezî sisteme ham görüntü yerine operatörün sınıflandırılmış durumu ile zaman damgası iletilir. Bu sınıflandırma bir iş güvenliği *demo göstergesi* olup tıbbi veya kesin yorgunluk tespiti değildir.

## 9. Kullanıcı, tenant ve canlı ekran

- **SuperAdmin:** platform ve tenant yönetiminden sorumlu yönetici.
- **TenantAdmin:** kendi tenant'ının kullanıcı/cihaz yönetimi ve izleme işlemleri.
- **RegularUser:** izleme odaklı kullanıcı.
- Yetkilendirme yalnızca menü/düğme gizlemekle sınırlı olmamalı; ilgili backend endpoint'lerinde de uygulanmalıdır. Demo öncesinde kritik controller/action'ların güncel policy/role kontrolleri doğrulanır.
- Dashboard, cihazlar, kamera durumları ve geçmiş ekranları tenant'a ait verileri gösterir. Arayüzün canlı güncellemelerinde SignalR kullanılır.

## 10. Demo doğrulama akışı

1. Ubuntu'da Mosquitto ve edge container'larının durumunu gösterin: `docker ps -a`.
2. Web'i açın; tenant ve yönetici oturumunu gösterin.
3. Modbus ve MQTT cihazları ekleyin; üretim değerlerini ve son veri zamanını gözlemleyin.
4. Bir simülatör container'ını durdurun; arayüzde kesinti/bağlantı bilgisini gösterin; yeniden başlatıp toparlanmayı gösterin.
5. Yönetici ve izleyici yetkileriyle aynı işlemleri deneyerek backend yetki sınırını da gösterin.
6. Kamera bonusu: `Normal`, `Fatigued`, `Absent` sınıflarını ve bağlantı kesilince `Offline` davranışını gözlemleyin. Sınıflandırmanın ortam/ışık koşullarına bağlı olduğunu belirtin.
7. Kod üzerinde MQTT topic, Modbus register dönüşümü, ortak telemetri kaydı, `DatabaseInitializer` ve tenant provisioning akışını açın.

## 11. Demo öncesi kesinleştirilecek bilgiler

Bunlar **açık doğrulama maddeleridir**; çalışan kod veya makine konfigürasyonundan teyit edilmeden README'de kesin değer olarak sunulmamalıdır:

- [ ] MQTT simülatörünün **gerçek yayın JSON'u**: `mosquitto_sub` çıktısıyla bu belgedeki örnek payload'ı değiştirin.
- [ ] Güncel `appsettings` / environment değişkeni adları, SSH provisioning script argümanları ve adım adım sıfırdan Docker build komutları.
- [ ] Tenant oluşturma servisinde tenant DB yaratımı ve migration çağrısının birebir gösterilmesi.
- [ ] Gerçek EF Core entity isimleri, DB kolon adları ve veri ilişkilerinin koddan kontrolü.
- [ ] SuperAdmin seed hesabının nasıl ayarlandığı (parola paylaşmadan) ve rollerin endpoint bazlı kontrolü.
- [ ] Kamera `Fatigued` durumunun hangi algılama eşiği/model davranışıyla oluştuğu; demoda üç durumun gerçekten gösterilebilmesi.
- [ ] Demo için son kullandığınız Ubuntu IP'si ve Modbus container host portları.

---

**İlgili çalışma dokümanları:** Edge/simülatör kod incelemesi, Edge/Ubuntu/Docker mimarisi, Web/Worker kod incelemesi ve Web/Worker mimarisi. Bu README ise case study'nin istediği kısa **kurulum + karar + demo** belgesidir.
