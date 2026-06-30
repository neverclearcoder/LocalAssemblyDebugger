using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalAssemblyDebugger.Scenarios
{
    public class PluginScenario
    {
        [JsonProperty("name")]             public string Name             { get; set; } = "new_scenario";
        [JsonProperty("type")]             public string Type             { get; set; } = "Plugin";
        [JsonProperty("assemblyPath")]     public string AssemblyPath     { get; set; } = "";
        [JsonProperty("className")]        public string ClassName        { get; set; } = "";
        [JsonProperty("connectionString")] public string ConnectionString { get; set; } = "";
        [JsonProperty("entityName")]       public string EntityName       { get; set; } = "";
        [JsonProperty("entityId")]         public Guid   EntityId         { get; set; } = Guid.Empty;
        [JsonProperty("messageName")]      public string MessageName      { get; set; } = "Create";
        [JsonProperty("stage")]            public int    Stage            { get; set; } = 40;
        [JsonProperty("mode")]             public int    Mode             { get; set; } = 0;
        [JsonProperty("depth")]            public int    Depth            { get; set; } = 1;
        [JsonProperty("retrieveEntity")]   public bool   RetrieveEntity   { get; set; } = true;
        [JsonProperty("unsecureConfig")]   public string UnsecureConfig   { get; set; } = "";
        [JsonProperty("secureConfig")]     public string SecureConfig     { get; set; } = "";

        [JsonProperty("targetAttributes")]
        public List<InputParameter> TargetAttributes { get; set; } = new List<InputParameter>();

        [JsonProperty("preImages")]
        public Dictionary<string, List<InputParameter>> PreImages  { get; set; } = new Dictionary<string, List<InputParameter>>();

        [JsonProperty("postImages")]
        public Dictionary<string, List<InputParameter>> PostImages { get; set; } = new Dictionary<string, List<InputParameter>>();
    }
}
