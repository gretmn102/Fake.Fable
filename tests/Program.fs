open Expecto

open Fake.Fable

[<EntryPoint>]
let main args =
    runTestsInAssembly defaultConfig args
