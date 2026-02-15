using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Assertions;

namespace SigmaTau.Unity.NeovimCodeEditor.Editor
{
    public class NeovimProjectGeneration : IDisposable
    {
        private const string _projectTypeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        private static readonly string[] _capabilitiesToRemove = new string[]
        {
            "LaunchProfiles",
            "SharedProjectReferences",
            "ReferenceManagerSharedProjects",
            "ProjectReferences",
            "ReferenceManagerProjects",
            "COMReferences",
            "ReferenceManagerCOM",
            "AssemblyReferences",
            "ReferenceManagerAssemblies",
        };

        private readonly bool _deleteProjectFiles = NeovimCodeEditorSettings.DeleteProjectFiles;
        private readonly bool _useXmlSolution = NeovimCodeEditorSettings.UseXmlSolution;
        private readonly List<ProjectInfo> _projects = new();
        // File size generally seems to be 70kB - 90kB on Windows.
        private readonly MemoryStream _memoryStream = new(100 * 1024);
        private MD5 _md5;

        public void Dispose()
        {
            _memoryStream.Dispose();
            _md5?.Dispose();
        }

        public void GenerateProjectFiles()
        {
            GetProjectsToInclude();

            foreach (ProjectInfo project in _projects)
            {
                CreateProject(project);
            }

            CreateNugetConfig();

            if (_useXmlSolution)
            {
                CreateSolutionXml();
            }
            else
            {
                CreateSolutionLegacy();
            }

            if (_deleteProjectFiles)
            {
                DeleteUnusedProjectFiles();
            }
        }

        private void DeleteUnusedProjectFiles()
        {
            foreach (var csProjFile in Directory
                .EnumerateFiles(PathUtils.AssetFolderFullPath, "*.csproj", SearchOption.AllDirectories)
                .Where((csProjFile) => !_projects.Any((p) => PathUtils.IsSameFile(p.CsProjFilename, csProjFile))))
            {
                Debug.LogWarningFormat("Deleting unused CS project file: {0}", csProjFile);
                File.Delete(csProjFile);
            }

            foreach (var csProjMetaFile in Directory
                .EnumerateFiles(PathUtils.AssetFolderFullPath, "*.csproj.meta", SearchOption.AllDirectories)
                .Where((csProjMetaFile) =>
                    !_projects.Any((p) => PathUtils.IsSameFile(p.CsProjFilename + ".meta", csProjMetaFile))))
            {
                Debug.LogWarningFormat("Deleting unused CS project meta file: {0}", csProjMetaFile);
                File.Delete(csProjMetaFile);
            }

            string solutionFilename = $"{PathUtils.ProjectName}.{(_useXmlSolution ? "slnx" : "sln")}";

            foreach (var slnFile in Directory
                .EnumerateFiles(PathUtils.ProjectFullPath, "*.sln", SearchOption.TopDirectoryOnly)
                .Where((slnFile) => !PathUtils.IsSameFile(solutionFilename, slnFile)))
            {
                Debug.LogWarningFormat("Deleting unused solution file: {0}", slnFile);
                File.Delete(slnFile);
            }
            foreach (var slnxFile in Directory
                .EnumerateFiles(PathUtils.ProjectFullPath, "*.slnx", SearchOption.TopDirectoryOnly)
                .Where((slnxFile) => !PathUtils.IsSameFile(solutionFilename, slnxFile)))
            {
                Debug.LogWarningFormat("Deleting unused solution file: {0}", slnxFile);
                File.Delete(slnxFile);
            }
        }

        private void GetProjectsToInclude()
        {
            var assemblies = CompilationPipeline.GetAssemblies();

            foreach (Assembly assembly in assemblies)
            {
                string assemblyDefinitionPath =
                    CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                // Root folder is the folder that contains the asmdef file for the assembly. If one isn't available,
                // find the common root folder for all source files.
                string rootSourcePath = string.IsNullOrWhiteSpace(assemblyDefinitionPath)
                    ? PathUtils.TryFindRootPathOfAllFiles(assembly.sourceFiles)
                    : Path.GetDirectoryName(assemblyDefinitionPath);

                if (!PathUtils.IsInAssetsFolder(rootSourcePath))
                {
                    continue;
                }

                var newProject = new ProjectInfo
                {
                    Assembly = assembly,
                    CsProjFilename = $"{rootSourcePath}/{assembly.name}.csproj",
                    RootSourcePath = rootSourcePath,
                    NestedProjects = new List<ProjectInfo>(),
                };

                foreach (ProjectInfo checkProject in _projects)
                {
                    if (PathUtils.IsNested(newProject.RootSourcePath, checkProject.RootSourcePath))
                    {
                        newProject.NestedProjects.Add(checkProject);
                    }
                    else if (PathUtils.IsNested(checkProject.RootSourcePath, newProject.RootSourcePath))
                    {
                        checkProject.NestedProjects.Add(newProject);
                    }
                }

                _projects.Add(newProject);
            }
        }

