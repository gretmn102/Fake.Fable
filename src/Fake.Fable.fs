module Fake.Fable

open Fake.DotNet
open Fake.IO

let dotnet cmd workingDir =
    let result = DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then
        failwithf "'dotnet %s' failed in %s" cmd workingDir

type BuildOption =
    {
        Output: string
        IsWatch: bool
    }
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module BuildOption =
    let empty: BuildOption =
        {
            Output = ""
            IsWatch = false
        }

let run fableArgs =
    dotnet (sprintf "fable %s" (String.concat " " fableArgs)) "."

let build (fableOption: BuildOption) projectPath =
    [
        yield projectPath

        yield
            match fableOption.Output with
            | null | "" -> ""
            | output -> sprintf "-o %s" output

        yield
            if fableOption.IsWatch then
                "--watch"
            else
                ""
    ]
    |> run

let clean outputPath =
    if Shell.testDir outputPath then
        [
            "clean"
            sprintf "-o %s" outputPath
            "--yes"
        ]
        |> run
