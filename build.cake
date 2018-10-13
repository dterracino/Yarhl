//
//  build.cake
//
//  Author:
//       Benito Palacios Sánchez (aka pleonex) <benito356@gmail.com>
//
//  Copyright (c) 2018 Benito Palacios Sánchez
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
#tool "nuget:?package=NUnit.ConsoleRunner"
#addin nuget:?package=SharpZipLib
#addin nuget:?package=Cake.Compression
#addin nuget:?package=Cake.Incubator&version=3.0.0
#addin nuget:?package=Cake.FileHelpers
#addin nuget:?package=altcover.api
#tool "nuget:?package=ReportGenerator"
#tool coveralls.io
#addin Cake.Coveralls
#addin "nuget:?package=Cake.Sonar"
#tool "nuget:?package=MSBuild.SonarQube.Runner.Tool"
#addin Cake.DocFx
#tool nuget:?package=docfx.console

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");

Task("Restore-NuGet")
    .Does(() =>
{
    NuGetRestore("src/Yarhl.sln");
});

Task("Build-Mono.Addins")
    .Does(() =>
{
    MSBuild(
        "mono-addins/Mono.Addins/Mono.Addins.csproj",
        configurator => configurator.SetConfiguration(configuration));
});

Task("Build")
    .IsDependentOn("Restore-NuGet")
    .IsDependentOn("Build-Mono.Addins")
    .Does(() =>
{
    MSBuild(
        "src/Yarhl.sln",
        configurator => configurator.SetConfiguration(configuration));
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    NUnit3(
        $"src/**/bin/{configuration}/*.UnitTests.dll",
        new NUnit3Settings { NoResults = true });
});

Task("Run-Linter-Gendarme")
    .IsDependentOn("Build")
    .Does(() =>
{
    var mono_tools = DownloadFile("https://github.com/pleonex/mono-tools/releases/download/v4.2.2/mono-tools-v4.2.2.zip");
    ZipUncompress(mono_tools, "tools/mono_tools");
    var gendarme = "tools/mono_tools/bin/gendarme";
    if (!IsRunningOnWindows()) {
        if (StartProcess("chmod", $"+x {gendarme}") != 0) {
            Error("Cannot change gendarme permissions");
        }
    }

    RunGendarme(gendarme, "src/Yarhl/Yarhl.csproj", "src/Yarhl/Gendarme.ignore");
    RunGendarme(gendarme, "src/Yarhl.Media/Yarhl.Media.csproj", "src/Yarhl.Media/Gendarme.ignore");
});

public void RunGendarme(string gendarme, string project, string ignore)
{
    var assembly = GetProjectAssemblies(project, configuration).Single();
    var retcode = StartProcess(gendarme, $"--ignore {ignore} {assembly}");
    if (retcode != 0) {
        Warning($"Gendarme found errors on {assembly}");
    }
}

Task("Run-AltCover")
    .IsDependentOn("Build")
    .Does(() =>
{
    var inputDir = $"src/Yarhl.UnitTests/bin/{configuration}";
    var outputDir = $"{inputDir}/__Instrumented";

    // Create new assemblies with the instrumentation
    var altcoverArgs = new AltCover.PrepareArgs {
        InputDirectory = inputDir,
        OutputDirectory = outputDir,
        AssemblyFilter = new[] { "nunit.framework", "Mono.Addins" },
        XmlReport = "coverage.xml",
        OpenCover = true
    };
    Prepare(altcoverArgs);

    // Run the tests again but instrumented
    NUnit3(
        $"{outputDir}/Yarhl.UnitTests.dll",
        new NUnit3Settings { NoResults = true });

    // Create the report
    ReportGenerator(
        "coverage.xml",
        "coveragereport",
        new ReportGeneratorSettings {
            ReportTypes = new[] {
                ReportGeneratorReportType.Html,
                ReportGeneratorReportType.TextSummary,
                ReportGeneratorReportType.XmlSummary } });

    // Get final result
    var xml = System.Xml.Linq.XDocument.Load("coveragereport/Summary.xml");
    var coverage = xml.Root.Element("Summary").Element("Linecoverage").Value;
    if (coverage == "100%") {
        Information("Full coverage!");
    } else {
        Warning($"Missing coverage: {coverage}");
    }
});