        private void TryWriteFile(string filename)
        {
            string path = Path.Combine(PathUtils.ProjectFullPath, filename);

            FileStream fileStream = null;
            try
            {
                // Try to open/create the file 3 times; otherwise, let it throw.
                for (int attempts = 1; fileStream is null; attempts++)
                {
                    try
                    {
                        fileStream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                    }
                    catch when (attempts < 3)
                    {
                        Thread.Sleep(500);
                    }
                }

                int outputLength = (int)_memoryStream.Length;
                byte[] outputBytes = _memoryStream.GetBuffer();

                if (fileStream.Length == outputLength)
                {
                    ReadOnlySpan<byte> outputSpan = outputBytes.AsSpan(0, outputLength);
                    Span<byte> buffer = stackalloc byte[1024];

                    bool matches = true;
                    while (outputSpan.Length > 0 && matches)
                    {
                        int bytesRead = fileStream.Read(buffer);
                        matches = buffer[..bytesRead] == outputSpan[..bytesRead];
                        outputSpan = outputSpan[bytesRead..];
                    }

                    if (matches)
                    {
                        return;
                    }
                }

                fileStream.Position = 0;
                fileStream.SetLength(outputLength);
                fileStream.Write(outputBytes, 0, outputLength);
                fileStream.Flush();
                fileStream.Close();
            }
            finally
            {
                fileStream?.Dispose();
            }
        }

        private void CreateNugetConfig()
        {
            _memoryStream.Position = 0;
            _memoryStream.SetLength(0);

            using var xml = XmlWriter.Create(
                _memoryStream,
                new XmlWriterSettings
                {
                    Encoding = Encoding.UTF8,
                    Indent = true,
                    IndentChars = "    ",
                    OmitXmlDeclaration = false,
                    CloseOutput = false,
                }
            );

            xml.WriteStartElement("configuration");
            xml.WriteStartElement("config");
            xml.WriteStartElement("add");
            xml.WriteAttributeString("key", "globalPackagesFolder");
            xml.WriteAttributeString("value", Path.Combine("Temp", "NugetPackages"));
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.Flush();

            TryWriteFile("nuget.config");
        }

