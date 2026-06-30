using Newtonsoft.Json;

namespace LocalAssemblyDebugger.Scenarios
{
    public class InputParameter
    {
        [JsonProperty("name")]  public string Name  { get; set; }
        [JsonProperty("type")]  public string Type  { get; set; } = "string";
        [JsonProperty("value")] public string Value { get; set; }
    }
}