Task("Test-Quality")
    .IsDependentOn("Run-Linter-Gendarme")
    .IsDependentOn("Run-AltCover");


Task("Run-Coveralls")
    .IsDependentOn("Run-AltCover")
    .Does(() =>
{
    CoverallsIo(
        MakeAbsolute(File("coverage.xml")).FullPath,
        new CoverallsIoSettings {
            RepoToken = EnvironmentVariable("COVERALLS_REPO_TOKEN")
        });
});

Task("Run-Sonar")
    .IsDependentOn("Build")
    .Does(() =>
{
    var sonar_token = EnvironmentVariable("SONAR_TOKEN");
    SonarBegin(new SonarBeginSettings{
        Url = "https://sonarqube.com",
        Key = "yarhl",
        Login = sonar_token,
        Organization = "pleonex-github",
        Verbose = true
     });

    MSBuild("src/Yarhl.sln", configurator =>
            configurator.SetConfiguration(configuration)
                .WithTarget("Rebuild"));

     SonarEnd(new SonarEndSettings{
        Login = sonar_token
     });
});

Task("Build-Doc")
    .IsDependentOn("Build")
    .Does(() =>
{
    // Workaround for
    // https://github.com/dotnet/docfx/issues/3389
    NuGetInstall("SQLitePCLRaw.core", new NuGetInstallSettings {
        ExcludeVersion  = true,
        OutputDirectory = "./tools"
    });
    CopyFileToDirectory(
        "tools/SQLitePCLRaw.core/lib/net45/SQLitePCLRaw.core.dll",
        GetDirectories("tools/docfx.console.*").Single().Combine("tools"));

    DocFxMetadata("docs/docfx.json");
    DocFxBuild("docs/docfx.json");
});

Task("Serve-Doc")
    .IsDependentOn("Build-Doc")
    .Does(() =>
{
    DocFxBuild("docs/docfx.json", new DocFxBuildSettings { Serve = true });
});

Task("Deploy-Doc")
    .IsDependentOn("Build-Doc")
    .Does(() =>
{
    int retcode;

    // Clone or pull
    var repo_doc = Directory("doc-branch");
    if (!DirectoryExists(repo_doc)) {
        retcode = StartProcess(
            "git",
            $"clone git@github.com:SceneGate/Yarhl.git {repo_doc} -b gh-pages");
        if (retcode != 0) {
            throw new Exception("Cannot clone repository");
        }
    } else {
        retcode = StartProcess("git", new ProcessSettings {
            Arguments = "pull",
            WorkingDirectory = repo_doc
        });
        if (retcode != 0) {
            throw new Exception("Cannot pull repository");
        }
    }

    // Copy the content of the web
    CopyDirectory("docs/_site", repo_doc);

    // Commit and push
    retcode = StartProcess("git", new ProcessSettings {
        Arguments = "commit -a -m 'Update doc from cake'",
        WorkingDirectory = repo_doc
    });
    if (retcode != 0) {
        throw new Exception("Cannot commit");
    }

    retcode = StartProcess("git", new ProcessSettings {
        Arguments = "push origin gh-pages",
        WorkingDirectory = repo_doc
    });
    if (retcode != 0) {
        throw new Exception("Cannot push");
    }
});

Task("Default")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Test-Quality");

Task("Travis")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Test-Quality")
    .IsDependentOn("Build-Doc");  // Try to build the doc but don't deploy

Task("AppVeyor")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Run-Coveralls")
    .IsDependentOn("Run-Sonar");

RunTarget(target);
