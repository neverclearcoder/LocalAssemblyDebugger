using System;
using Microsoft.Xrm.Sdk;

namespace LocalAssemblyDebugger.Fakes
{
    public class ServiceEndpointNotificationServiceFake : IServiceEndpointNotificationService
    {
        public IServiceEndpointNotificationService CrmServiceEndpointNotificationService;
        public string Execute(EntityReference serviceEndpoint, IExecutionContext context)
        {
            if (CrmServiceEndpointNotificationService != null)
                return CrmServiceEndpointNotificationService.Execute(serviceEndpoint, context);
            else
                throw new NotImplementedException();
        }
    }
}