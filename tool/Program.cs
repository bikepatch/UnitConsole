using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.RepresentationModel;

class UnityProjectAnalyzer
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: tool.exe <unity_project_path> <output_folder_path>");
            return;
        }

        string projectDirectory = args[0];
        string outputDirectory = args[1];

        var sceneFiles = Directory.GetFiles(projectDirectory, "*.unity", SearchOption.AllDirectories);
        var scriptFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);

        Console.WriteLine("Scene Files:");
        foreach (var sceneFile in sceneFiles)
        {
            Console.WriteLine(sceneFile);
            var hierarchy = ExtractHierarchyFromScene(sceneFile);
            string dumpFilePath = Path.Combine(outputDirectory, Path.GetFileName(Path.ChangeExtension(sceneFile, ".dump")));
            DumpHierarchyToFile(hierarchy, dumpFilePath);
        }

        Console.WriteLine("\nScript Files:");
        foreach (var scriptFile in scriptFiles)
        {
            Console.WriteLine(scriptFile);
        }

        var unusedScripts = FindUnusedScripts(projectDirectory);
        string unusedScriptsFilePath = Path.Combine(outputDirectory, "UnusedScripts.csv");
        DumpUnusedScriptsToFile(unusedScripts, unusedScriptsFilePath);
    }

    private static List<string> ExtractHierarchyFromScene(string sceneFile)
    {
        var hierarchy = new List<string>();
        
        using (var reader = new StreamReader(sceneFile))
        {
            var yaml = new YamlStream();
            yaml.Load(reader);

            foreach (var document in yaml.Documents)
            {
                foreach (var entry in ((YamlMappingNode)document.RootNode).Children)
                {
                    var key = ((YamlScalarNode)entry.Key).Value;
                    if (key.StartsWith("GameObject")) // GameObject node
                    {   
                        var gameObjectNode = (YamlMappingNode)entry.Value;
                        if (gameObjectNode.Children.TryGetValue(new YamlScalarNode("m_Name"), out var nameNode) &&
                            nameNode is YamlScalarNode nameScalarNode)
                        {
                            hierarchy.Add(nameScalarNode.Value);
                        }
                    }
                }
            }
        }

        return hierarchy;
    }

    private static long ExtractGameObjectId(string idString)
    {
        var parts = idString.Split('&');
        if (parts.Length > 1 && long.TryParse(parts[1], out long id))
        {
            return id;
        }
        return 0;
    }
    private static int GetDepth(long gameObjectId, Dictionary<long, long> parentMap)
    {
        int depth = 0;
        while (parentMap.ContainsKey(gameObjectId))
        {
            gameObjectId = parentMap[gameObjectId];
            depth++;
        }
        return depth;
    }

   private static List<string> FindUnusedScripts(string projectDirectory)
    {
        var allScriptFiles = new HashSet<string>(Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories));
        var usedScripts = GetUsedScripts(projectDirectory);

        var unusedScripts = new List<string>();
        foreach (var scriptFile in allScriptFiles)
        {
            var scriptFileName = Path.GetFileName(scriptFile);
            if (!usedScripts.Contains(scriptFileName))
            {
                unusedScripts.Add(scriptFileName);
            }
        }

        return unusedScripts;
    }

    private static HashSet<string> GetUsedScripts(string projectDirectory)
    {
        var usedScripts = new HashSet<string>();
        var sceneFiles = Directory.GetFiles(projectDirectory, "*.unity", SearchOption.AllDirectories);

        foreach (var sceneFile in sceneFiles)
        {
            using (var reader = new StreamReader(sceneFile))
            {
                var yaml = new YamlStream();
                yaml.Load(reader);

                foreach (var document in yaml.Documents)
                {
                    var rootNode = document.RootNode;
                    if (rootNode is YamlMappingNode)
                    {
                        foreach (var node in (rootNode as YamlMappingNode).Children)
                        {
                            if (node.Key is YamlScalarNode keyNode && keyNode.Value == "MonoBehaviour")
                            {
                                var monoBehaviourNode = node.Value as YamlMappingNode;
                                if (monoBehaviourNode.Children.TryGetValue(new YamlScalarNode("m_Script"), out var scriptNode))
                                {
                                    var script = scriptNode as YamlMappingNode;
                                    if (script.Children.TryGetValue(new YamlScalarNode("guid"), out var guidNode))
                                    {
                                        var scriptGuid = (guidNode as YamlScalarNode)?.Value;
                                        usedScripts.Add(scriptGuid);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return usedScripts;
    }

    private static void DumpHierarchyToFile(List<string> hierarchy, string filePath)
    {
        File.WriteAllLines(filePath, hierarchy);
    }

    private static void DumpUnusedScriptsToFile(List<string> unusedScripts, string filePath)
    {
        var lines = new List<string> { "Relative Path,GUID" };
        foreach (var script in unusedScripts)
        {
            var relativePath = GetRelativePath(script, filePath);
            var guid = GetGuidForScript(script);
            lines.Add($"{relativePath},{guid}");
        }

        File.WriteAllLines(filePath, lines);
    }

    private static string GetRelativePath(string fullPath, string baseFolder)
    {
        // Ensure there's a trailing slash on the base folder path
        if (!baseFolder.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            baseFolder += Path.DirectorySeparatorChar;

        // Check if the full path starts with the base folder path
        if (fullPath.StartsWith(baseFolder, StringComparison.OrdinalIgnoreCase))
        {
            // Return the part of the full path that comes after the base folder path
            return fullPath.Substring(baseFolder.Length);
        }
        else
        {
            // If not a subset, return the original path
            return fullPath;
        }
    }
    private static string GetGuidForScript(string scriptPath)
    {
        string metaFilePath = scriptPath + ".meta";
        Console.WriteLine(metaFilePath);
        if (File.Exists(metaFilePath))
        {   
            var lines = File.ReadAllLines(metaFilePath);
            foreach (var line in lines)
            {
                if (line.StartsWith("guid:"))
                {
                    return line.Split(new[] { "guid:" }, StringSplitOptions.None)[1].Trim();
                }
            }
        }
        return "GUID_NOT_FOUND"; // Return a placeholder or handle the error as needed
    }
}