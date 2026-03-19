# LocalAssemblyDebugger

Dynamics 365 / Power Platform Plugin ve Custom Action (CodeActivity) assembly'lerini **yerel ortamda**, gerçek bir CRM bağlantısıyla doğrudan debug etmeye yarayan konsol uygulaması.

Plugin Registration Tool'a ihtiyaç duymadan, kendi makinenizdeki Visual Studio debugger'ı ile breakpoint koyarak plugin/workflow kodunuzu çalıştırmanızı sağlar.

---

## Özellikler

- **Plugin çalıştırma** – `IPlugin` implementasyonlarını seçip gerçek CRM verisiyle tetikler
- **Custom Action çalıştırma** – `CodeActivity` (Workflow) sınıflarını input parametreleriyle çalıştırır, output parametrelerini konsola yazdırır
- **DLL içinden sınıf seçimi** – Yüklenen assembly içindeki tüm uygun sınıfları listeler
- **CRM'den entity çekme** – Hedef entity'yi CRM'den otomatik olarak `Retrieve` edebilir
- **Config kalıcılığı** – Girilen bağlantı dizesi ve parametreler `App.config`'e kaydedilir; sonraki çalıştırmada varsayılan olarak gösterilir
- **Fake servis katmanı** – `IPluginExecutionContext`, `IOrganizationServiceFactory`, `ITracingService` gibi Dynamics SDK arayüzleri sahte (fake) implementasyonlarla karşılanır

---

## Gereksinimler

- .NET Framework 4.7.1
- Visual Studio 2019 / 2022
- Erişilebilir bir Dynamics 365 / Dataverse ortamı

---

## Kurulum

```bash
git clone https://github.com/neverclearcoder/LocalAssemblyDebugger.git
cd LocalAssemblyDebugger
```

Visual Studio'da `LocalAssemblyDebugger.csproj` dosyasını açın ve projeyi derleyin. NuGet paketleri otomatik geri yüklenecektir.

---

## Kullanım

### 1. Debug edilecek projeyi hazırlayın

Debug etmek istediğiniz Plugin veya CodeActivity projesini **Debug** modunda derleyin ve üretilen `.dll` dosyasının yolunu not edin.

### 2. LocalAssemblyDebugger'ı başlatın

Uygulamayı Visual Studio'dan **F5** ile veya doğrudan `LocalAssemblyDebugger.exe` olarak çalıştırın.

```
=== LocalAssemblyDebugger ===

Ne çalıştırmak istiyorsunuz?
  1 - Plugin
  2 - Custom Action (CodeActivity)
Seçim [1]:
```

### 3. Plugin çalıştırma

```
DLL Yolu (IPlugin): C:\Repos\MyProject\bin\Debug\MyPlugin.dll

Bulunan IPlugin sınıfları:
  1 - MyProject.Plugins.AccountCreatePlugin
  2 - MyProject.Plugins.ContactUpdatePlugin

Seçim [1]: 1

CRM Bağlantı Dizesi: AuthType=OAuth;Url=https://org.crm.dynamics.com;...
Entity Adı: account
Entity ID (GUID): 00000000-0000-0000-0000-000000000001
Mesaj Adı: Create
Entity CRM'den çekilsin mi? (true/false): true
```

### 4. Custom Action (CodeActivity) çalıştırma

```
DLL Yolu (CodeActivity): C:\Repos\MyProject\bin\Debug\MyWorkflow.dll

Bulunan CodeActivity sınıfları:
  1 - MyProject.Workflows.SendNotificationAction

Seçim [1]: 1

CRM Bağlantı Dizesi: AuthType=OAuth;...
Primary Entity Adı: account
Primary Entity ID (GUID, boş bırakılabilir):

Giriş parametrelerini girin (boş bırakarak bitirin).
Desteklenen tipler: string, int, bool, guid, decimal, entityref (format: logicalname,guid)

Parametre adı (boş = bitir): EmailAddress
  EmailAddress tipi [string]: string
  EmailAddress değeri: test@example.com
  -> EmailAddress = test@example.com (String) eklendi.

Parametre adı (boş = bitir):
```

---

## Input Parametre Tipleri

| Tip | Örnek değer |
|---|---|
| `string` | `Merhaba Dünya` |
| `int` | `42` |
| `bool` | `true` |
| `guid` | `00000000-0000-0000-0000-000000000001` |
| `decimal` | `3.14` |
| `entityref` | `account,00000000-0000-0000-0000-000000000001` |

---

## CRM Bağlantı Dizesi Örnekleri

**OAuth (önerilen):**
```
AuthType=OAuth;Url=https://orgname.crm.dynamics.com;Username=user@domain.com;Password=pass;AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;RedirectUri=app://58145B91-0C36-4500-8554-080854F2AC97;LoginPrompt=Auto
```

**Client Credentials:**
```
AuthType=ClientSecret;Url=https://orgname.crm.dynamics.com;ClientId=<AppId>;ClientSecret=<Secret>
```

---

## Proje Yapısı

```
LocalAssemblyDebugger/
├── Program.cs                          # Giriş noktası, kullanıcı akışı
├── PluginExecutor.cs                   # IPlugin.Execute() çağrısını sarar
├── CodeActivityExecutor.cs             # WorkflowInvoker ile CodeActivity çalıştırır
└── Functions/
    ├── PluginExecutionContextFake.cs   # IPluginExecutionContext implementasyonu
    ├── CodeActivityContextFake.cs      # IWorkflowContext implementasyonu
    ├── ServiceProviderFake.cs          # IServiceProvider implementasyonu
    ├── OrganizationServiceFactoryFake.cs
    ├── TracingServiceFake.cs
    └── ServiceEndpointNotificationServiceFake.cs
```

---

## Lisans

MIT
