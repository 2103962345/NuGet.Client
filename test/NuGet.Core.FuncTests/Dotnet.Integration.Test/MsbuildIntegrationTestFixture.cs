// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Protocol;
using NuGet.Test.Utility;
using NuGet.XPlat.FuncTest;
using Xunit;

namespace Dotnet.Integration.Test
{
    public class MsbuildIntegrationTestFixture : IDisposable
    {
        private readonly TestDirectory _cliDirectory;
        private readonly string _dotnetCli = DotnetCliUtil.GetDotnetCli();
        internal readonly string TestDotnetCli;
        internal readonly string MsBuildSdksPath;
        private readonly Dictionary<string, string> _processEnvVars = new Dictionary<string, string>();

        public MsbuildIntegrationTestFixture()
        {
            _cliDirectory = CopyLatestCliForPack();
            TestDotnetCli = Path.Combine(_cliDirectory, "dotnet.exe");

            var sdkPaths = Directory.GetDirectories(Path.Combine(_cliDirectory, "sdk"));

            // TODO - remove when shipping. See https://github.com/NuGet/Home/issues/8508
            // const string dotnetMajorVersion = "3.";
            const string dotnetMajorVersion = "5.";
            PatchSDKWithCryptographyDlls(dotnetMajorVersion, sdkPaths);

            MsBuildSdksPath = Path.Combine(
                sdkPaths.Where(path => path.Split(Path.DirectorySeparatorChar).Last().StartsWith(dotnetMajorVersion)).First(),
                "Sdks"
            );

            _processEnvVars.Add("MSBuildSDKsPath", MsBuildSdksPath);
            _processEnvVars.Add("UseSharedCompilation", "false");
            _processEnvVars.Add("DOTNET_MULTILEVEL_LOOKUP", "0");
            _processEnvVars.Add("MSBUILDDISABLENODEREUSE ", "true");
            // We do this here so that dotnet new will extract all the packages on the first run on the machine.
            InitDotnetNewToExtractPackages();
        }

