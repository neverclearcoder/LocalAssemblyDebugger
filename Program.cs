using System;
using System.Activities;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Xml;
using LocalAssemblyDebugger.Fakes;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;

namespace LocalAssemblyDebugger
{
    class Program
    {
        static void Main()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            Console.WriteLine("=== LocalAssemblyDebugger ===");
            Console.WriteLine();
            Console.WriteLine("Ne çalıştırmak istiyorsunuz?");
            Console.WriteLine("  1 - Plugin");
            Console.WriteLine("  2 - Custom Action (CodeActivity)");
            Console.Write("Seçim [1]: ");

            string choice = Console.ReadLine()?.Trim();
            Console.WriteLine();

            if (choice == "2")
                RunCustomAction();
            else
                RunPlugin();
        }

        // ================================================================
        // ORTAK: DLL YÜKLE + SINIF SEÇ
        // ================================================================
        static Type SelectTypeFromDll(string baseTypeName, Type baseType)
        {
            string dllPath = AskOrKeep($"DLL Yolu ({baseTypeName})", "AssemblyPath");
            if (string.IsNullOrWhiteSpace(dllPath))
            {
                Console.WriteLine("HATA: DLL yolu boş olamaz.");
                return null;
            }

            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(dllPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA: DLL yüklenemedi. {ex.Message}");
                return null;
            }

            List<Type> types = assembly.GetTypes()
                .Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t))
                .OrderBy(t => t.FullName)
                .ToList();

            if (types.Count == 0)
            {
                Console.WriteLine($"HATA: DLL içinde {baseTypeName} implementasyonu bulunamadı.");
                return null;
            }

            Console.WriteLine($"\nBulunan {baseTypeName} sınıfları:");
            for (int i = 0; i < types.Count; i++)
                Console.WriteLine($"  {i + 1} - {types[i].FullName}");

            Console.Write($"\nSeçim [1]: ");
            string input = Console.ReadLine()?.Trim();
            int index = 1;
            if (!string.IsNullOrWhiteSpace(input))
                int.TryParse(input, out index);

            if (index < 1 || index > types.Count)
            {
                Console.WriteLine("Geçersiz seçim.");
                return null;
            }

            return types[index - 1];
        }

        // ================================================================
        // PLUGIN
        // ================================================================
        static void RunPlugin()
        {
            Type selectedType = SelectTypeFromDll("IPlugin", typeof(IPlugin));
            if (selectedType == null) { Console.ReadLine(); return; }

            Console.WriteLine($"\nSeçilen: {selectedType.FullName}");
            Console.WriteLine();

            string connectionString = AskOrKeep("CRM Bağlantı Dizesi", "CrmConnection", isConnectionString: true);
            string entityName       = AskOrKeep("Entity Adı",           "TargetEntityName");
            string entityIdStr      = AskOrKeep("Entity ID (GUID)",     "TargetEntityId");
            string messageName      = AskOrKeep("Mesaj Adı",            "MessageName");
            string retrieveStr      = AskOrKeep("Entity CRM'den çekilsin mi? (true/false)", "RetrieveEntity");
            Console.WriteLine();

            if (!Guid.TryParse(entityIdStr, out Guid entityId))
            {
                Console.WriteLine($"HATA: Geçerli bir GUID değil: {entityIdStr}");
                Console.ReadLine();
                return;
            }

            bool retrieveEntity = bool.TryParse(retrieveStr, out bool r) && r;

            try
            {
                Console.WriteLine("CRM'e bağlanılıyor...");
                using (CrmServiceClient serviceClient = new CrmServiceClient(connectionString))
                {
                    if (!serviceClient.IsReady)
                    {
                        Console.WriteLine($"HATA: CRM bağlantısı kurulamadı. {serviceClient.LastCrmError}");
                        Console.ReadLine();
                        return;
                    }

                    Console.WriteLine("CRM bağlantısı kuruldu.");

                    WhoAmIResponse whoAmI = (WhoAmIResponse)serviceClient.Execute(new WhoAmIRequest());

                    Entity target = retrieveEntity
                        ? serviceClient.Retrieve(entityName, entityId, new ColumnSet(true))
                        : new Entity(entityName, entityId);

                    Console.WriteLine($"Entity: {entityName} | Id: {entityId} | Mesaj: {messageName}");

                    PluginExecutionContextFake context = new PluginExecutionContextFake
                    {
                        PrimaryEntityId   = target.Id,
                        PrimaryEntityName = target.LogicalName,
                        UserId            = whoAmI.UserId,
                        MessageName       = messageName
                    };
                    context.InputParameters.Add("Target", target);

                    IPlugin plugin = (IPlugin)Activator.CreateInstance(selectedType);
                    PluginExecutor.Execute(plugin, serviceClient, context);

                    Console.WriteLine("Plugin başarıyla tamamlandı.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }

            Console.ReadLine();
        }

        // ================================================================
        // CUSTOM ACTION (CodeActivity)
        // ================================================================
        static void RunCustomAction()
        {
            Type selectedType = SelectTypeFromDll("CodeActivity", typeof(CodeActivity));
            if (selectedType == null) { Console.ReadLine(); return; }

            Console.WriteLine($"\nSeçilen: {selectedType.FullName}");
            Console.WriteLine();

            string connectionString = AskOrKeep("CRM Bağlantı Dizesi", "CrmConnection", isConnectionString: true);
            string entityName       = AskOrKeep("Primary Entity Adı",  "CA_EntityName");
            string entityIdStr      = AskOrKeep("Primary Entity ID (GUID, boş bırakılabilir)", "CA_EntityId");
            Console.WriteLine();

            Guid entityId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(entityIdStr) && !Guid.TryParse(entityIdStr, out entityId))
            {
                Console.WriteLine($"HATA: Geçerli bir GUID değil: {entityIdStr}");
                Console.ReadLine();
                return;
            }

            Dictionary<string, object> inputs = AskInputParameters();
            Console.WriteLine();

            try
            {
                Console.WriteLine("CRM'e bağlanılıyor...");
                using (CrmServiceClient serviceClient = new CrmServiceClient(connectionString))
                {
                    if (!serviceClient.IsReady)
                    {
                        Console.WriteLine($"HATA: CRM bağlantısı kurulamadı. {serviceClient.LastCrmError}");
                        Console.ReadLine();
                        return;
                    }

                    Console.WriteLine("CRM bağlantısı kuruldu.");

                    WhoAmIResponse whoAmI = (WhoAmIResponse)serviceClient.Execute(new WhoAmIRequest());

                    CodeActivityContextFake context = new CodeActivityContextFake
                    {
                        PrimaryEntityName = entityName,
                        PrimaryEntityId   = entityId,
                        UserId            = whoAmI.UserId,
                        MessageName       = "Execute"
                    };

                    CodeActivity customAction = (CodeActivity)Activator.CreateInstance(selectedType);
                    IDictionary<string, object> outputs = CodeActivityExecutor.Execute(
                        customAction, serviceClient, context, inputs);

                    PrintOutputs(outputs);
                    Console.WriteLine("Custom Action başarıyla tamamlandı.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }

            Console.ReadLine();
        }

        // ================================================================
        // YARDIMCILAR
        // ================================================================
        static Dictionary<string, object> AskInputParameters()
        {
            var inputs = new Dictionary<string, object>();
            Console.WriteLine("Giriş parametrelerini girin (boş bırakarak bitirin).");
            Console.WriteLine("Desteklenen tipler: string, int, bool, guid, decimal, entityref (format: logicalname,guid)");
            Console.WriteLine();

            while (true)
            {
                Console.Write("Parametre adı (boş = bitir): ");
                string name = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(name)) break;

                Console.Write($"  {name} tipi [string]: ");
                string type = Console.ReadLine()?.Trim().ToLower();
                if (string.IsNullOrWhiteSpace(type)) type = "string";

                Console.Write($"  {name} değeri: ");
                string raw = Console.ReadLine()?.Trim();

                try
                {
                    object value = ConvertInput(type, raw);
                    inputs[name] = value;
                    Console.WriteLine($"  -> {name} = {value} ({value?.GetType().Name}) eklendi.");
                }
                catch
                {
                    Console.WriteLine($"  [Uyarı] '{raw}' değeri '{type}' tipine çevrilemedi, string olarak eklendi.");
                    inputs[name] = raw;
                }

                Console.WriteLine();
            }

            return inputs;
        }

        static object ConvertInput(string type, string raw)
        {
            switch (type)
            {
                case "int":     return int.Parse(raw);
                case "bool":    return bool.Parse(raw);
                case "guid":    return Guid.Parse(raw);
                case "decimal": return decimal.Parse(raw);
                case "entityref":
                    var parts = raw.Split(',');
                    return new EntityReference(parts[0].Trim(), Guid.Parse(parts[1].Trim()));
                default:        return raw;
            }
        }

        static void PrintOutputs(IDictionary<string, object> outputs)
        {
            if (outputs == null || outputs.Count == 0)
            {
                Console.WriteLine("Custom Action çıktı parametresi döndürmedi.");
                return;
            }

            Console.WriteLine("--- Çıktı Parametreleri ---");
            foreach (var kv in outputs)
                Console.WriteLine($"  {kv.Key} = {kv.Value}");
        }

        static string AskOrKeep(string label, string key, bool isConnectionString = false)
        {
            string current = isConnectionString
                ? ConfigurationManager.ConnectionStrings[key]?.ConnectionString
                : ConfigurationManager.AppSettings[key];

            Console.Write(label);
            if (!string.IsNullOrWhiteSpace(current))
                Console.Write($" [{current}]");
            Console.Write(": ");

            string input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
                return current ?? string.Empty;

            SaveToConfig(key, input, isConnectionString);
            return input;
        }

        static void SaveToConfig(string key, string value, bool isConnectionString)
        {
            try
            {
                string configPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                XmlDocument doc = new XmlDocument();
                doc.Load(configPath);

                if (isConnectionString)
                {
                    XmlNode node = doc.SelectSingleNode($"//connectionStrings/add[@name='{key}']");
                    if (node != null)
                        node.Attributes["connectionString"].Value = value;
                    else
                    {
                        XmlNode section = doc.SelectSingleNode("//connectionStrings")
                            ?? CreateSection(doc, "connectionStrings");
                        XmlElement el = doc.CreateElement("add");
                        el.SetAttribute("name", key);
                        el.SetAttribute("connectionString", value);
                        section.AppendChild(el);
                    }
                }
                else
                {
                    XmlNode node = doc.SelectSingleNode($"//appSettings/add[@key='{key}']");
                    if (node != null)
                        node.Attributes["value"].Value = value;
                    else
                    {
                        XmlNode section = doc.SelectSingleNode("//appSettings")
                            ?? CreateSection(doc, "appSettings");
                        XmlElement el = doc.CreateElement("add");
                        el.SetAttribute("key", key);
                        el.SetAttribute("value", value);
                        section.AppendChild(el);
                    }
                }

                doc.Save(configPath);
                ConfigurationManager.RefreshSection(isConnectionString ? "connectionStrings" : "appSettings");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Uyarı] Config kaydedilemedi: {ex.Message}");
            }
        }

        static XmlNode CreateSection(XmlDocument doc, string sectionName)
        {
            XmlElement section = doc.CreateElement(sectionName);
            doc.DocumentElement.AppendChild(section);
            return section;
        }
    }
}
