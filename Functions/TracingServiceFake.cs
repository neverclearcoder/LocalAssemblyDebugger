using System;
using Microsoft.Xrm.Sdk;

namespace LocalAssemblyDebugger.Fakes
{
    public class TracingServiceFake : ITracingService
    {
        public ITracingService CrmTracingService;
        public void Trace(string format, params object[] args)
        {
            if (CrmTracingService == null)
            {
                Console.Write(format, args);
                Console.WriteLine();
            }

            else
                CrmTracingService.Trace(format, args);
        }
   }
}