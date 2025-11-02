module LizardGenFs.TypeCheck

open LizardGenFs.Types

exception TypeCheckError of string

let basicTypes =
    [
        "void", BasicType.Void
        "bool", BasicType.Bool
        "int8", BasicType.Int8
        "int16", BasicType.Int16
        "int32", BasicType.Int32
        "uint8", BasicType.UInt8
        "uint16", BasicType.UInt16
        "uint32", BasicType.UInt32
        "string", BasicType.String
    ] |> Map.ofList

let parseEnumBackingType (s : string) =
    match s with
    | "int8"   -> EnumBackingType.Int8
    | "int16"  -> EnumBackingType.Int16
    | "int32"  -> EnumBackingType.Int32
    | "uint8"  -> EnumBackingType.UInt8
    | "uint16" -> EnumBackingType.UInt16
    | "uint32" -> EnumBackingType.UInt32
    | _        -> raise (TypeCheckError $"Unknown enum backing type: {s}")

let tryParseEnumValue (s : string) : int64 option =
    if s.StartsWith("0x") then
        match System.Int64.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null) with
        | true, v -> Some v
        | _ -> None
    else
        match System.Int64.TryParse(s) with
        | true, v -> Some v
        | _ -> None

let rec resolveType (types : Map<string, TypeDef>) (name : string) : TypeDef =
    if name.EndsWith("[]") then
        let elemType = resolveType types (name.Substring(0, name.Length - 2))
        Array elemType
    elif basicTypes.ContainsKey(name) then
        Basic (basicTypes[name])
    elif types.ContainsKey(name) then
        types[name]
    else
        raise (TypeCheckError $"Unknown type: {name}")

let resolveEnum (types : Map<string, TypeDef>) (raw : RawEnumDef) : EnumDef =
    let backingType = parseEnumBackingType raw.backingType
    let mutable lastValue = -1L
    let values =
        raw.values
        |> List.map (fun (name, valueOpt) ->
            let value =
                match valueOpt with
                | Some s ->
                    match tryParseEnumValue s with
                    | Some v -> v
                    | None -> raise (TypeCheckError $"Invalid enum value: {s} in {raw.name}")
                | None -> lastValue + 1L
            lastValue <- value
            (name, value)
        )
    { name = raw.name; backingType = backingType; values = values }

let resolveStruct (types : Map<string, TypeDef>) (raw : RawStructDef) : StructDef =
    let members =
        raw.members
        |> List.map (fun (t, name) -> name, resolveType types t)
    { name = raw.name; members = members }

let resolveMethod (types : Map<string, TypeDef>) (raw : RawMethodDef) : MethodDef =
    let parameters =
        raw.parameters
        |> List.map (fun (t, name) -> name, resolveType types t)
    {
        name = raw.name
        returnType = resolveType types raw.returnType
        parameters = parameters
    }

let resolveService (types : Map<string, TypeDef>) (raw : RawServiceDef) : ServiceDef =
    let callbackService =
        match raw.callbackService with
        | Some cb -> Some (resolveType types cb)
        | None -> None
    let methods =
        raw.methods
        |> List.map (resolveMethod types)
    {
        name = raw.name
        isClient = raw.isClient
        callbackService = callbackService
        methods = methods
    }

let typecheck (rawTypes : RawTypeDef list) : TypeDef list =
    // First pass: collect all type names
    let rec collect (acc : Map<string, TypeDef>) (raw : RawTypeDef) =
        match raw with
        | RawTypeDef.Enum e ->
            if acc.ContainsKey(e.name) then
                raise (TypeCheckError $"Duplicate type: {e.name}")
            acc.Add(e.name, Unchecked.defaultof<_>) // placeholder
        | RawTypeDef.Struct s ->
            if acc.ContainsKey(s.name) then
                raise (TypeCheckError $"Duplicate type: {s.name}")
            acc.Add(s.name, Unchecked.defaultof<_>)
        | RawTypeDef.Service svc ->
            if acc.ContainsKey(svc.name) then
                raise (TypeCheckError $"Duplicate type: {svc.name}")
            acc.Add(svc.name, Unchecked.defaultof<_>)
        | RawTypeDef.Basic _ | RawTypeDef.Array _ -> acc // not used in input

    let typeNames = List.fold collect Map.empty rawTypes

    // Second pass: resolve all types
    let rec resolveAll (types : Map<string, TypeDef>) (raws : RawTypeDef list) (acc : Map<string, TypeDef>) =
        match raws with
        | [] -> acc
        | raw :: rest ->
            match raw with
            | RawTypeDef.Enum e ->
                let enumDef = TypeDef.Enum (resolveEnum acc e)
                resolveAll types rest (acc.Add(e.name, enumDef))
            | RawTypeDef.Struct s ->
                let structDef = TypeDef.Struct (resolveStruct acc s)
                resolveAll types rest (acc.Add(s.name, structDef))
            | RawTypeDef.Service svc ->
                let serviceDef = TypeDef.Service (resolveService acc svc)
                resolveAll types rest (acc.Add(svc.name, serviceDef))
            | RawTypeDef.Basic _ | RawTypeDef.Array _ -> resolveAll types rest acc

    let resolved = resolveAll typeNames rawTypes typeNames
    // Return in the same order as input
    rawTypes
    |> List.map (function
        | RawTypeDef.Enum e -> resolved[e.name]
        | RawTypeDef.Struct s -> resolved[s.name]
        | RawTypeDef.Service svc -> resolved[svc.name]
        | RawTypeDef.Basic _ | RawTypeDef.Array _ -> failwith "Unexpected RawTypeDef in input"
    )
