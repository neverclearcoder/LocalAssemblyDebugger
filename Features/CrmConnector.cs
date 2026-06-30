using System;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;

namespace LocalAssemblyDebugger.Features
{
    public static class CrmConnector
    {
        public static CrmServiceClient Connect(string connectionString)
        {
            var client = new CrmServiceClient(connectionString);
            if (!client.IsReady)
                throw new InvalidOperationException(
                    $"CRM baglantisi kurulamadi: {client.LastCrmError}");
            return client;
        }

        public static Guid GetCurrentUserId(CrmServiceClient client)
        {
            var response = (WhoAmIResponse)client.Execute(new WhoAmIRequest());
            return response.UserId;
        }

        public static Entity RetrieveEntity(CrmServiceClient client, string entityName, Guid entityId)
        {
            return client.Retrieve(entityName, entityId, new ColumnSet(true));
        }
    }
}
