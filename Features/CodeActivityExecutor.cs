using System.Activities;
using System.Collections.Generic;
using LocalAssemblyDebugger.Fakes;
using LocalAssemblyDebugger.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;

namespace LocalAssemblyDebugger.Features
{
    public static class CodeActivityExecutor
    {
        public static IDictionary<string, object> Execute(
            CodeActivity workflow,
            IOrganizationService service,
            CodeActivityContextFake context,
            Dictionary<string, object> inputs,
            DebugLogger logger)
        {
            var tracingService = new TracingServiceFake { LogAction = logger.WriteTrace };

            var invoker = new WorkflowInvoker(workflow);
            invoker.Extensions.Add(() => service);
            invoker.Extensions.Add<IWorkflowContext>(() => context);
            invoker.Extensions.Add<ITracingService>(() => tracingService);
            invoker.Extensions.Add<IOrganizationServiceFactory>(() =>
                new OrganizationServiceFactoryFake { Service = service });
            invoker.Extensions.Add<IServiceEndpointNotificationService>(() =>
                new ServiceEndpointNotificationServiceFake());

            return invoker.Invoke(inputs);
        }
    }
}
