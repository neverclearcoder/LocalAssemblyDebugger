# LocalAssemblyDebugger v2.0 — Modernization Design Spec

**Date:** 2026-06-30
**Status:** Approved
**Target:** .NET Framework 4.7.1 (required — plugin DLLs target .NET Framework)

---

## 1. Goal

Turn LocalAssemblyDebugger into a modern, comprehensive, and usable Dynamics 365 plugin/CodeActivity debugging tool. Add new features on top of a clean architecture without touching the existing, working Fakes layer.

---

## 2. Approach

**Hybrid refactor:** Fakes and executors are preserved. `Program.cs` is split apart. Spectre.Console UI is designed from scratch. New features (PreImage, UnsecureConfig, Scenarios, Logging) are split into clean layers.

---

## 3. Project Structure

```
LocalAssemblyDebugger/
│
├── Program.cs                         # Entry point: new App().Run()
├── App.cs                             # Top-level orchestrator, menu loop
│
├── UI/                                # Spectre.Console screens
│   ├── MainMenu.cs                    # Figlet banner + main menu
│   ├── PluginMenu.cs                  # Plugin execution flow (7 steps)
│   ├── CodeActivityMenu.cs            # Custom Action flow
│   ├── ScenarioMenu.cs                # List/load/delete scenarios
│   └── Prompts.cs                     # Shared input helpers
│
├── Features/                          # Business logic, independent of UI
│   ├── PluginExecutor.cs              # (existing + UnsecureConfig/PreImage support)
│   ├── CodeActivityExecutor.cs        # (existing, preserved)
│   └── CrmConnector.cs                # Connection management, WhoAmI, Retrieve
│
├── Fakes/                             # SDK fake implementations (do not touch)
│   ├── PluginExecutionContextFake.cs
│   ├── CodeActivityContextFake.cs
│   ├── ServiceProviderFake.cs
│   ├── OrganizationServiceFactoryFake.cs
│   ├── TracingServiceFake.cs
│   └── ServiceEndpointNotificationServiceFake.cs
│
├── Scenarios/                         # Scenario system
│   ├── ScenarioService.cs             # Load/Save/List/Delete JSON files
│   ├── PluginScenario.cs              # Plugin scenario model
│   └── CodeActivityScenario.cs        # CodeActivity scenario model
│
├── Logging/
│   └── DebugLogger.cs                 # Spectre console + file dual output
│
├── scenarios/                         # JSON scenario files
│   ├── .gitkeep
│   └── example_plugin.json            # Example scenario
│
└── logs/                              # Run logs (gitignored)
    └── .gitkeep
```

---

## 4. UI Flow (Spectre.Console)

### Main Menu
- Figlet banner: "LocalAssemblyDebugger"
- SelectionPrompt with 4 options:
  1. Run Plugin
  2. Run Custom Action (CodeActivity)
  3. Load Scenario
  4. Exit

### Plugin Flow (7 steps)
The previous value/scenario value is shown as the default at every step.

| Step | Question | UI Type |
|------|------|---------|
| 1/7 | DLL Path | TextPrompt (validation: does the file exist?) |
| 2/7 | CRM Connection String | TextPrompt (secret: true) |
| 3/7 | Entity Name + GUID | TextPrompt x2 (GUID validation) |
| 4/7 | Message Name | SelectionPrompt (Create/Update/Delete/Other) |
| 5/7 | Stage / Mode / Depth | SelectionPrompt x2 + TextPrompt |
| 6/7 | PreImage / PostImage | SelectionPrompt (None/Retrieve from CRM/Enter manually) |
| 7/7 | UnsecureConfig / SecureConfig | TextPrompt (shown/hidden based on reflection) |

### During Execution
- Real-time trace panel via `LiveDisplay`
- TracingServiceFake output reflected instantly

### Result Screen
- Success/error state (colored panel)
- Run summary table (plugin name, entity, message, elapsed time, log file)
- "Save this scenario?" prompt

