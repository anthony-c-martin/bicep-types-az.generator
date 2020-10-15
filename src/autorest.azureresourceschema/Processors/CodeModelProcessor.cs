// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using AutoRest.Core.Model;
using AutoRest.Core.Utilities;
using System.Text.RegularExpressions;
using AutoRest.AzureResourceSchema.Models;
using AutoRest.Core.Logging;
using Bicep.SerializedTypes.Concrete;

namespace AutoRest.AzureResourceSchema.Processors
{
    public static class CodeModelProcessor
    {
        public static void LogMessage(string message)
            => Logger.Instance.Log(new LogMessage(Category.Information, message));

        public static void LogWarning(string message)
            => Logger.Instance.Log(new LogMessage(Category.Warning, message));

        public static void LogError(string message)
            => Logger.Instance.Log(new LogMessage(Category.Error, message));

        public static readonly Regex parentScopePrefix = new Regex("^.*/providers/", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
        private static readonly Regex managementGroupPrefix = new Regex("^/providers/Microsoft.Management/managementGroups/{\\w+}/$", RegexOptions.IgnoreCase);
        private static readonly Regex tenantPrefix = new Regex("^/$", RegexOptions.IgnoreCase);
        private static readonly Regex subscriptionPrefix = new Regex("^/subscriptions/{\\w+}/$", RegexOptions.IgnoreCase);
        private static readonly Regex resourceGroupPrefix = new Regex("^/subscriptions/{\\w+}/resourceGroups/{\\w+}/$", RegexOptions.IgnoreCase);

        private static bool ShouldProcessResourceType(CodeModel codeModel, Method method, string apiVersion)
        {
            if (method.HttpMethod != HttpMethod.Put)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(method.Url))
            {
                return false;
            }

            return Array.Exists(method.XMsMetadata.apiVersions, v => v.Equals(apiVersion));
        }

        public static bool ShouldProcessResourceAction(CodeModel codeModel, Method method, string apiVersion)
        {
            if (method.HttpMethod != HttpMethod.Post)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(method.Url))
            {
                return false;
            }

