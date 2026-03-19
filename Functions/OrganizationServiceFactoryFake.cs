using System;
using Microsoft.Xrm.Sdk;

namespace LocalAssemblyDebugger.Fakes
{
    public class OrganizationServiceFactoryFake : IOrganizationServiceFactory
    {
        public IOrganizationService Service;
        public IOrganizationService CreateOrganizationService(Guid? userId)
        {
            return Service;
        }
    }
}