### Scenario Screen
- List existing JSON files (name, type, last modified)
- Select → run directly or edit
- Delete option (with confirmation)

---

## 5. Scenario Model

### Plugin Scenario (`PluginScenario.cs`)
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
      "name": "Old Name"
    }
  },
  "postImages": {}
}
```

### CodeActivity Scenario (`CodeActivityScenario.cs`)
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

## 6. New Features

### 6.1 PreImage / PostImage Support
- `PluginExecutionContextFake.PreEntityImages` and `PostEntityImages` can be populated from the UI
- Three modes: (1) leave empty, (2) retrieve entity from CRM, (3) enter attributes manually
- Image name is taken from the user (default: "preImage")
- Saved to the scenario, reusable

### 6.2 UnsecureConfig / SecureConfig
- When the DLL is loaded, the selected class's constructor signatures are scanned via reflection
- If `IPlugin` has a `(string, string)` constructor overload → show two TextPrompts
- If only parameterless exists → the config step is skipped, the user is informed
- Saved to the scenario JSON

### 6.3 Target Entity Attribute Editor
- When `retrieveEntity = false` is selected: "Do you want to add attributes?" prompt
- Supported types: `string`, `int`, `bool`, `guid`, `decimal`, `entityref` (logicalname,guid), `optionset` (int), `money` (decimal), `datetime` (yyyy-MM-dd HH:mm)
- Entered attributes are used both at run time and in the scenario

### 6.4 Stage / Mode / Depth
- Stage: SelectionPrompt → 10 (PreValidation) / 20 (PreOperation) / 40 (PostOperation)
- Mode: SelectionPrompt → 0 (Synchronous) / 1 (Asynchronous)
- Depth: TextPrompt, int, default 1
- Set on `PluginExecutionContextFake` properties

### 6.5 Named Scenario Profiles
- Storage: `scenarios/<name>.json` (serialized with Newtonsoft.Json)
- Operations: Save / Load / List / Delete
- File naming rule: scenario name is slugified (space→`_`, non-ASCII→ASCII, lowercase) → `account_create_test.json`
- If `App.config` has `AssemblyPath`, prompt "import as scenario?" on first launch
- `ScenarioService` is fully independent of the UI; file I/O only

### 6.6 Logging
- `DebugLogger`: Spectre output + `StreamWriter` dual output
- `TracingServiceFake` writes trace messages through `DebugLogger`
- Log file: `logs/yyyy-MM-dd_HH-mm-ss_<scenarioName>.log`
- Format: header (metadata) + trace section + result section
- `logs/` folder is added to `.gitignore`

---

## 7. Error Handling

| Situation | Behavior |
|-------|---------|
| DLL failed to load | Red Markup panel, `ex.Message` + `InnerException`, return to menu |
| CRM connection failed | Show `LastCrmError`, offer retry option |
| Plugin exception | Stack trace shown in both console and log; full `InnerException` chain shown |
| Constructor signature mismatch | Warn, fall back to parameterless, continue |
| GUID parse error | Validate instantly, re-prompt on invalid input |
| Corrupted scenario JSON | Catch `JsonException`, show which field is problematic |
| PreImage attribute missing in CRM | Warn but continue execution |

---

## 8. Backward Compatibility

- `App.config` reading is not removed: check for the `AssemblyPath` key's existence on app startup
- If present, offer a one-time "import as scenario" prompt → `scenarios/imported_from_appconfig.json`
- `App.config.example` is updated (documents the new scenario system)

---

## 9. Dependencies

**Single addition** to existing packages: `Spectre.Console` (.NET Framework 4.7.1 compatible).
`Newtonsoft.Json` is already present in `packages.config` — used for scenario serialization.

---

## 10. Out of Scope

- .NET 8 / SDK-style project migration (plugin DLL compatibility risk)
- Multi-project solution
- Unit test project
- Headless / CI batch mode
- Distribution as a NuGet package
