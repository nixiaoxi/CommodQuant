﻿#r "bin/Debug/netstandard2.0/CommodLib.dll"
open System
open Commod.Utils

let addPath dir =
    let entries = 
        Environment.GetEnvironmentVariable("Path").Split(';')
        |> Array.filter( fun x -> x <> dir)
        |> Array.filter( fun x -> x <> @"C:\Commodities\bin")
        |> Array.append( [|dir|] ) //add to the front
    let path = String.Join(";", entries)
    Environment.SetEnvironmentVariable("Path", path, EnvironmentVariableTarget.User)

let dir = Environment.GetEnvironmentVariable("USERPROFILE") 
            + @"\OneDrive - Pavilion Energy\Commodities\bin"
            +/ "OneDrive - Pavilion Energy" 
            +/ "Commodities" 
            +/ "bin"
Environment.SetEnvironmentVariable("COMMODITIES", dir, EnvironmentVariableTarget.User)
addPath dir
