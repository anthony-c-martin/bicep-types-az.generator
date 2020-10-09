// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bicep.SerializedTypes;
using Bicep.SerializedTypes.Az;
using Bicep.SerializedTypes.Concrete;

namespace Bicep.TypeIndexBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            var baseDir = Path.Join(args[0], "types");
            var indexContent = BuildSerializedIndex(baseDir);
            var indexPath = Path.Combine(baseDir, "index.json");

            File.WriteAllText(indexPath, indexContent);
        }
        
        private static (TypeBase type, int index) GetTypeWithIndex(TypeBase type, int index)
            => (type, index);

        private static IReadOnlyDictionary<string, TypeLocation> BuildIndex(string baseDir)
        {
            var typeFiles = Directory.GetFiles(baseDir, "types.json", SearchOption.AllDirectories);
            var typeDictionary = new Dictionary<string, TypeLocation>(StringComparer.OrdinalIgnoreCase);

            foreach (var typeFile in typeFiles)
            {
                var content = File.ReadAllText(typeFile);

                var relativePath = Path.GetFullPath(typeFile).Substring(Path.GetFullPath(baseDir).Length + 1)
                    .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToLowerInvariant();

                var indexedTypes = TypeSerializer.Deserialize(content).Select(GetTypeWithIndex);

                foreach (var (type, index) in indexedTypes)
                {
                    if (!(type is ResourceType resourceType))
                    {
                        continue;
                    }

                    if (resourceType.Name == null)
                    {
                        throw new ArgumentException($"Found resource with null resource name");
                    }

                    if (typeDictionary.ContainsKey(resourceType.Name))
                    {
                        Console.WriteLine($"WARNING: Found duplicate type {resourceType.Name}");
                        continue;
                    }

                    typeDictionary.Add(resourceType.Name, new TypeLocation
                    {
                        RelativePath = relativePath,
                        Index = index,
                    });
                }
            }

            return typeDictionary;
        }

        public static string BuildSerializedIndex(string baseDir)
        {
            var output = BuildIndex(baseDir);
            
            return JsonSerializer.Serialize(output);
        }
    }
}
