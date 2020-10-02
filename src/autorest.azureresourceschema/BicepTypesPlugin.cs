// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace AutoRest.AzureResourceSchema {
    using Core;
    using Core.Extensibility;
    using Core.Model;

    public sealed class BicepTypesPlugin : Plugin<IGeneratorSettings, CodeModelTransformer<CodeModel>, BicepTypesCodeGenerator, CodeNamer, CodeModel> {
    }
}