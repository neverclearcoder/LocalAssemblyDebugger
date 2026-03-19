# LocalAssemblyDebugger

A console application for debugging Dynamics 365 / Power Platform Plugin and Custom Action (CodeActivity) assemblies **locally**, against a real CRM connection.

Run and debug your plugin/workflow code with Visual Studio breakpoints — no Plugin Registration Tool required.

---

## Features

- **Plugin execution** – Selects and triggers `IPlugin` implementations with real CRM data
- **Custom Action execution** – Runs `CodeActivity` (Workflow) classes with input parameters and prints output parameters to the console
- **Class selection from DLL** – Lists all eligible classes found in the loaded assembly
- **Entity retrieval from CRM** – Can automatically `Retrieve` the target entity from CRM
- **Config persistence** – Connection string and parameters are saved to `App.config` and shown as defaults on the next run
- **Fake service layer** – Dynamics SDK interfaces such as `IPluginExecutionContext`, `IOrganizationServiceFactory`, and `ITracingService` are satisfied with fake implementations

---

## Requirements

- .NET Framework 4.7.1
- Visual Studio 2019 / 2022
- An accessible Dynamics 365 / Dataverse environment

---

## Setup

```bash
git clone https://github.com/neverclearcoder/LocalAssemblyDebugger.git
cd LocalAssemblyDebugger
```

Open `LocalAssemblyDebugger.csproj` in Visual Studio and build the project. NuGet packages will be restored automatically.

Copy `App.config.example` to `App.config` and fill in your connection string.

---

## Usage

### 1. Prepare the project to debug

Build your Plugin or CodeActivity project in **Debug** mode and note the path of the generated `.dll` file.

### 2. Start LocalAssemblyDebugger

Run the application with **F5** from Visual Studio or directly as `LocalAssemblyDebugger.exe`.

```
=== LocalAssemblyDebugger ===

What would you like to run?
  1 - Plugin
  2 - Custom Action (CodeActivity)
Selection [1]:
```

### 3. Running a Plugin

```
DLL Path (IPlugin): C:\Repos\MyProject\bin\Debug\MyPlugin.dll

Found IPlugin classes:
  1 - MyProject.Plugins.AccountCreatePlugin
  2 - MyProject.Plugins.ContactUpdatePlugin

Selection [1]: 1

CRM Connection String: AuthType=OAuth;Url=https://org.crm.dynamics.com;...
Entity Name: account
Entity ID (GUID): 00000000-0000-0000-0000-000000000001
Message Name: Create
Retrieve entity from CRM? (true/false): true
```

### 4. Running a Custom Action (CodeActivity)

```
DLL Path (CodeActivity): C:\Repos\MyProject\bin\Debug\MyWorkflow.dll

Found CodeActivity classes:
  1 - MyProject.Workflows.SendNotificationAction

Selection [1]: 1

CRM Connection String: AuthType=OAuth;...
Primary Entity Name: account
Primary Entity ID (GUID, can be left empty):

Enter input parameters (leave blank to finish).
Supported types: string, int, bool, guid, decimal, entityref (format: logicalname,guid)

Parameter name (blank = done): EmailAddress
  EmailAddress type [string]: string
  EmailAddress value: test@example.com
  -> EmailAddress = test@example.com (String) added.

Parameter name (blank = done):
```

---

## Input Parameter Types

| Type | Example value |
|---|---|
| `string` | `Hello World` |
| `int` | `42` |
| `bool` | `true` |
| `guid` | `00000000-0000-0000-0000-000000000001` |
| `decimal` | `3.14` |
| `entityref` | `account,00000000-0000-0000-0000-000000000001` |

---

## CRM Connection String Examples

**OAuth (recommended):**
```
AuthType=OAuth;Url=https://orgname.crm.dynamics.com;Username=user@domain.com;Password=pass;AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;RedirectUri=app://58145B91-0C36-4500-8554-080854F2AC97;LoginPrompt=Auto
```

**Client Credentials:**
```
AuthType=ClientSecret;Url=https://orgname.crm.dynamics.com;ClientId=<AppId>;ClientSecret=<Secret>
```

---

## Project Structure

```
LocalAssemblyDebugger/
├── Program.cs                          # Entry point, user flow
├── PluginExecutor.cs                   # Wraps the IPlugin.Execute() call
├── CodeActivityExecutor.cs             # Runs CodeActivity via WorkflowInvoker
└── Functions/
    ├── PluginExecutionContextFake.cs   # IPluginExecutionContext implementation
    ├── CodeActivityContextFake.cs      # IWorkflowContext implementation
    ├── ServiceProviderFake.cs          # IServiceProvider implementation
    ├── OrganizationServiceFactoryFake.cs
    ├── TracingServiceFake.cs
    └── ServiceEndpointNotificationServiceFake.cs
```

---

## License

MIT
