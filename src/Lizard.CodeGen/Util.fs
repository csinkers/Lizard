module LizardGenFs.Util
open LizardGenFs.Types

let reverseDependencyOrder (types : TypeDef list) =
    // Build a map from type name to TypeDef
    let rec typeName =
        function
        | TypeDef.Enum e    -> e.name
        | TypeDef.Struct s  -> s.name
        | TypeDef.Service s -> s.name
        | TypeDef.Basic b   -> string b
        | TypeDef.Array a   -> typeName a

    let typeOrder =
        function
        | TypeDef.Enum _    -> 0
        | TypeDef.Struct _  -> 1
        | TypeDef.Service _ -> 2
        | _                 -> 3

    let allTypes =
        types
        |> List.map (fun t -> typeName t, t)
        |> Map.ofList

    // Get direct dependencies by name
    let rec deps t =
        match t with
        | TypeDef.Basic _   -> []
        | TypeDef.Enum _    -> []
        | TypeDef.Struct s  -> s.members |> List.map snd |> List.map typeName
        | TypeDef.Array a   -> [typeName a]
        | TypeDef.Service s -> s.methods |> List.collect (fun m -> m.parameters |> List.map snd |> List.map typeName)
    
    // Build dependency graph
    let depMap =
        allTypes
        |> Map.map (fun _ t -> deps t |> List.filter allTypes.ContainsKey)

    // Kahn's algorithm with per-level sorting
    let rec kahn (depMap : Map<string, list<string>>) (result : TypeDef list) =
        let (noDeps, rest) =
            depMap
            |> Map.partition (fun _ ds -> ds.IsEmpty)

        if Map.isEmpty noDeps then
            if Map.isEmpty rest then
                List.rev result
            else
                // If there are nodes left but none with zero deps, we have a cycle
                let cycle = rest |> Map.toList |> List.map fst
                failwithf "Circular dependency detected: %A" cycle
        else
            // Sort nodes with no dependencies by type and name
            let sorted =
                noDeps
                |> Map.toList
                |> List.map (fun (n,_) -> allTypes.[n])
                |> List.sortBy (fun t -> (typeOrder t, typeName t))

            // Remove these from the graph
            let depMap' =
                rest
                |> Map.map (fun _ ds -> ds |> List.filter (fun d -> not (noDeps.ContainsKey d)))
            kahn depMap' (List.rev sorted @ result)
    kahn depMap []

let indentText (indent : int) (text : string) =
    let indentation = String.replicate indent "    "
    text.Split('\n')
    |> Array.map (fun line -> indentation + line)
    |> String.concat "\n"