        private void CreateSolutionXml()
        {
            _memoryStream.Position = 0;
            _memoryStream.SetLength(0);

            using var xml = XmlWriter.Create(
                _memoryStream,
                new XmlWriterSettings
                {
                    Encoding = Encoding.UTF8,
                    Indent = true,
                    IndentChars = "    ",
                    OmitXmlDeclaration = true,
                    CloseOutput = false,
                }
            );

            xml.WriteStartElement("Solution");
            foreach (var project in _projects)
            {
                xml.WriteStartElement("Project");
                xml.WriteAttributeString("Path", project.CsProjFilename);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            xml.Flush();

            TryWriteFile($"{PathUtils.ProjectName}.slnx");
        }

        private void CreateSolutionLegacy()
        {
            _memoryStream.Position = 0;
            _memoryStream.SetLength(0);

            using var writer = new StreamWriter(_memoryStream, Encoding.UTF8, 512, true);

            writer.WriteLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            writer.WriteLine("# Visual Studio 15");

            foreach (ProjectInfo project in _projects)
            {
                project.ProjectGuid = GetProjectGuid(project.Assembly);
                writer.WriteLine(
                    "Project(\"{0}\") = \"{1}\", \"{2}\", \"{3}\"",
                    _projectTypeGuid,
                    project.Assembly.name,
                    project.CsProjFilename,
                    project.ProjectGuid
                );
                writer.WriteLine("EndProject");
            }

            writer.WriteLine("Global");
            writer.WriteLine("    GlobalSection(SolutionConfigurationPlatforms) = preSolution");
            writer.WriteLine("        Debug|Any CPU = Debug|Any CPU");
            writer.WriteLine("        Release|Any CPU = Release|Any CPU");
            writer.WriteLine("    EndGlobalSection");
            writer.WriteLine("    GlobalSection(ProjectConfigurationPlatforms) = postSolution");

            foreach (ProjectInfo project in _projects)
            {
                writer.WriteLine($"        {project.ProjectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
                writer.WriteLine($"        {project.ProjectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU");
                writer.WriteLine($"        {project.ProjectGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU");
                writer.WriteLine($"        {project.ProjectGuid}.Release|Any CPU.Build.0 = Release|Any CPU");
            }

            writer.WriteLine("    EndGlobalSection");
            writer.WriteLine("    GlobalSection(SolutionProperties) = preSolution");
            writer.WriteLine("        HideSolutionNode = FALSE");
            writer.WriteLine("    EndGlobalSection");
            writer.WriteLine("EndGlobal");
            writer.Flush();

            TryWriteFile($"{PathUtils.ProjectName}.sln");
        }

        private void CreateProject(ProjectInfo project)
        {
            _memoryStream.Position = 0;
            _memoryStream.SetLength(0);

            using var xml = XmlWriter.Create(
                _memoryStream,
                new XmlWriterSettings
                {
                    Encoding = Encoding.UTF8,
                    Indent = true,
                    IndentChars = "    ",
                    OmitXmlDeclaration = true,
                    CloseOutput = false,
                }
            );

            xml.WriteStartElement("Project");
            xml.WriteAttributeString("ToolsVersion", "Current");

            xml.WriteStartElement("PropertyGroup");
            xml.WriteElementString(
                "BaseIntermediateOutputPath",
                PathUtils.GetAbsoluteOrRelativePath(
                    project.RootSourcePath,
                    "Temp/obj/$(Configuration)/$(MSBuildProjectName)/"));
            xml.WriteElementString("IntermediateOutputPath", "$(BaseIntermediateOutputPath)");
            xml.WriteEndElement();

            xml.WriteStartElement("Import");
            xml.WriteAttributeString("Project", "Sdk.props");
            xml.WriteAttributeString("Sdk", "Microsoft.NET.Sdk");
            xml.WriteEndElement();

            xml.WriteStartElement("PropertyGroup");
            xml.WriteElementString("GenerateAssemblyInfo", "false");
            xml.WriteElementString("EnableDefaultItems", "false");
            xml.WriteElementString("AppendTargetFrameworkToOutputPath", "false");
            xml.WriteElementString("LangVersion", project.Assembly.compilerOptions.LanguageVersion);
            xml.WriteElementString("Configurations", "Debug;Release");
            xml.WriteStartElement("Configuration");
            xml.WriteAttributeString("Condition", "'$(Configuration)' == ''");
            xml.WriteString("Debug");
            xml.WriteEndElement();
            xml.WriteStartElement("Platform");
            xml.WriteAttributeString("Condition", "'$(Platform)' == ''");
            xml.WriteString("AnyCPU");
            xml.WriteEndElement();
            xml.WriteElementString("RootNamespace", project.Assembly.rootNamespace);
            xml.WriteElementString("OutputType", "Library");
            xml.WriteElementString("AssemblyName", project.Assembly.name);
            xml.WriteElementString("TargetFramework", "netstandard2.1");
            xml.WriteElementString("WarningLevel", "4");
            xml.WriteElementString("NoWarn", "0169;USG0001");
            xml.WriteElementString("AllowUnsafeBlocks", project.Assembly.compilerOptions.AllowUnsafeCode.ToString());
            xml.WriteElementString(
                "OutputPath",
                PathUtils.GetAbsoluteOrRelativePath(
                    project.RootSourcePath, "Temp/bin/$(Configuration)/$(MSBuildProjectName)/"));
            xml.WriteEndElement();

            xml.WriteStartElement("PropertyGroup");
            xml.WriteAttributeString("Condition", "'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'");
            xml.WriteElementString("DebugSymbols", "true");
            xml.WriteElementString("DebugType", "full");
            xml.WriteElementString("Optimize", "false");
            xml.WriteElementString("DefineConstants", string.Join(";", project.Assembly.defines));
            xml.WriteEndElement();

            xml.WriteStartElement("PropertyGroup");
            xml.WriteAttributeString("Condition", "'$(Configuration)|$(Platform)' == 'Release|AnyCPU'");
            xml.WriteElementString("DebugType", "pdbonly");
            xml.WriteElementString("Optimize", "true");
            xml.WriteEndElement();

            xml.WriteStartElement("PropertyGroup");
            xml.WriteElementString("NoStandardLibraries", "true");
            xml.WriteElementString("NoStdLib", "true");
            xml.WriteElementString("NoConfig", "true");
            xml.WriteElementString("DisableImplicitFrameworkReferences", "true");
            xml.WriteElementString("MSBuildWarningsAsMessages", "MSB3277");
            xml.WriteEndElement();

            xml.WriteStartElement("ItemGroup");
            xml.WriteStartElement("PackageReference");
            xml.WriteAttributeString("Include", "Microsoft.Unity.Analyzers");
            xml.WriteAttributeString("Version", "*");
            xml.WriteStartElement("PrivateAssets");
            xml.WriteString("all");
            xml.WriteEndElement();
            xml.WriteStartElement("IncludeAssets");
            xml.WriteString("runtime; build; native; contentfiles; analyzers; buildtransitive");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteStartElement("ItemGroup");

            var analyzers = project.Assembly.compilerOptions.RoslynAnalyzerDllPaths;
            // if (_unityAnlayzerPath != null)
            // {
            //     xml.WriteStartElement("Analyzer");
            //     xml.WriteAttributeString("Include",
            //         PathUtils.GetAbsoluteOrRelativePath(project.RootSourcePath, _unityAnlayzerPath));
            //     xml.WriteEndElement();
            // }
            foreach (string analyzer in analyzers)
            {
                xml.WriteStartElement("Analyzer");
                xml.WriteAttributeString("Include", analyzer);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            xml.WriteStartElement("Import");
            xml.WriteAttributeString("Project", "Sdk.targets");
            xml.WriteAttributeString("Sdk", "Microsoft.NET.Sdk");
            xml.WriteEndElement();

            xml.WriteStartElement("ItemGroup");
            foreach (string capabilityToRemove in _capabilitiesToRemove)
            {
                xml.WriteStartElement("ProjectCapability");
                xml.WriteAttributeString("Remove", capabilityToRemove);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            xml.WriteStartElement("ItemGroup");
            xml.WriteStartElement("Compile");
            xml.WriteAttributeString("Include", "**/*.cs");
            xml.WriteEndElement();

            foreach (ProjectInfo nestedProject in project.NestedProjects)
            {
                xml.WriteStartElement("Compile");
                xml.WriteAttributeString(
                    "Remove",
                    Path.Combine(
                        PathUtils.GetAbsoluteOrRelativePath(project.RootSourcePath, nestedProject.RootSourcePath),
                        "**", "*.cs"));
                xml.WriteEndElement();
            }

            xml.WriteEndElement();

            var projectReferences = new List<string>();

            xml.WriteStartElement("ItemGroup");
            foreach (string referencePath in project.Assembly.allReferences)
            {
                string name = Path.GetFileNameWithoutExtension(referencePath);
                ProjectInfo otherProject = _projects.FirstOrDefault((p) => p.Assembly.name == name);
                if (otherProject is not null)
                {
                    projectReferences.Add(otherProject.CsProjFilename);
                    continue;
                }
                xml.WriteStartElement("Reference");
                xml.WriteAttributeString("Include", name);
                xml.WriteElementString("HintPath",
                    PathUtils.GetAbsoluteOrRelativePath(project.RootSourcePath, referencePath));
                xml.WriteElementString("Private", "false");
                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            xml.WriteStartElement("ItemGroup");
            foreach (string projectReference in projectReferences)
            {
                xml.WriteStartElement("ProjectReference");
                xml.WriteAttributeString("Include",
                    PathUtils.GetAbsoluteOrRelativePath(project.RootSourcePath, projectReference));
                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            xml.WriteEndElement();

            xml.Flush();

            TryWriteFile(project.CsProjFilename);
        }

        private string GetProjectGuid(Assembly assembly)
        {
            static void WriteHex(Span<char> guidSpan, ReadOnlySpan<byte> hashSpan)
            {
                const string hexDigits = "0123456789ABCDEF";
                Assert.AreEqual(guidSpan.Length, hashSpan.Length * 2);
                for (int index = 0; index < hashSpan.Length; index++)
                {
                    byte hashByte = hashSpan[index];
                    int guidIndex = 2 * index;
                    guidSpan[guidIndex] = hexDigits[hashByte >> 4];
                    guidSpan[guidIndex + 1] = hexDigits[hashByte & 0xf];
                }
            }

            int byteCount = Encoding.UTF8.GetByteCount(assembly.name);
            Span<byte> nameEncoded = stackalloc byte[byteCount];
            int bytesWritten = Encoding.UTF8.GetBytes(assembly.name, nameEncoded);

            Span<byte> hashSpan = stackalloc byte[16];
            _md5 ??= MD5.Create();
            if (!_md5.TryComputeHash(nameEncoded[..bytesWritten], hashSpan, out bytesWritten))
            {
                throw new InvalidOperationException("Failed to compute project GUID");
            }
            Assert.AreEqual(16, bytesWritten);

            Span<char> guidSpan = stackalloc char[38];
            guidSpan[0] = '{';
            WriteHex(guidSpan[1..9], hashSpan[0..4]);
            guidSpan[9] = '-';
            WriteHex(guidSpan[10..14], hashSpan[4..6]);
            guidSpan[14] = '-';
            WriteHex(guidSpan[15..19], hashSpan[6..8]);
            guidSpan[19] = '-';
            WriteHex(guidSpan[20..24], hashSpan[8..10]);
            guidSpan[24] = '-';
            WriteHex(guidSpan[25..37], hashSpan[10..16]);
            guidSpan[37] = '}';

            return guidSpan.ToString();
        }

        private class ProjectInfo
        {
            public Assembly Assembly { get; set; }

            public string CsProjFilename { get; set; }

            public string RootSourcePath { get; set; }

            public List<ProjectInfo> NestedProjects { get; set; }

            public string ProjectGuid { get; set; }
        }
    }
}
