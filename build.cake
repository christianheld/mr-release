#addin nuget:?package=Cake.GitVersioning&version=3.6.146

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
    var version = GitVersioningGetVersion();

    context.Information($"Solution: {solution}");
    context.Information($"Version:  {version.SemVer2}");
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

void DotNetPublishCore(string runtime)
{
    DotNetPublish(
        "./src/MrRelease/MrRelease.csproj",
        new DotNetPublishSettings
        {
            Configuration = configuration,
            Verbosity = dotNetVerbosity,
            PublishSingleFile = true,
            Runtime = runtime,
            SelfContained = false,
            PublishTrimmed = false,
            IncludeNativeLibrariesForSelfExtract = true,
        });

    CreateDirectory($"./artifacts/{runtime}");
    CopyFiles(
        $"./src/MrRelease/bin/{configuration}/net9.0/{runtime}/publish/*",
        $"./artifacts/{runtime}");
}

Task("Publish-Windows")
    .IsDependentOn("CleanArtifacts")
    .Does(() => DotNetPublishCore("win-x64"));

Task("Publish-Linux")
    .IsDependentOn("CleanArtifacts")
    .Does(() => DotNetPublishCore("linux-x64"));

Task("Publish-Mac")
    .IsDependentOn("CleanArtifacts")
    .Does(() => DotNetPublishCore("osx-x64"))
    .Does(() => DotNetPublishCore("osx-arm64"));

Task("Build")
   .IsDependentOn("CleanArtifacts")
   .IsDependentOn("Test");

Task("Publish")
    .IsDependentOn("Publish-Windows")
    .IsDependentOn("Publish-Linux")
    .IsDependentOn("Publish-Mac");

Task("Default")
    .IsDependentOn("Build")
    .IsDependentOn("Publish");

RunTarget(target);