            var actionName = method.Url.Split('/').Last();
            if (!actionName.StartsWith("list", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Array.Exists(method.XMsMetadata.apiVersions, v => v.Equals(apiVersion));
        }

        private static (bool success, string failureReason, ScopeType scopeType, string routingScope) ParseResourceScopes(Method method, string apiVersion)
        {
            var finalProvidersMatch = parentScopePrefix.Match(method.Url);
            if (!finalProvidersMatch.Success)
            {
                return (false, "Unable to locate '/providers/' segment", ScopeType.Unknown, string.Empty);
            }

            var parentScope = method.Url.Substring(0, finalProvidersMatch.Length - "providers/".Length);
            var routingScope = method.Url.Substring(finalProvidersMatch.Length).Trim('/');

            var scopeType = ScopeType.Unknown;
            if (tenantPrefix.IsMatch(parentScope))
            {
                scopeType = ScopeType.Tenant;
            }
            else if (managementGroupPrefix.IsMatch(parentScope))
            {
                scopeType = ScopeType.ManagementGroup;
            }
            else if (resourceGroupPrefix.IsMatch(parentScope))
            {
                scopeType = ScopeType.ResourceGroup;
            }
            else if (subscriptionPrefix.IsMatch(parentScope))
            {
                scopeType = ScopeType.Subcription;
            }
            else if (parentScopePrefix.IsMatch(parentScope))
            {
                scopeType = ScopeType.Extension;
            }

            return (true, string.Empty, scopeType, routingScope);
        }

        private static (bool success, string failureReason, IEnumerable<ResourceDescriptor> resourceDescriptors) ParseResourceDescriptors(Method method, string apiVersion, ScopeType scopeType, string routingScope)
        {
            var providerNamespace = routingScope.Substring(0, routingScope.IndexOf('/'));
            if (IsPathVariable(providerNamespace))
            {
                return (false, $"Unable to process parameterized provider namespace '{providerNamespace}'", Enumerable.Empty<ResourceDescriptor>());
            }

            var (success, failureReason, resourceTypesFound) = ParseResourceTypes(method, routingScope);
            if (!success)
            {
                return (false, failureReason, Enumerable.Empty<ResourceDescriptor>());
            }

            var resNameParam = routingScope.Substring(routingScope.LastIndexOf('/') + 1);
            var hasVariableName = IsPathVariable(resNameParam);

            return (true, string.Empty, resourceTypesFound.Select(type => new ResourceDescriptor
            {
                ScopeType = scopeType,
                ProviderNamespace = providerNamespace,
                ResourceTypeSegments = type.ToList(),
                ApiVersion = apiVersion,
                HasVariableName = hasVariableName,
                XmsMetadata = method.XMsMetadata,
            }));
        }

        private static (bool success, string failureReason, IEnumerable<ResourceDescriptor> resourceDescriptors) ParseResourceMethod(Method method, string apiVersion)
        {
            var (parseScopeSuccess, parseScopeFailureReason, scopeType, routingScope) = ParseResourceScopes(method, apiVersion);

            return ParseResourceDescriptors(method, apiVersion, scopeType, routingScope);
        }
        
        private static (bool success, string failureReason, IEnumerable<ResourceDescriptor> resourceDescriptors, string actionName) ParseResourceActionMethod(Method method, string apiVersion)
        {
            var (parseScopeSuccess, parseScopeFailureReason, scopeType, routingScope) = ParseResourceScopes(method, apiVersion);

            var resourceRoutingScope = routingScope.Substring(0, routingScope.LastIndexOf('/'));
            var actionName = routingScope.Substring(resourceRoutingScope.Length + 1);

            var (success, failureReason, resourceDescriptors) = ParseResourceDescriptors(method, apiVersion, scopeType, resourceRoutingScope);

            return (success, failureReason, resourceDescriptors, actionName);
        }

        private static (bool success, string failureReason, IEnumerable<IEnumerable<string>> resourceTypesFound) ParseResourceTypes(Method method, string routingScope)
        {
            var nameSegments = routingScope.Split('/').Skip(1).Where((_, i) => i % 2 == 0);

            if (nameSegments.Count() == 0)
            {
                return (false, $"Unable to find name segments", Enumerable.Empty<IEnumerable<string>>());
            }

            IEnumerable<IEnumerable<string>> resourceTypes = new[] { Enumerable.Empty<string>() };
            foreach (var nameSegment in nameSegments)
            {
                if (IsPathVariable(nameSegment))
                {
                    var parameterName = TrimParamBraces(nameSegment);
                    var parameter = method.Parameters.FirstOrDefault(methodParameter => methodParameter.SerializedName == parameterName);
                    if (parameter == null)
                    {
                        return (false, $"Found undefined parameter reference {nameSegment}", Enumerable.Empty<IEnumerable<string>>());
                    }

                    if (parameter.ModelType == null || !(parameter.ModelType is EnumType parameterType))
                    {
                        return (false, $"Parameter reference {nameSegment} is not defined as an enum", Enumerable.Empty<IEnumerable<string>>());
                    }

                    if (parameterType.Values == null || parameterType.Values.Count == 0)
                    {
                        return (false, $"Parameter reference {nameSegment} is defined as an enum, but doesn't have any specified values", Enumerable.Empty<IEnumerable<string>>());
                    }

                    resourceTypes = resourceTypes.SelectMany(type => parameterType.Values.Select(v => type.Append(v.SerializedName)));
                }
                else
                {
                    resourceTypes = resourceTypes.Select(type => type.Append(nameSegment));
                }
            }

            return (true, string.Empty, resourceTypes);
        }

        private static ProviderDefinition GetProviderDefinition(IDictionary<string, ProviderDefinition> providerDefinitions, CodeModel codeModel, ResourceDescriptor descriptor, string apiVersion)
        {
            if (!providerDefinitions.ContainsKey(descriptor.ProviderNamespace))
            {
                providerDefinitions[descriptor.ProviderNamespace] = new ProviderDefinition
                {
                    Namespace = descriptor.ProviderNamespace,
                    ApiVersion = apiVersion,
                    Model = codeModel,
                };
            }
            
            return providerDefinitions[descriptor.ProviderNamespace];
        }

        public static IEnumerable<GenerateResult> GenerateTypes(CodeModel serviceClient, string apiVersion)
        {            
            if (serviceClient == null)
            {
                throw new ArgumentNullException(nameof(serviceClient));
            }

            var providerDefinitions = new Dictionary<string, ProviderDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var putMethod in serviceClient.Methods.Where(method => ShouldProcessResourceType(serviceClient, method, apiVersion)))
            {
                var (success, failureReason, resourceDescriptors) = ParseResourceMethod(putMethod, apiVersion);
                if (!success)
                {
                    LogWarning($"Skipping resource PUT path '{putMethod.Url}': {failureReason}");
                    continue;
                }

                var getMethod = serviceClient.Methods.FirstOrDefault(x => x.Url == putMethod.Url && x.HttpMethod == HttpMethod.Get);

                foreach (var descriptor in resourceDescriptors)
                {
                    var providerDefinition = GetProviderDefinition(providerDefinitions, serviceClient, descriptor, apiVersion);

                    providerDefinition.ResourceDefinitions.Add(new ResourceDefinition
                    {
                        Descriptor = descriptor,
                        DeclaringMethod = putMethod,
                        GetMethod = getMethod,
                    });
                }
            }

            foreach (var listActionMethod in serviceClient.Methods.Where(method => ShouldProcessResourceAction(serviceClient, method, apiVersion)))
            {
                var (success, failureReason, resourceDescriptors, actionName) = ParseResourceActionMethod(listActionMethod, apiVersion);
                if (!success)
                {
                    LogWarning($"Skipping resource POST action path '{listActionMethod.Url}': {failureReason}");
                    continue;
                }

                foreach (var descriptor in resourceDescriptors)
                {
                    var providerDefinition = GetProviderDefinition(providerDefinitions, serviceClient, descriptor, apiVersion);

                    providerDefinition.ResourceListActions.Add(new ResourceListActionDefinition
                    {
                        ActionName = actionName,
                        Descriptor = descriptor,
                        DeclaringMethod = listActionMethod,
                    });
                }
            }

            return providerDefinitions.Select(definition => ProviderTypeGenerator.Generate(serviceClient, definition.Value));
        }

        public static bool IsPathVariable(string pathSegment)
        {
            Debug.Assert(pathSegment != null);

            return pathSegment.StartsWith("{", StringComparison.Ordinal) && pathSegment.EndsWith("}", StringComparison.Ordinal);
        }

        public static string TrimParamBraces(string pathSegment)
            => pathSegment.Substring(1, pathSegment.Length - 2);
    }
}
