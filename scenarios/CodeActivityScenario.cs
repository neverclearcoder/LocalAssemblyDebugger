using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalAssemblyDebugger.Scenarios
{
    public class CodeActivityScenario
    {
        [JsonProperty("name")]             public string Name             { get; set; } = "new_scenario";
        [JsonProperty("type")]             public string Type             { get; set; } = "CodeActivity";
        [JsonProperty("assemblyPath")]     public string AssemblyPath     { get; set; } = "";
        [JsonProperty("className")]        public string ClassName        { get; set; } = "";
        [JsonProperty("connectionString")] public string ConnectionString { get; set; } = "";
        [JsonProperty("entityName")]       public string EntityName       { get; set; } = "";
        [JsonProperty("entityId")]         public Guid   EntityId         { get; set; } = Guid.Empty;

        [JsonProperty("inputParameters")]
        public List<InputParameter> InputParameters { get; set; } = new List<InputParameter>();
    }
}
