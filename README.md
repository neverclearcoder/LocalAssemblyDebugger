# LocalAssemblyDebugger

A console application for debugging Dynamics 365 / Power Platform Plugin and Custom Action (CodeActivity) assemblies **locally**, against a real CRM connection.

Run and debug your plugin/workflow code with Visual Studio breakpoints - no Plugin Registration Tool required.

---

## Features

- **Interactive Spectre.Console UI** – Figlet banner, selection menus, status spinners, and result tables instead of raw `Console.ReadLine` prompts
- **Plugin execution** – Selects and triggers `IPlugin` implementations with real CRM data, including Stage / Mode / Depth and Unsecure/Secure config
- **Custom Action execution** – Runs `CodeActivity` (Workflow) classes via `WorkflowInvoker` with typed input parameters and printed output parameters
- **Class selection from DLL** – Lists all eligible classes found in the loaded assembly
- **PreImage / PostImage support** – Define image attributes by hand, or retrieve the real entity from CRM with a single confirmation
- **Scenario profiles** – Save any plugin/CodeActivity run as a named JSON scenario under `scenarios/` and re-run it later from the main menu
- **Legacy App.config import** – One-time migration prompt imports old `App.config`-based settings into a scenario
- **Dual logging** – Every run writes a timestamped log under `logs/` in addition to the live console output
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

> Connection strings and other inputs are no longer stored in `App.config`. Enter them interactively and choose "save as scenario" to persist them as JSON under `scenarios/`. If you have an old `App.config` with saved settings, the app offers a one-time import into a scenario on startup.

---

## Usage

### 1. Prepare the project to debug

Build your Plugin or CodeActivity project in **Debug** mode and note the path of the generated `.dll` file.

### 2. Start LocalAssemblyDebugger

Run the application with **F5** from Visual Studio or directly as `LocalAssemblyDebugger.exe`. You'll land on the main menu:

```
 _                    _ ____       _
| |    ___   ___ __ _| |  _ \  ___| |__  _   _  __ _  __ _  ___ _ __
| |   / _ \ / __/ _` | | | | |/ _ \ '_ \| | | |/ _` |/ _` |/ _ \ '__|
| |__| (_) | (_| (_| | | |_| |  __/ |_) | |_| | (_| | (_| |  __/ |
|_____\___/ \___\__,_|_|____/ \___|_.__/ \__,_|\__, |\__, |\___|_|
                                                |___/ |___/

> Plugin Calistir
  Custom Action (CodeActivity) Calistir
  Senaryo Yukle
  Cikis
```

### 3. Running a Plugin

The Plugin flow walks through: assembly path → class selection (via `SelectionPrompt`) → connection string → entity/message/stage/mode/depth → target attributes → PreImage/PostImage configuration → optional scenario save → execute. Output parameters and timing are shown in a table and written to the log.

### 4. Running a Custom Action (CodeActivity)

The CodeActivity flow walks through: assembly path → class selection → connection string → optional context entity → typed input parameters → optional scenario save → execute via `WorkflowInvoker`.

### 5. Loading a saved scenario

Choose **Senaryo Yukle** from the main menu to list, run, or delete previously saved JSON scenarios (see `scenarios/example_plugin.json` and `scenarios/example_ca.json`).

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
| `optionset` | `1` |

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
├── Program.cs                          # Minimal entry point -> App.Run()
├── App.cs                              # Top-level orchestration, main loop, legacy import offer
├── Features/
│   ├── CrmConnector.cs                 # CRM connect / current user / retrieve entity
│   ├── PluginExecutor.cs               # Plugin instantiation, attribute mapping, IPlugin.Execute()
│   └── CodeActivityExecutor.cs         # Runs CodeActivity via WorkflowInvoker
├── Fakes/
│   ├── PluginExecutionContextFake.cs   # IPluginExecutionContext implementation
│   ├── CodeActivityContextFake.cs      # IWorkflowContext implementation
│   ├── ServiceProviderFake.cs          # IServiceProvider implementation
│   ├── OrganizationServiceFactoryFake.cs
│   ├── TracingServiceFake.cs           # Forwards Trace() to DebugLogger via LogAction
│   └── ServiceEndpointNotificationServiceFake.cs
├── Scenarios/
│   ├── PluginScenario.cs               # Plugin run configuration model
│   ├── CodeActivityScenario.cs         # CodeActivity run configuration model
│   ├── InputParameter.cs               # Typed name/type/value triple
│   └── ScenarioService.cs              # JSON persistence, slugified naming, legacy App.config import
├── UI/
│   ├── MainMenu.cs                     # Figlet banner + top-level SelectionPrompt
│   ├── PluginMenu.cs                   # 7-step interactive plugin execution flow
│   ├── CodeActivityMenu.cs             # 5-step interactive CodeActivity execution flow
│   ├── ScenarioMenu.cs                 # Scenario list/run/delete
│   └── Prompts.cs                      # Shared Spectre.Console input helpers
├── Logging/
│   └── DebugLogger.cs                  # Dual console (AnsiConsole) + file logging
└── scenarios/                          # Saved JSON scenarios (gitignored, example_*.json checked in)
```

---

## License

Copyright © 2025 Halil Bora Ocak ([@neverclearcoder](https://github.com/neverclearcoder)). All rights reserved.

This software is proprietary and confidential. It is not licensed for any use - public, private, commercial, or otherwise - without explicit written permission from the author.

- Viewing this repository does not constitute permission to use the code.
- No open-source license is granted.
- Forking, cloning for use, or deploying this software without authorization is prohibited.
