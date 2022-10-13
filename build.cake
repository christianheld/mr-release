#tool "dotnet:?package=GitVersion.Tool&version=5.10.3"
#tool "dotnet:?package=dotnet-reportgenerator-globaltool&version=5.1.9"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

string solution = "MrRelease.sln";

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var dotNetVerbosity = DotNetVerbosity.Minimal;
var msBuildSettings = new DotNetMSBuildSettings()
        .SetMaxCpuCount(0);

Setup(context =>
{
    var version = context.GitVersion();

    context.Information($"Solution: {solution}");
    context.Information($"Version:  {version.FullSemVer}");
});

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("CleanArtifacts")
    .Does(() =>
{
    CleanDirectory("./artifacts");
});

Task("Clean")
   .WithCriteria(c => HasArgument("rebuild"))
   .Does(() => 
{
   var objs = GetDirectories($"./**/obj");
   var bins = GetDirectories($"./**/bin");

   CleanDirectories(objs.Concat(bins));
});

Task("RestorePackages")
    .Does(() =>
{
    DotNetRestore(new DotNetRestoreSettings { Verbosity = dotNetVerbosity });
});

Task("Compile")
    .IsDependentOn("RestorePackages")
    .Does(() =>
{
    DotNetBuild(
        solution,
        new DotNetBuildSettings
        {
            Configuration = configuration,
            NoRestore = true,
            Verbosity = dotNetVerbosity,
            MSBuildSettings = msBuildSettings
        });
});

Task("Test")
    .IsDependentOn("Compile")
    .Does(() =>
{
    DotNetTest(
       solution,
       new DotNetTestSettings
       {
          Configuration = configuration,
          NoRestore = true,
          NoBuild = true,
          Verbosity = dotNetVerbosity,
          Collectors = { "XPlat Code Coverage" }
       }
    );
});

Task("TestReport")
    .IsDependentOn("Test")
    .Does(() => 
{
    ReportGenerator(
        new GlobPattern("./tests/**/coverage.cobertura.xml"),
        "./artifacts/TestReport");
});

Task("DotNetPublish")
    .DoesForEach(new[] { "win-x64", "linux-x64", "osx-x64" }, runtime =>
{
    DotNetPublish(
        "./src/MrRelease/MrRelease.csproj",
        new DotNetPublishSettings
        {
            Configuration = configuration,
            Verbosity = dotNetVerbosity,
            PublishSingleFile = true,
            Runtime = runtime,
            SelfContained = true,
            PublishTrimmed = true,
            IncludeNativeLibrariesForSelfExtract = true,
        });

    CreateDirectory($"./artifacts/{runtime}");
    CopyFiles(
        $"./src/MrRelease/bin/{configuration}/net6.0/{runtime}/publish/*",
        $"./artifacts/{runtime}");
});


Task("Build")
   .IsDependentOn("CleanArtifacts")
   .IsDependentOn("Test");

Task("Publish")
    .IsDependentOn("CleanArtifacts")
    .IsDependentOn("DotNetPublish");

Task("Default")
    .IsDependentOn("Build")
    .IsDependentOn("Publish");

RunTarget(target);
