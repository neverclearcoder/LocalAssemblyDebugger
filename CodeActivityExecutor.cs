using System.Activities;
using System.Collections.Generic;
using LocalAssemblyDebugger.Fakes;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;

namespace LocalAssemblyDebugger
{
    public static class CodeActivityExecutor
    {
        public static IDictionary<string, object> Execute(
            CodeActivity workflow,
            IOrganizationService service,
            CodeActivityContextFake context,
            Dictionary<string, object> inputs,
            ITracingService tracingService = null,
            IServiceEndpointNotificationService notificationService = null)
        {
            WorkflowInvoker invoker = new WorkflowInvoker(workflow);
            invoker.Extensions.Add(() => service);
            invoker.Extensions.Add<IWorkflowContext>(() => context);

            invoker.Extensions.Add<ITracingService>(() =>
                tracingService ?? new TracingServiceFake());

            invoker.Extensions.Add<IOrganizationServiceFactory>(() =>
                new OrganizationServiceFactoryFake { Service = service });

            invoker.Extensions.Add<IServiceEndpointNotificationService>(() =>
                notificationService ?? new ServiceEndpointNotificationServiceFake());

            return invoker.Invoke(inputs);
        }
    }
}