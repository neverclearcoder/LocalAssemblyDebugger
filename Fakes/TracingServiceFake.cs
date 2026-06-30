using System;
using Microsoft.Xrm.Sdk;

namespace LocalAssemblyDebugger.Fakes
{
    public class TracingServiceFake : ITracingService
    {
        public ITracingService CrmTracingService;
        public Action<string> LogAction;

        public void Trace(string format, params object[] args)
        {
            string message = args.Length == 0 ? format : string.Format(format, args);
            if (LogAction != null)
                LogAction(message);
            else
                Console.WriteLine(message);
            CrmTracingService?.Trace(format, args);
        }
    }
}