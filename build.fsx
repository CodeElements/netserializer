#r "paket:
nuget Fake.IO.FileSystem
nuget Fake.DotNet.Cli
nuget Fake.Core.Target //"

#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO

let artifactsDir = "./artifacts"

Target.create "Cleanup" (fun _ ->
    Shell.cleanDir artifactsDir
)

Target.create "Build" (fun _ ->
    "./NetSerializer" |> DotNet.pack (fun opts -> {opts with Configuration = DotNet.BuildConfiguration.Release
                                                             OutputPath = Some artifactsDir
                                                         })
)

Target.create "All" ignore

open Fake.Core.TargetOperators

"Cleanup"
  ==> "Build"
  ==> "All"

Target.runOrDefault "All"