// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------
#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"
#r "netstandard"

open Fake.Core
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
// --------------------------------------------------------------------------------------
// Build variables
// --------------------------------------------------------------------------------------
let f projName =
    let pattern = sprintf @"**/%s.fsproj" projName
    let xs = !! pattern
    xs
    |> Seq.tryExactlyOne
    |> Option.defaultWith (fun () ->
        xs
        |> List.ofSeq
        |> failwithf "'%s' expected exactly one but:\n%A" pattern
    )

let testProjName = "Tests"
let testProjPath = f testProjName
let testsProjDir = Path.getDirectory testProjPath
let mainProjName = "Fake.Fable"
let mainProjPath = f mainProjName
let mainProjDir = Path.getDirectory mainProjPath

let deployDir = Path.getFullName "./deploy"
// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------
open Fake.DotNet

let dotnet cmd workingDir =
    let result = DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

module XmlText =
    let escape rawText =
        let doc = new System.Xml.XmlDocument()
        let node = doc.CreateElement("root")
        node.InnerText <- rawText
        node.InnerXml
// --------------------------------------------------------------------------------------
// Targets
// --------------------------------------------------------------------------------------

let cleanBinAndObj projectPath =
    Shell.cleanDirs [
        projectPath </> "bin"
        projectPath </> "obj"
    ]

Target.create "MainClean" (fun _ ->
    cleanBinAndObj mainProjDir
)

Target.create "TestsClean" (fun _ ->
    cleanBinAndObj testsProjDir
)

Target.create "DeployClean" (fun _ ->
    Shell.cleanDir deployDir
)

Target.create "Meta" (fun _ ->
    let release = ReleaseNotes.load "RELEASE_NOTES.md"

    [
        "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
        "<ItemGroup>"
        "    <PackageReference Include=\"Microsoft.SourceLink.GitHub\" Version=\"1.0.0\" PrivateAssets=\"All\"/>"
        "</ItemGroup>"
        "<PropertyGroup>"
        "    <EmbedUntrackedSources>true</EmbedUntrackedSources>"
        "    <PackageProjectUrl>https://github.com/gretmn102/Fake.Fable</PackageProjectUrl>"
        "    <PackageLicenseExpression>MIT</PackageLicenseExpression>"
        "    <RepositoryUrl>https://github.com/gretmn102/Fake.Fable.git</RepositoryUrl>"
        sprintf "    <PackageReleaseNotes>%s</PackageReleaseNotes>"
            (String.concat "\n" release.Notes |> XmlText.escape)
        "    <PackageTags>fake;fable;fsharp</PackageTags>"
        "    <Authors>Fering</Authors>"
        sprintf "    <Version>%s</Version>" (string release.SemVer)
        "</PropertyGroup>"
        "</Project>"
    ]
    |> File.write false "Directory.Build.props"
)

let commonBuildArgs = "-c Release"

Target.create "MainBuild" (fun _ ->
    mainProjDir
    |> dotnet (sprintf "build %s" commonBuildArgs)
)

Target.create "Deploy" (fun _ ->
    mainProjDir
    |> dotnet (sprintf "build %s -o \"%s\"" commonBuildArgs deployDir)
)

Target.create "Pack" (fun _ ->
    mainProjDir
    |> dotnet (sprintf "pack %s -o \"%s\"" commonBuildArgs deployDir)
)

Target.create "PushToGitlab" (fun _ ->
    let packPathPattern = sprintf "%s/*.nupkg" deployDir
    let packPath =
        !! packPathPattern |> Seq.tryExactlyOne
        |> Option.defaultWith (fun () -> failwithf "'%s' not found" packPathPattern)

    deployDir
    |> dotnet (sprintf "nuget push -s %s %s" "gitlab" packPath)
)

Target.create "TestsBuild" (fun _ ->
    testsProjDir
    |> dotnet (sprintf "build %s" commonBuildArgs)
)

Target.create "TestsRun" (fun _ ->
    testsProjDir
    |> dotnet (sprintf "run %s" commonBuildArgs)
)

Target.create "Build" (fun _ -> ())

Target.create "Clean" (fun _ -> ())

Target.create "CleanBuild" (fun _ -> ())

// --------------------------------------------------------------------------------------
// Build order
// --------------------------------------------------------------------------------------
open Fake.Core.TargetOperators

"MainClean" ==> "Clean"
"TestsClean" ==> "Clean"

"DeployClean" ==> "Deploy"
"MainClean" ==> "Deploy"
"DeployClean" ?=> "MainClean"

"DeployClean" ==> "Pack"
"MainClean" ==> "Pack"
"Meta" ==> "Pack"
"Pack" ==> "PushToGitlab"

"MainBuild" ==> "Build"
"TestsBuild" ==> "Build"
"MainBuild" ?=> "TestsBuild"

"Clean" ==> "CleanBuild"
"Build" ==> "CleanBuild"
"Clean" ?=> "MainBuild"
"Clean" ?=> "TestsBuild"
"Clean" ?=> "Build"

Target.runOrDefault "Deploy"
