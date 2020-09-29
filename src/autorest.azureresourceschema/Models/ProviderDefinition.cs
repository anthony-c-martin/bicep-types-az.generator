namespace AutoRest.AzureResourceSchema.Models
{
    using System;
    using System.Collections.Generic;
    using AutoRest.Core.Model;

    public class ProviderDefinition
    {
        public string Namespace { get; set; }

        public string ApiVersion { get; set; }

        public CodeModel Model { get; set; }

        public IList<ResourceDefinition> ResourceDefinitions { get; } = new List<ResourceDefinition>();
    }
}