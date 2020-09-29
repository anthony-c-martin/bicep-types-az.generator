// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoRest.Core;
using AutoRest.Core.Model;
using AutoRest.Core.Utilities.Collections;
using Bicep.SerializedTypes;

namespace AutoRest.AzureResourceSchema
{
    public class BicepTypesCodeGenerator : CodeGenerator
    {
        public BicepTypesCodeGenerator()
        {
        }

        public override string ImplementationFileExtension => ".json";

        public override string UsageInstructions => $"Your Bicep type definitions can be found in the specified `output-folder`.";

        public override async Task Generate(CodeModel serviceClient)
        {
            var apiVersions = serviceClient.Methods
                .SelectMany(method => method.XMsMetadata.apiVersions)
                .Concat(new [] { serviceClient.ApiVersion })
                .Where(each => each != null)
                .Distinct().ToArray();

            foreach(var version in apiVersions)
            { 
                var results = CodeModelProcessor.GenerateTypes(serviceClient, version);

                foreach (var result in results)
                {
                    var generatedTypes = result.TypeFactory.GetTypes();

                    var typesJson = TypeSerializer.Serialize(generatedTypes);
                    await Write(typesJson, Path.Combine(result.ProviderNamespace, result.ApiVersion, "types.json"), true);
                }
            }
        }
    }
}
