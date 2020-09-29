using System.Collections.Generic;
using System.Collections.Immutable;
using Bicep.SerializedTypes.Concrete;

namespace AutoRest.AzureResourceSchema.Models
{
    public class GenerateResult
    {
        public GenerateResult(string providerNamespace, string apiVersion, TypeFactory typeFactory, IEnumerable<ResourceDescriptor> resourceDescriptors)
        {
            ProviderNamespace = providerNamespace;
            ApiVersion = apiVersion;
            TypeFactory = typeFactory;
            ResourceDescriptors = resourceDescriptors.ToImmutableArray();
        }

        public string ProviderNamespace { get; }
        public string ApiVersion { get; }
        public TypeFactory TypeFactory { get; }
        public ImmutableArray<ResourceDescriptor> ResourceDescriptors { get; }
    }
}