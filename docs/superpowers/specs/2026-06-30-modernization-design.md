# LocalAssemblyDebugger v2.0 — Modernization Design Spec

**Date:** 2026-06-30  
**Status:** Approved  
**Target:** .NET Framework 4.7.1 (zorunlu — plugin DLL'leri .NET Framework hedefliyor)

---

## 1. Hedef

LocalAssemblyDebugger'ı Dynamics 365 plugin/CodeActivity debug aracı olarak modern, kapsamlı ve kullanılabilir bir hale getirmek. Mevcut çalışan Fakes katmanına dokunmadan temiz bir mimari üzerine yeni özellikler eklemek.

---

## 2. Yaklaşım

**Hybrid refactor:** Fakes ve executor'lar korunur. `Program.cs` parçalanır. Spectre.Console baştan tasarlanır. Yeni özellikler (PreImage, UnsecureConfig, Scenarios, Logging) temiz katmanlara ayrılır.

---

## 3. Proje Yapısı

```
LocalAssemblyDebugger/
│
├── Program.cs                         # Entry point: new App().Run()
├── App.cs                             # Ana orkestratör, menü döngüsü
│
├── UI/                                # Spectre.Console ekranları
│   ├── MainMenu.cs                    # Figlet banner + ana menü
│   ├── PluginMenu.cs                  # Plugin çalıştırma akışı (7 adım)
│   ├── CodeActivityMenu.cs            # Custom Action akışı
│   ├── ScenarioMenu.cs                # Senaryo listele/yükle/sil
│   └── Prompts.cs                     # Paylaşılan input helper'ları
│
├── Features/                          # İş mantığı, UI'dan bağımsız
│   ├── PluginExecutor.cs              # (mevcut + UnsecureConfig/PreImage desteği)
│   ├── CodeActivityExecutor.cs        # (mevcut, korunur)
│   └── CrmConnector.cs                # Bağlantı yönetimi, WhoAmI, Retrieve
│
├── Fakes/                             # SDK fake implementasyonları (dokunulmaz)
│   ├── PluginExecutionContextFake.cs
│   ├── CodeActivityContextFake.cs
│   ├── ServiceProviderFake.cs
│   ├── OrganizationServiceFactoryFake.cs
│   ├── TracingServiceFake.cs
│   └── ServiceEndpointNotificationServiceFake.cs
│
├── Scenarios/                         # Senaryo sistemi
│   ├── ScenarioService.cs             # Load/Save/List/Delete JSON dosyaları
│   ├── PluginScenario.cs              # Plugin senaryo modeli
│   └── CodeActivityScenario.cs        # CodeActivity senaryo modeli
│
├── Logging/
│   └── DebugLogger.cs                 # Spectre console + dosya çift çıktı
│
├── scenarios/                         # JSON senaryo dosyaları
│   ├── .gitkeep
│   └── example_plugin.json            # Örnek senaryo
│
└── logs/                              # Çalışma logları (gitignored)
    └── .gitkeep
```

---

## 4. UI Akışı (Spectre.Console)

### Ana Menü
- Figlet banner: "LocalAssemblyDebugger"
- SelectionPrompt ile 4 seçenek:
  1. Plugin çalıştır
  2. Custom Action (CodeActivity) çalıştır
  3. Senaryo yükle
  4. Çıkış

### Plugin Akışı (7 adım)
Tüm adımlarda önceki değer/senaryo değeri default olarak gösterilir.

| Adım | Soru | UI Tipi |
|------|------|---------|
| 1/7 | DLL Yolu | TextPrompt (validation: dosya var mı?) |
| 2/7 | CRM Bağlantı Dizesi | TextPrompt (secret: true) |
| 3/7 | Entity Adı + GUID | TextPrompt x2 (GUID validation) |
| 4/7 | Mesaj Adı | SelectionPrompt (Create/Update/Delete/Diğer) |
| 5/7 | Stage / Mode / Depth | SelectionPrompt x2 + TextPrompt |
| 6/7 | PreImage / PostImage | SelectionPrompt (Boş/CRM'den al/Elle gir) |
| 7/7 | UnsecureConfig / SecureConfig | TextPrompt (reflection sonrası göster/gizle) |

### Çalıştırma Sırasında
- `LiveDisplay` ile gerçek zamanlı trace panel
- TracingServiceFake çıktıları anında yansır

### Sonuç Ekranı
- Başarı/hata durumu (renkli panel)
- Çalışma özeti tablosu (plugin adı, entity, mesaj, süre, log dosyası)
- "Bu senaryoyu kaydet?" prompt'u

### Senaryo Ekranı
- Mevcut JSON dosyalarını listele (ad, tip, son değiştirilme)
- Seç → direkt çalıştır ya da düzenle
- Sil seçeneği (onay iste)

---

## 5. Senaryo Modeli

### Plugin Senaryosu (`PluginScenario.cs`)
```json
{
  "name": "account_create_test",
  "type": "Plugin",
  "assemblyPath": "C:\\Repos\\DMR.CRM\\bin\\Debug\\DMR.CRM.Plugin.dll",
  "className": "DMR.CRM.Plugins.AccountCreatePlugin",
  "connectionString": "AuthType=OAuth;Url=https://org.crm.dynamics.com;...",
  "entityName": "account",
  "entityId": "3fa85f64-0000-0000-0000-000000000001",
  "messageName": "Create",
  "stage": 40,
  "mode": 0,
  "depth": 1,
  "retrieveEntity": true,
  "unsecureConfig": "",
  "secureConfig": "",
  "targetAttributes": {
    "name": "Test Account",
    "emailaddress1": "test@example.com"
  },
  "preImages": {
    "preImage": {
      "name": "Eski Ad"
    }
  },
  "postImages": {}
}
```

### CodeActivity Senaryosu (`CodeActivityScenario.cs`)
```json
{
  "name": "send_notification_test",
  "type": "CodeActivity",
  "assemblyPath": "C:\\Repos\\DMR.CRM\\bin\\Debug\\DMR.CRM.Workflow.dll",
  "className": "DMR.CRM.Workflows.SendNotificationAction",
  "connectionString": "AuthType=OAuth;Url=https://org.crm.dynamics.com;...",
  "entityName": "account",
  "entityId": "3fa85f64-0000-0000-0000-000000000001",
  "inputParameters": [
    { "name": "EmailAddress", "type": "string", "value": "test@example.com" },
    { "name": "SendCount",    "type": "int",    "value": "3" }
  ]
}
```

---

## 6. Yeni Özellikler

### 6.1 PreImage / PostImage Desteği
- `PluginExecutionContextFake.PreEntityImages` ve `PostEntityImages` UI'dan doldurulabilir
- Üç mod: (1) Boş bırak, (2) CRM'den entity retrieve et, (3) Elle attribute gir
- Image adı kullanıcıdan alınır (default: "preImage")
- Senaryoya kaydedilir, tekrar kullanılabilir

### 6.2 UnsecureConfig / SecureConfig
- DLL yüklendiğinde reflection ile seçilen class'ın constructor imzaları taranır
- `IPlugin` + constructor'da `(string, string)` overload varsa → iki TextPrompt göster
- Sadece parameterless varsa → config adımı atlanır, kullanıcı bilgilendirilir
- Senaryo JSON'ına kaydedilir

### 6.3 Target Entity Attribute Editörü
- `retrieveEntity = false` seçildiğinde: "Attribute eklemek ister misiniz?" sorusu
- Desteklenen tipler: `string`, `int`, `bool`, `guid`, `decimal`, `entityref` (logicalname,guid), `optionset` (int), `money` (decimal), `datetime` (yyyy-MM-dd HH:mm)
- Girilen attribute'lar hem çalıştırmada hem senaryoda kullanılır

### 6.4 Stage / Mode / Depth
- Stage: SelectionPrompt → 10 (PreValidation) / 20 (PreOperation) / 40 (PostOperation)
- Mode: SelectionPrompt → 0 (Synchronous) / 1 (Asynchronous)  
- Depth: TextPrompt, int, default 1
- `PluginExecutionContextFake` property'lerine set edilir

### 6.5 Named Scenario Profilleri
- Depolama: `scenarios/<name>.json` (Newtonsoft.Json ile serialize)
- Operasyonlar: Kaydet / Yükle / Listele / Sil
- Dosya adı kuralı: senaryo adı slug'a çevrilir (boşluk→`_`, Türkçe karakter→ASCII, küçük harf) → `account_create_test.json`
- App.config'te `AssemblyPath` varsa ilk açılışta "senaryo olarak içe aktar?" sorusu
- `ScenarioService` UI'dan tamamen bağımsız; sadece dosya I/O

### 6.6 Logging
- `DebugLogger`: Spectre output + `StreamWriter` çift çıktı
- `TracingServiceFake` trace mesajlarını `DebugLogger` üzerinden yazar
- Log dosyası: `logs/yyyy-MM-dd_HH-mm-ss_<scenarioName>.log`
- Format: header (metadata) + trace bölümü + sonuç bölümü
- `logs/` klasörü `.gitignore`'a eklenir

---

## 7. Hata Yönetimi

| Durum | Davranış |
|-------|---------|
| DLL yüklenemedi | Kırmızı Markup panel, `ex.Message` + `InnerException`, menüye dön |
| CRM bağlantısı başarısız | `LastCrmError` göster, yeniden dene seçeneği sun |
| Plugin exception | Stack trace hem console hem log; `InnerException` zinciri tam gösterilir |
| Constructor imzası eşleşmedi | Uyar, parameterless'a düş, devam et |
| GUID parse hatası | Anında validate et, geçersiz girişte tekrar sor |
| Senaryo JSON bozuk | `JsonException` yakala, hangi alan sorunlu ise göster |
| PreImage attribute CRM'de yok | Uyarı ver ama çalıştırmaya devam et |

---

## 8. Geriye Dönük Uyumluluk

- `App.config` okuma kaldırılmaz: uygulama açılışında `AssemblyPath` key'i varlık kontrolü
- Varsa tek seferlik "senaryo olarak içe aktar" önerisi → `scenarios/imported_from_appconfig.json`
- `App.config.example` güncellenir (yeni senaryo sistemi belgelenir)

---

## 9. Bağımlılıklar

Mevcut paketlere **tek eklenti**: `Spectre.Console` (.NET Framework 4.7.1 uyumlu).  
`Newtonsoft.Json` zaten `packages.config`'te mevcut — senaryo serializasyonu için kullanılır.

---

## 10. Kapsam Dışı

- .NET 8 / SDK-style proje migrasyonu (plugin DLL uyumsuzluğu riski)
- Multi-project solution
- Unit test projesi
- Headless / CI batch modu
- NuGet paketi olarak dağıtım
