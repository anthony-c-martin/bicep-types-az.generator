﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using AutoRest.Core.Model;
using Bicep.SerializedTypes.Concrete;

namespace AutoRest.AzureResourceSchema.Models
{
    public class ResourceDefinition
    {
        public ResourceDescriptor Descriptor { get; set; }

        public Method DeclaringMethod { get; set; }

        public Method GetMethod { get; set; }

        public ResourceType Type { get; set; }
    }
}