// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using AutoRest.Core.Model;

namespace AutoRest.AzureResourceSchema.Models
{
    public class ResourceListActionDefinition
    {
        public ResourceDescriptor Descriptor { get; set; }

        public Method DeclaringMethod { get; set; }

        public string ActionName { get; set; }
    }
}