module LizardGenFs.CppCommon
open LizardGenFs.Types

let backingTypeName t =
    match t with
    | EnumBackingType.Int8   -> "int8_t"
    | EnumBackingType.Int16  -> "int16_t"
    | EnumBackingType.Int32  -> "int32_t"
    | EnumBackingType.UInt8  -> "uint8_t"
    | EnumBackingType.UInt16 -> "uint16_t"
    | EnumBackingType.UInt32 -> "uint32_t"

let basicTypeName t =
    match t with
    | Void   -> "void"
    | Bool   -> "bool"
    | Int8   -> "int8_t"
    | Int16  -> "int16_t"
    | Int32  -> "int32_t"
    | UInt8  -> "uint8_t"
    | UInt16 -> "uint16_t"
    | UInt32 -> "uint32_t"
    | String -> "std::string"

let rec typeName =
    function
    | Basic b   -> basicTypeName b
    | Enum e    -> e.name
    | Struct s  -> s.name
    | Array a   -> $"std::vector<{typeName a}>"
    | Service _ -> failwith "Services cannot be members of structs"

let paramTypeName =
    function
    | Basic   b -> basicTypeName b
    | Enum    e -> e.name
    | Struct  s -> $"{s.name}&"
    | Array   a -> $"std::vector<{typeName a}>&"
    | Service _ -> failwith "Services cannot be service method return types"

let returnTypeName =
    function
    | Basic   b -> basicTypeName b
    | Enum    e -> e.name
    | Struct  s -> s.name
    | Array   a -> $"std::vector<{typeName a}>"
    | Service _ -> failwith "Services cannot be service method parameters"

let serdesCall s name t = // s=ISerdes name, name=member name, t=member type
    match t with
    | Basic Int8   -> $"{s}.Int8(\"{name}\", {name})"
    | Basic Int16  -> $"{s}.Int16(\"{name}\", {name})"
    | Basic Int32  -> $"{s}.Int32(\"{name}\", {name})"
    | Basic UInt8  -> $"{s}.UInt8(\"{name}\", {name})"
    | Basic UInt16 -> $"{s}.UInt16(\"{name}\", {name})"
    | Basic UInt32 -> $"{s}.UInt32(\"{name}\", {name})"
    | Basic Bool   -> $"{s}.Bool(\"{name}\", {name})"
    | Basic String -> $"{s}.String(\"{name}\", {name})"
    | Enum e ->
        match e.backingType with
        | EnumBackingType.Int8   -> $"{s}.Int8Enum(\"{name}\", {name})"
        | EnumBackingType.Int16  -> $"{s}.Int16Enum(\"{name}\", {name})"
        | EnumBackingType.Int32  -> $"{s}.Int32Enum(\"{name}\", {name})"
        | EnumBackingType.UInt8  -> $"{s}.UInt8Enum(\"{name}\", {name})"
        | EnumBackingType.UInt16 -> $"{s}.UInt16Enum(\"{name}\", {name})"
        | EnumBackingType.UInt32 -> $"{s}.UInt32Enum(\"{name}\", {name})"
    | Struct _ -> $"{name}.Serdes(\"{name}\", {s})"
    | Array (Basic UInt8) -> $"{s}.Bytes(\"{name}\", {name})"
    | Array t ->
        seq {
            yield ($"{s}.Array(\"{name}\", {name})" )
        } |> String.concat ""
    | Service _ -> failwith "Services cannot be members of structs"
    | _ -> failwith "Unsupported"

