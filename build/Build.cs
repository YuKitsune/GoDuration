using System;
using System.Linq;
using Fallout.Common;
using Fallout.Common.CI.GitHubActions;
using Fallout.Common.Execution;
using Fallout.Common.Git;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Utilities.Collections;
using Fallout.Solutions;
using static Fallout.Common.EnvironmentInfo;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

[GitHubActions(
    "ci",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranches = new[] { "main" },
    OnPullRequestBranches = new[] { "main" },
    InvokedTargets = new[] { nameof(Test) },
    FetchDepth = 0)]
[GitHubActions(
    "release",
    GitHubActionsImage.UbuntuLatest,
    OnPushTags = new[] { "v*" },
    InvokedTargets = new[] { nameof(Publish) },
    ImportSecrets = new[] { nameof(NuGetApiKey) },
    EnableGitHubToken = true,
    FetchDepth = 0)]
class Build : FalloutBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("NuGet feed URL to push packages to.")]
    readonly string NuGetSource = "https://api.nuget.org/v3/index.json";

    [Parameter("NuGet API key. Set the NUGET_API_KEY secret in CI.")] [Secret]
    readonly string NuGetApiKey;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;

    static readonly string[] PackableProjectNames =
    {
        "GoDuration",
        "GoDuration.SystemTextJson",
        "GoDuration.NewtonsoftJson",
        "GoDuration.YamlDotNet",
    };

    AbsolutePath SourceDirectory => RootDirectory / "source";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath PackagesDirectory => ArtifactsDirectory / "nuget";
    AbsolutePath ReleaseDirectory => ArtifactsDirectory / "release";

    string _packageVersion;
    string PackageVersion => _packageVersion ??= ReadMinVerVersion();

    string ReadMinVerVersion()
    {
        var process = ProcessTasks.StartProcess(
                "dotnet",
                "minver --verbosity error",
                RootDirectory,
                logOutput: false);
        process.AssertZeroExitCode();
        var version = process.Output
            .Select(line => line.Text.Trim())
            .FirstOrDefault(line => !string.IsNullOrEmpty(line));
        if (string.IsNullOrEmpty(version))
            throw new InvalidOperationException("dotnet minver produced no output.");
        return version;
    }

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => x.DeleteDirectory());
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetToolRestore();
            DotNetRestore(s => s.SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(Test)
        .Produces(PackagesDirectory / "*.nupkg")
        .Produces(PackagesDirectory / "*.snupkg")
        .Executes(() =>
        {
            PackagesDirectory.CreateOrCleanDirectory();

            foreach (var name in PackableProjectNames)
            {
                var project = Solution.GetProject(name);
                DotNetPack(s => s
                    .SetProject(project)
                    .SetConfiguration(Configuration)
                    .SetOutputDirectory(PackagesDirectory)
                    .EnableNoBuild()
                    .EnableNoRestore());
            }
        });

    Target BundleZip => _ => _
        .DependsOn(Pack)
        .Produces(ReleaseDirectory / "*.zip")
        .Executes(() =>
        {
            ReleaseDirectory.CreateOrCleanDirectory();
            var stage = ReleaseDirectory / "stage";
            stage.CreateOrCleanDirectory();

            (RootDirectory / "LICENSE").CopyToDirectory(stage);
            (RootDirectory / "README.md").CopyToDirectory(stage);
            (RootDirectory / "CHANGELOG.md").CopyToDirectory(stage);
            PackagesDirectory.GlobFiles("*.nupkg", "*.snupkg").ForEach(x => x.CopyToDirectory(stage));

            var zipPath = ReleaseDirectory / $"GoDuration-{PackageVersion}.zip";
            stage.ZipTo(zipPath);
            stage.DeleteDirectory();
        });

    Target PublishNuGet => _ => _
        .DependsOn(Pack)
        .Requires(() => NuGetApiKey)
        .Executes(() =>
        {
            PackagesDirectory.GlobFiles("*.nupkg").ForEach(package =>
            {
                DotNetNuGetPush(s => s
                    .SetTargetPath(package)
                    .SetSource(NuGetSource)
                    .SetApiKey(NuGetApiKey)
                    .EnableSkipDuplicate());
            });
        });

    Target PublishGitHubRelease => _ => _
        .DependsOn(BundleZip)
        .Executes(() =>
        {
            var tag = $"v{PackageVersion}";
            var zipPath = ReleaseDirectory / $"GoDuration-{PackageVersion}.zip";
            var notes = ExtractReleaseNotes(PackageVersion);

            ProcessTasks.StartProcess(
                    "gh",
                    $"release create {tag} \"{zipPath}\" --title \"{tag}\" --notes-file \"{notes}\"",
                    RootDirectory)
                .AssertZeroExitCode();
        });

    AbsolutePath ExtractReleaseNotes(string version)
    {
        var changelog = RootDirectory / "CHANGELOG.md";
        var lines = changelog.ReadAllLines();
        var header = $"## [{version}]";

        var start = -1;
        var end = lines.Length;
        for (var i = 0; i < lines.Length; i++)
        {
            if (start < 0)
            {
                if (lines[i].StartsWith(header, StringComparison.Ordinal))
                    start = i + 1;
            }
            else if (lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                end = i;
                break;
            }
        }

        if (start < 0)
            throw new InvalidOperationException(
                $"CHANGELOG.md has no section for version {version}. Add a '## [{version}]' heading before the release.");

        var body = string.Join(Environment.NewLine, lines[start..end]).Trim();
        var notesFile = ReleaseDirectory / $"release-notes-{version}.md";
        notesFile.WriteAllText(body);
        return notesFile;
    }

    Target Publish => _ => _
        .DependsOn(PublishNuGet, PublishGitHubRelease);
}