        private void InitDotnetNewToExtractPackages()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                CreateDotnetNewProject(testDirectory.Path, projectName, " classlib", timeOut: 300000);
            }

            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ConsoleApp1";
                CreateDotnetNewProject(testDirectory.Path, projectName, " console", timeOut: 300000);
            }
        }

        internal void CreateDotnetNewProject(string solutionRoot, string projectName, string args = "console", int timeOut = 60000)
        {
            var workingDirectory = Path.Combine(solutionRoot, projectName);
            if (!Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }

            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"new {args}",
                waitForExit: true,
                timeOutInMilliseconds: timeOut,
                environmentVariables: _processEnvVars);

            Assert.True(result.Item1 == 0, $"Creating project failed with following log information :\n {result.AllOutput}");
            Assert.True(string.IsNullOrWhiteSpace(result.Item3), $"Creating project failed with following message in error stream :\n {result.AllOutput}");
        }

        internal void CreateDotnetToolProject(string solutionRoot, string projectName, string targetFramework, string rid, string source, IList<PackageIdentity> packages, int timeOut = 60000)
        {
            var workingDirectory = Path.Combine(solutionRoot, projectName);
            if (!Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }

            var projectFileName = Path.Combine(workingDirectory, projectName + ".csproj");

            var restorePackagesPath = Path.Combine(workingDirectory, "tools", "packages");
            var restoreSolutionDirectory = workingDirectory;
            var msbuildProjectExtensionsPath = Path.Combine(workingDirectory);
            var packageReference = string.Empty;
            foreach (var package in packages)
            {
                packageReference = string.Concat(packageReference, Environment.NewLine, $@"<PackageReference Include=""{ package.Id }"" Version=""{ package.Version.ToString()}""/>");
            }

            var projectFile = $@"<Project Sdk=""Microsoft.NET.Sdk"">
                <PropertyGroup><RestoreProjectStyle>DotnetToolReference</RestoreProjectStyle>
                <OutputType>Exe</OutputType>
                <TargetFramework> {targetFramework} </TargetFramework>
                <RuntimeIdentifier>{rid} </RuntimeIdentifier> 
                <!-- Things that do change-->
                <RestorePackagesPath>{restorePackagesPath}</RestorePackagesPath>
                <RestoreSolutionDirectory>{restoreSolutionDirectory}</RestoreSolutionDirectory>
                <MSBuildProjectExtensionsPath>{msbuildProjectExtensionsPath}</MSBuildProjectExtensionsPath>
                <RestoreSources>{source}</RestoreSources>
                <!--Things that don't change -->
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <RestoreFallbackFolders>clear</RestoreFallbackFolders>
                <RestoreAdditionalProjectSources></RestoreAdditionalProjectSources>
                <RestoreAdditionalProjectFallbackFolders></RestoreAdditionalProjectFallbackFolders>
                <RestoreAdditionalProjectFallbackFoldersExcludes></RestoreAdditionalProjectFallbackFoldersExcludes>
              </PropertyGroup>
                <ItemGroup>
                    {packageReference}
                </ItemGroup>
            </Project>";

            try
            {
                File.WriteAllText(projectFileName, projectFile);
            }
            catch
            {
                // ignore
            }
            Assert.True(File.Exists(projectFileName));
        }

        internal CommandRunnerResult RestoreToolProject(string workingDirectory, string projectName, string args = "")
        {

            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"restore {projectName}.csproj {args}",
                waitForExit: true,
                environmentVariables: _processEnvVars);
            return result;
        }

        internal void RestoreProject(string workingDirectory, string projectName, string args)
            => RestoreProjectOrSolution(workingDirectory, $"{projectName}.csproj", args);

        internal void RestoreSolution(string workingDirectory, string solutionName, string args)
            => RestoreProjectOrSolution(workingDirectory, $"{solutionName}.sln", args);

        private void RestoreProjectOrSolution(string workingDirectory, string fileName, string args)
        {

            var envVar = new Dictionary<string, string>();
            envVar.Add("MSBuildSDKsPath", MsBuildSdksPath);

            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"restore {fileName} {args}",
                waitForExit: true,
                environmentVariables: _processEnvVars);
            Assert.True(result.Item1 == 0, $"Restore failed with following log information :\n {result.AllOutput}");
            Assert.True(result.Item3 == "", $"Restore failed with following message in error stream :\n {result.AllOutput}");
        }

        /// <summary>
        /// dotnet.exe args
        /// </summary>
        internal CommandRunnerResult RunDotnet(string workingDirectory, string args, bool ignoreExitCode = false)
        {

            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                args,
                waitForExit: true,
                environmentVariables: _processEnvVars);

            if (!ignoreExitCode)
            {
                Assert.True(result.ExitCode == 0, $"dotnet.exe {args} command failed with following log information :\n {result.AllOutput}");
            }

            return result;
        }

        internal CommandRunnerResult PackProject(string workingDirectory, string projectName, string args, string nuspecOutputPath = "obj", bool validateSuccess = true)
            => PackProjectOrSolution(workingDirectory, $"{projectName}.csproj", args, nuspecOutputPath, validateSuccess);

        internal CommandRunnerResult PackSolution(string workingDirectory, string solutionName, string args, string nuspecOutputPath = "obj", bool validateSuccess = true)
            => PackProjectOrSolution(workingDirectory, $"{solutionName}.sln", args, nuspecOutputPath, validateSuccess);

        private CommandRunnerResult PackProjectOrSolution(string workingDirectory, string file, string args, string nuspecOutputPath, bool validateSuccess)
        {

            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"pack {file} {args} /p:NuspecOutputPath={nuspecOutputPath}",
                waitForExit: true,
                environmentVariables: _processEnvVars);
            if (validateSuccess)
            {
                Assert.True(result.Item1 == 0, $"Pack failed with following log information :\n {result.AllOutput}");
                Assert.True(result.Item3 == "", $"Pack failed with following message in error stream :\n {result.AllOutput}");
            }
            return result;
        }

        internal void BuildProject(string workingDirectory, string projectName, string args)
        {

            var result = CommandRunner.Run(TestDotnetCli,
                workingDirectory,
                $"msbuild {projectName}.csproj {args} /p:AppendRuntimeIdentifierToOutputPath=false",
                waitForExit: true,
                environmentVariables: _processEnvVars);
            Assert.True(result.Item1 == 0, $"Build failed with following log information :\n {result.AllOutput}");
            Assert.True(result.Item3 == "", $"Build failed with following message in error stream :\n {result.AllOutput}");
        }

        private TestDirectory CopyLatestCliForPack()
        {
            var cliDirectory = TestDirectory.Create();
            CopyLatestCliToTestDirectory(cliDirectory);
            UpdateCliWithLatestNuGetAssemblies(cliDirectory);
            return cliDirectory;
        }

        private void CopyLatestCliToTestDirectory(string destinationDir)
        {
            var cliDir = Path.GetDirectoryName(_dotnetCli);

            //Create sub-directory structure in destination
            foreach (var directory in Directory.GetDirectories(cliDir, "*", SearchOption.AllDirectories))
            {
                var destDir = destinationDir + directory.Substring(cliDir.Length);
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
            }

            var lastWriteTime = DateTime.Now.AddDays(-2);

            //Copy files recursively to destination directories
            foreach (var fileName in Directory.GetFiles(cliDir, "*", SearchOption.AllDirectories))
            {
                var destFileName = destinationDir + fileName.Substring(cliDir.Length);
                File.Copy(fileName, destFileName);
                File.SetLastWriteTime(destFileName, lastWriteTime);
            }
        }

        private void UpdateCliWithLatestNuGetAssemblies(string cliDirectory)
        {
            var nupkgsDirectory = DotnetCliUtil.GetNupkgDirectoryInRepo();

            var pathToPackNupkg = FindMostRecentNupkg(nupkgsDirectory, "NuGet.Build.Tasks.Pack");

            var nupkgsToCopy = new List<string> { "NuGet.Build.Tasks", "NuGet.Versioning", "NuGet.Protocol", "NuGet.ProjectModel", "NuGet.Packaging", "NuGet.LibraryModel", "NuGet.Frameworks", "NuGet.DependencyResolver.Core", "NuGet.Configuration", "NuGet.Common", "NuGet.Commands", "NuGet.CommandLine.XPlat", "NuGet.Credentials" };

            var sdkPaths = Directory.GetDirectories(Path.Combine(cliDirectory, "sdk"));

            var pathToSdkInCli = Path.Combine(
                    Directory.GetDirectories(Path.Combine(cliDirectory, "sdk"))
                        .First());

            using (var nupkg = new PackageArchiveReader(pathToPackNupkg))
            {
                var pathToPackSdk = Path.Combine(pathToSdkInCli, "Sdks", "NuGet.Build.Tasks.Pack");
                var files = nupkg.GetFiles()
                .Where(fileName => fileName.StartsWith("Desktop")
                                || fileName.StartsWith("CoreCLR")
                                || fileName.StartsWith("build")
                                || fileName.StartsWith("buildCrossTargeting"));

                DeleteDirectory(pathToPackSdk);
                CopyNupkgFilesToTarget(nupkg, pathToPackSdk, files);
            }


            foreach (var nupkgName in nupkgsToCopy)
            {
                using (var nupkg = new PackageArchiveReader(FindMostRecentNupkg(nupkgsDirectory, nupkgName)))
                {
                    var files = nupkg.GetFiles()
                   .Where(fileName => fileName.StartsWith("lib/netstandard2.1")
                                   || fileName.StartsWith("lib/netcoreapp5.0")
                                   || fileName.Contains("NuGet.targets"));
                    if (!files.Any())
                    {
                        files = nupkg.GetFiles()
                        .Where(fileName => fileName.StartsWith("lib/netstandard2.0")
                                    || fileName.Contains("NuGet.targets"));

                    }

                    CopyFlatlistOfFilesToTarget(nupkg, pathToSdkInCli, files);

                }
            }
        }

        private void CopyFlatlistOfFilesToTarget(PackageArchiveReader nupkg, string destination, IEnumerable<string> packageFiles)
        {
            var packageFileExtractor = new PackageFileExtractor(packageFiles,
                             PackageExtractionBehavior.XmlDocFileSaveMode);
            var logger = new TestCommandOutputLogger();
            var token = CancellationToken.None;
            var filesCopied = new List<string>();

            foreach (var packageFile in packageFiles)
            {
                token.ThrowIfCancellationRequested();

                var entry = nupkg.GetEntry(packageFile);

                var packageFileName = entry.FullName;
                // An entry in a ZipArchive could start with a '/' based on how it is zipped
                // Remove it if present
                if (packageFileName.StartsWith("/", StringComparison.Ordinal))
                {
                    packageFileName = packageFileName.Substring(1);
                }
                // Get only the name, without the path, since we are extracting to flat list
                packageFileName = Path.GetFileName(packageFileName);

                // ZipArchive always has forward slashes in them. By replacing them with DirectorySeparatorChar;
                // in windows, we get the windows-style path
                var normalizedPath = Uri.UnescapeDataString(packageFileName.Replace('/', Path.DirectorySeparatorChar));

                var targetFilePath = Path.Combine(destination, normalizedPath);
                if (!targetFilePath.StartsWith(destination, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                try
                {
                    File.Delete(targetFilePath);
                }
                catch
                {
                    // Do nothing
                }
                using (var stream = entry.Open())
                {
                    var copiedFile = packageFileExtractor.ExtractPackageFile(packageFileName, targetFilePath, stream);
                    if (copiedFile != null)
                    {
                        entry.UpdateFileTimeFromEntry(copiedFile, logger);

                        filesCopied.Add(copiedFile);
                    }
                }
            }

        }

        private void CopyNupkgFilesToTarget(PackageArchiveReader nupkg, string destPath, IEnumerable<string> files)
        {
            var packageFileExtractor = new PackageFileExtractor(files,
                                         PackageExtractionBehavior.XmlDocFileSaveMode);

            nupkg.CopyFiles(destPath, files, packageFileExtractor.ExtractPackageFile, new TestCommandOutputLogger(),
                CancellationToken.None);

        }

        private static string FindMostRecentNupkg(string nupkgDirectory, string id)
        {
            var info = LocalFolderUtility.GetPackagesV2(nupkgDirectory, new TestLogger());

            return info.Where(t => t.Identity.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .Where(t => !Path.GetExtension(Path.GetFileNameWithoutExtension(t.Path)).Equals(".symbols"))
                .OrderByDescending(p => p.LastWriteTimeUtc)
                .First().Path;
        }

        public void Dispose()
        {
            RunDotnet(Path.GetDirectoryName(TestDotnetCli), "build-server shutdown");
            KillDotnetExe(TestDotnetCli);
            _cliDirectory.Dispose();
        }

        private static void KillDotnetExe(string pathToDotnetExe)
        {
            var processes = Process.GetProcessesByName("dotnet")
                .Where(t => string.Compare(t.MainModule.FileName, Path.GetFullPath(pathToDotnetExe), ignoreCase: true) == 0);
            var testDirProcesses = Process.GetProcesses()
                .Where(t => t.MainModule.FileName.StartsWith(TestFileSystemUtility.NuGetTestFolder, StringComparison.OrdinalIgnoreCase));
            try
            {
                if (processes != null)
                {
                    foreach (var process in processes)
                    {
                        if (string.Compare(process.MainModule.FileName, Path.GetFullPath(pathToDotnetExe), true) == 0)
                        {
                            process.Kill();
                        }
                    }
                }

                if (testDirProcesses != null)
                {
                    foreach (var process in testDirProcesses)
                    {
                        process.Kill();
                    }
                }

            }
            catch { }
        }

        /// <summary>
        /// Depth-first recursive delete, with handling for descendant 
        /// directories open in Windows Explorer or used by another process
        /// </summary>
        private static void DeleteDirectory(string path)
        {
            foreach (string directory in Directory.GetDirectories(path))
            {
                DeleteDirectory(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                var MaxTries = 100;

                for (var i = 0; i < MaxTries; i++)
                {

                    try
                    {
                        Directory.Delete(path, recursive: true);
                        break;
                    }
                    catch (UnauthorizedAccessException) when (i < (MaxTries - 1))
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch
            {

            }
        }

        // Temporary added methods for processing deps.json files for patching

        /// <summary>
        /// Temporary patching process to bring in Cryptography DLLs for testing while SDK gets around to including them in 5.0.
        /// See also: https://github.com/NuGet/Home/issues/8508
        /// </summary>
        private void PatchSDKWithCryptographyDlls(string dotnetMajorVersion, string[] sdkPaths)
        {
            string directoryToPatch = sdkPaths.Where(path => path.Split(Path.DirectorySeparatorChar).Last().StartsWith(dotnetMajorVersion)).First();
            var assemblyNames = new string[1] { "System.Security.Cryptography.Pkcs.dll" };
            PatchDepsJsonFiles(assemblyNames, directoryToPatch);

            string userProfilePath = Environment.GetEnvironmentVariable(RuntimeEnvironmentHelper.IsWindows ? "USERPROFILE" : "HOME");
            string globalPackagesPath = Path.Combine(userProfilePath, ".nuget", "packages");

            CopyNewlyAddedDlls(assemblyNames, Directory.GetCurrentDirectory(), directoryToPatch);
        }

        private void PatchDepsJsonFiles(string[] assemblyNames, string patchDir)
        {
            string[] fileNames = new string[3] { "dotnet.deps.json", "MSBuild.deps.json", "NuGet.CommandLine.XPlat.deps.json" };
            string[] fullNames = fileNames.Select(filename => Path.Combine(patchDir, filename)).ToArray();
            PatchDepsJsonWithNewlyAddedDlls(assemblyNames, fullNames);
        }

        private void CopyNewlyAddedDlls(string[] assemblyNames, string copyFromPath, string copyToPath)
        {
            foreach (var assemblyName in assemblyNames)
            {
                File.Copy(
                    Path.Combine(copyFromPath, assemblyName),
                    Path.Combine(copyToPath, assemblyName)
                );
            }
        }

        private void PatchDepsJsonWithNewlyAddedDlls(string[] assemblyNames, string[] filePaths)
        {
            string nugetBuildTasksName = "NuGet.Build.Tasks/5.3.0-rtm.6251";
            foreach (string assemblyName in assemblyNames)
            {
                foreach (string filePath in filePaths)
                {
                    JObject jsonFile = GetJson(filePath);

                    JObject targets = jsonFile.GetJObjectProperty<JObject>("targets");

                    JObject netcoreapp50 = targets.GetJObjectProperty<JObject>(".NETCoreApp,Version=v5.0");

                    JObject nugetBuildTasks = netcoreapp50.GetJObjectProperty<JObject>(nugetBuildTasksName);

                    JObject runtime = nugetBuildTasks.GetJObjectProperty<JObject>("runtime");

                    var assemblyPath = Path.Combine(Directory.GetCurrentDirectory(), assemblyName);
                    var assemblyVersion = Assembly.LoadFile(assemblyPath).GetName().Version.ToString();
                    var assemblyFileVersion = FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion;
                    var jproperty = new JProperty("lib/netstandard2.1/" + assemblyName,
                        new JObject
                        {
                            new JProperty("assemblyVersion", assemblyVersion),
                            new JProperty("fileVersion", assemblyFileVersion),
                        }
                    );
                    runtime.Add(jproperty);
                    nugetBuildTasks["runtime"] = runtime;
                    netcoreapp50[nugetBuildTasksName] = nugetBuildTasks;
                    targets[".NETCoreApp,Version=v5.0"] = netcoreapp50;
                    jsonFile["targets"] = targets;
                    SaveJson(jsonFile, filePath);
                }
            }
        }

        private JObject GetJson(string jsonFilePath)
        {
            try
            {
                return FileUtility.SafeRead(jsonFilePath, (stream, filePath) =>
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return JObject.Parse(reader.ReadToEnd());
                    }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    string.Format("Failed to read json file at {0}: {1}", jsonFilePath, ex.Message),
                    ex
                );
            }
        }

        private void SaveJson(JObject json, string jsonFilePath)
        {
            FileUtility.Replace((outputPath) =>
            {
                using (var writer = new StreamWriter(outputPath, append: false, encoding: Encoding.UTF8))
                {
                    writer.Write(json.ToString());
                }
            },
            jsonFilePath);
        }
    }
}
