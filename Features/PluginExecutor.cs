using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using LocalAssemblyDebugger.Fakes;
using LocalAssemblyDebugger.Logging;
using LocalAssemblyDebugger.Scenarios;
using Microsoft.Xrm.Sdk;

namespace LocalAssemblyDebugger.Features
{
    public static class PluginExecutor
    {
        public static bool HasTwoArgConstructor(Type pluginType)
        {
            return pluginType.GetConstructor(new[] { typeof(string), typeof(string) }) != null;
        }

        public static IPlugin CreateInstance(Type pluginType, string unsecureConfig, string secureConfig)
        {
            var twoArg = pluginType.GetConstructor(new[] { typeof(string), typeof(string) });
            if (twoArg != null)
                return (IPlugin)twoArg.Invoke(new object[] { unsecureConfig ?? "", secureConfig ?? "" });

            var noArg = pluginType.GetConstructor(Type.EmptyTypes);
            if (noArg != null)
                return (IPlugin)noArg.Invoke(null);

            throw new InvalidOperationException(
                $"{pluginType.FullName} icin uygun constructor bulunamadi " +
                "(parameterless veya (string,string) gerekli).");
        }

        public static void Execute(
            IPlugin plugin,
            IOrganizationService service,
            PluginExecutionContextFake context,
            Dictionary<string, List<InputParameter>> preImages,
            Dictionary<string, List<InputParameter>> postImages,
            DebugLogger logger)
        {
            if (preImages != null)
                foreach (var kv in preImages)
                {
                    var img = new Entity(context.PrimaryEntityName);
                    ApplyAttributes(img, kv.Value);
                    context.PreEntityImages.Add(kv.Key, img);
                }

            if (postImages != null)
                foreach (var kv in postImages)
                {
                    var img = new Entity(context.PrimaryEntityName);
                    ApplyAttributes(img, kv.Value);
                    context.PostEntityImages.Add(kv.Key, img);
                }

            var tracingService = new TracingServiceFake { LogAction = logger.WriteTrace };
            var factory        = new OrganizationServiceFactoryFake { Service = service };

            var serviceProvider = new ServiceProviderFake
            {
                MyPluginExecutionContext                  = context,
                MyTracingServiceFake                      = tracingService,
                MyOrganizationServiceFactory              = factory,
                MyServiceEndpointNotificationServiceFake  = new ServiceEndpointNotificationServiceFake()
            };

            plugin.Execute(serviceProvider);
        }

        public static void ApplyAttributes(Entity entity, List<InputParameter> parameters)
        {
            if (parameters == null) return;
            foreach (var p in parameters)
            {
                if (string.IsNullOrWhiteSpace(p.Name)) continue;
                entity[p.Name] = ParseAttributeValue(p.Type, p.Value);
            }
        }

        public static object ParseAttributeValue(string type, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            switch ((type ?? "string").ToLowerInvariant())
            {
                case "int":       return int.Parse(value);
                case "bool":      return bool.Parse(value);
                case "guid":      return Guid.Parse(value);
                case "decimal":   return decimal.Parse(value, CultureInfo.InvariantCulture);
                case "money":     return new Money(decimal.Parse(value, CultureInfo.InvariantCulture));
                case "optionset": return new OptionSetValue(int.Parse(value));
                case "datetime":  return DateTime.Parse(value, CultureInfo.InvariantCulture);
                case "entityref":
                    var parts = value.Split(',');
                    if (parts.Length < 2)
                        throw new FormatException($"entityref formati: 'logicalname,guid' bekleniyor, alindi: '{value}'");
                    return new EntityReference(parts[0].Trim(), Guid.Parse(parts[1].Trim()));
                default:          return value;
            }
        }
    }
}
