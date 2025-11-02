module LizardGenFs.Cs
open LizardGenFs.Types
open LizardGenFs.Util

let private nl = System.Environment.NewLine

let private backingTypeName t =
    match t with
    | EnumBackingType.Int8   -> "sbyte"
    | EnumBackingType.Int16  -> "short"
    | EnumBackingType.Int32  -> "int"
    | EnumBackingType.UInt8  -> "byte"
    | EnumBackingType.UInt16 -> "ushort"
    | EnumBackingType.UInt32 -> "uint"

let private basicTypeName t =
    match t with
    | Void   -> "void"
    | Bool   -> "bool"
    | Int8   -> "sbyte"
    | Int16  -> "short"
    | Int32  -> "int"
    | UInt8  -> "byte"
    | UInt16 -> "ushort"
    | UInt32 -> "uint"
    | String -> "string"

let rec private typeName =
    function
    | Basic b   -> basicTypeName b
    | Enum e    -> e.name
    | Struct s  -> s.name
    | Array (Basic UInt8) -> $"byte[]"
    | Array a   -> $"List<{typeName a}>"
    | Service _ -> failwith "Services cannot be members of structs"

let rec private typeNameN =
    function
    | Basic String -> "string?"
    | Basic b   -> basicTypeName b
    | Enum e    -> e.name
    | Struct s  -> s.name + "?"
    | Array (Basic UInt8) -> $"byte[]?"
    | Array a   -> $"List<{typeName a}>?"
    | Service _ -> failwith "Services cannot be members of structs"

let private paramTypeName =
    function
    | Basic   b -> basicTypeName b
    | Enum    e -> e.name
    | Struct  s -> s.name
    | Array   (Basic UInt8) -> $"byte[]"
    | Array   a -> $"List<{typeName a}>"
    | Service _ -> failwith "Services cannot be service method return types"

let private returnTypeName =
    function
    | Basic   b -> basicTypeName b
    | Enum    e -> e.name
    | Struct  s -> s.name
    | Array   (Basic UInt8) -> $"byte[]"
    | Array   a -> $"List<{typeName a}>"
    | Service _ -> failwith "Services cannot be service method parameters"

let private pascalCase (name: string) =
    // Convert snake_case to PascalCase
    name.Split([| '_' |], System.StringSplitOptions.RemoveEmptyEntries)
    |> Array.map (fun part ->
        if part.Length = 0 then ""
        else part.Substring(0, 1).ToUpperInvariant() + part.Substring(1)
    )
    |> String.concat ""

let private camelCase (name: string) =
    // Convert snake_case to camelCase
    name.Split([| '_' |], System.StringSplitOptions.RemoveEmptyEntries)
    |> Array.mapi (fun i part ->
        if part.Length = 0 then ""
        else
            if i = 0 then part
            else part.Substring(0, 1).ToUpperInvariant() + part.Substring(1)
    )
    |> String.concat ""

let private generateEnum ns (e : EnumDef) =
    seq {
        yield $$"""namespace {{ns}};

public enum {{e.name}} : {{backingTypeName e.backingType}}
{
"""

        let lines =
            e.values
            |> List.map (fun (name, v) -> $"    {name} = {v}")

        yield String.concat ("," + nl) lines
        yield """
}

"""
    } |> String.concat ""

let private memberSerdes s name t = // s=ISerdes name, name=member name, t=member type
    match t with
    | Basic Int8   -> $"{name} = {s}.Int8(nameof({name}), {name})"
    | Basic Int16  -> $"{name} = {s}.Int16(nameof({name}), {name})"
    | Basic Int32  -> $"{name} = {s}.Int32(nameof({name}), {name})"
    | Basic UInt8  -> $"{name} = {s}.UInt8(nameof({name}), {name})"
    | Basic UInt16 -> $"{name} = {s}.UInt16(nameof({name}), {name})"
    | Basic UInt32 -> $"{name} = {s}.UInt32(nameof({name}), {name})"
    | Basic Bool   -> $"{name} = {s}.Bool8(nameof({name}), {name})"
    | Basic String -> $"{name} = {s}.String32Utf8(nameof({name}), {name})"
    | Enum e ->
        match e.backingType with
        | EnumBackingType.Int8   -> $"{name} = {s}.Enum8(nameof({name}), {name})"
        | EnumBackingType.Int16  -> $"{name} = {s}.Enum16(nameof({name}), {name})"
        | EnumBackingType.Int32  -> $"{name} = {s}.Enum32(nameof({name}), {name})"
        | EnumBackingType.UInt8  -> $"{name} = {s}.EnumU8(nameof({name}), {name})"
        | EnumBackingType.UInt16 -> $"{name} = {s}.EnumU16(nameof({name}), {name})"
        | EnumBackingType.UInt32 -> $"{name} = {s}.EnumU32(nameof({name}), {name})"
    | Struct t -> $"{name} = {pascalCase t.name}.Serdes(nameof({name}), {name}, {s})"
    | Array (Basic UInt8) ->
        $$"""{{name}} = {{s}}.Bytes32(nameof({{name}}), {{name}})"""
    | Array t ->
        seq {
            yield $"{name} = {s}.List32(nameof({name}), {name}, {t}.Serdes)"
        } |> String.concat ""
    | Service _ -> failwith "Services cannot be members of structs"
    | _ -> failwith "Unsupported"

let private generateStruct ns (s : StructDef) =
    seq {
        yield $$"""using SerdesNet;
#nullable enable

namespace {{ns}};

public class {{s.name}}
{
"""

        for (name, t) in s.members do
            yield $$"""    public {{typeNameN t}} {{pascalCase name}};
"""

        yield $$"""
    public static {{s.name}} Serdes(SerdesName name, {{s.name}}? value, ISerdes s)
    {
        value ??= new {{s.name}}();
        s.Begin(name);
"""

        for (name, t) in s.members do
            let valName = "value." + (pascalCase name)
            yield indentText 2 (memberSerdes "s" valName t)
            yield ";" + nl

        yield """        s.End();
        return value;
    }
}

"""
    } |> String.concat ""

let private generateInterface ns (s : ServiceDef) =
    seq {
        yield $$"""namespace {{ns}};

public interface I{{s.name}}
{"""

        for m in s.methods do
            let args =
                m.parameters
                |> List.map (fun (p, pt) -> $"{paramTypeName pt} {camelCase p}")
                |> String.concat ", "

            yield $"    {returnTypeName m.returnType} {m.name}({args});"

        yield "}" + nl
    } |> String.concat nl

let private generateInterfaceMessageEnum ns (s : ServiceDef) =
    seq {
        yield $$"""namespace {{ns}};

public enum {{s.name}}Message : ushort
{
    Unknown = 0,"""
        let offset = 1
        let lines =
            s.methods
            |> List.mapi (fun i m -> $"    {m.name} = {i + offset}")

        yield String.concat ("," + nl) lines
        yield """}
"""
    } |> String.concat nl

let private generateSerializerMethod (s : ServiceDef) (m : MethodDef) =
    let paramSerdes (s:string) name t = // s=ISerdes name, name=parameter name, t=parameter type
        match t with
        | Basic Int8   -> $"{s}.Int8(nameof({name}), {name})"
        | Basic Int16  -> $"{s}.Int16(nameof({name}), {name})"
        | Basic Int32  -> $"{s}.Int32(nameof({name}), {name})"
        | Basic UInt8  -> $"{s}.UInt8(nameof({name}), {name})"
        | Basic UInt16 -> $"{s}.UInt16(nameof({name}), {name})"
        | Basic UInt32 -> $"{s}.UInt32(nameof({name}), {name})"
        | Basic Bool   -> $"{s}.Bool8(nameof({name}), {name})"
        | Basic String -> $"{s}.String32Utf8(nameof({name}), {name})"
        | Enum e ->
            match e.backingType with
            | EnumBackingType.Int8   -> $"{s}.Enum8(nameof({name}), {name})"
            | EnumBackingType.Int16  -> $"{s}.Enum16(nameof({name}), {name})"
            | EnumBackingType.Int32  -> $"{s}.Enum32(nameof({name}), {name})"
            | EnumBackingType.UInt8  -> $"{s}.EnumU8(nameof({name}), {name})"
            | EnumBackingType.UInt16 -> $"{s}.EnumU16(nameof({name}), {name})"
            | EnumBackingType.UInt32 -> $"{s}.EnumU32(nameof({name}), {name})"
        | Struct t -> $"{pascalCase t.name}.Serdes(nameof({name}), {name}, {s})"
        | Array (Basic UInt8) -> $"{s}.Bytes32(nameof({name}), {name})"
        | Array t             -> $"{s}.List32(nameof({name}), {name}, {t}.Serdes)"
        | Service _ -> failwith "Services cannot be members of structs"
        | _ -> failwith "Unsupported"

    let resultSerdes (m : MethodDef) s =
        let tname = returnTypeName m.returnType
        match m.returnType with
        | Basic Void -> ""
        | Basic Int8   -> $"{tname} result = {s}.Int8(\"result\", 0)"
        | Basic Int16  -> $"{tname} result = {s}.Int16(\"result\", 0)"
        | Basic Int32  -> $"{tname} result = {s}.Int32(\"result\", 0)"
        | Basic UInt8  -> $"{tname} result = {s}.UInt8(\"result\", 0)"
        | Basic UInt16 -> $"{tname} result = {s}.UInt16(\"result\", 0)"
        | Basic UInt32 -> $"{tname} result = {s}.UInt32(\"result\", 0)"
        | Basic Bool   -> $"{tname} result = {s}.Bool8(\"result\", false)"
        | Basic String -> $"{tname} result = {s}.String32Utf8(\"result\", \"\")"
        | Enum e ->
            match e.backingType with
            | EnumBackingType.Int8   -> $"{tname} result = {s}.Enum8(\"result\", default)"
            | EnumBackingType.Int16  -> $"{tname} result = {s}.Enum16(\"result\", default)"
            | EnumBackingType.Int32  -> $"{tname} result = {s}.Enum32(\"result\", default)"
            | EnumBackingType.UInt8  -> $"{tname} result = {s}.EnumU8(\"result\", default)"
            | EnumBackingType.UInt16 -> $"{tname} result = {s}.EnumU16(\"result\", default)"
            | EnumBackingType.UInt32 -> $"{tname} result = {s}.EnumU32(\"result\", default)"
        | Struct _ -> $"{tname} result = {tname}.Serdes(\"result\", null, {s})"
        | Array (Basic UInt8) ->
            $"{tname} result = {s}.Bytes32(\"result\", null)"
        | Array t ->
            $"{tname} result = {s}.List32<{typeName t}>(\"result\", null, {typeName t}.Serdes)"
        | Service _ -> failwith "Services cannot be method return values"

    seq {
        let args =
            m.parameters
            |> List.map (fun (p, pt) -> $"{paramTypeName pt} {camelCase p}")
            |> String.concat ", "

        yield $$"""    public {{returnTypeName m.returnType}} {{m.name}}({{args}})
    {
        var sw = Header({{s.name}}Message.{{m.name}});"""

        for (p, pt) in m.parameters do
            yield $$"""        {{(paramSerdes "sw" (camelCase p) pt)}};""";

        if (m.returnType = Basic Void) then
            yield "        _ = new MemoryReaderSerdes(_send(sw.GetMemory()));"
        else
            yield $$"""        var sr = new MemoryReaderSerdes(_send(sw.GetMemory()));

        {{resultSerdes m "sr"}};
        return result;"""

        yield "    }"
    } |> String.concat nl

let private generateSerializer ns (s : ServiceDef) =
    seq {
        yield $$"""using SerdesNet;
#nullable enable

namespace {{ns}};

public sealed class {{s.name}}Serializer : I{{s.name}}
{
    readonly PacketHandler _send;

    public {{s.name}}Serializer(PacketHandler send)
    {
        _send = send ?? throw new ArgumentNullException(nameof(send));
    }

    static MemoryWriterSerdes Header({{s.name}}Message type)
    {
        MemoryWriterSerdes sw = new();
        sw.EnumU16(nameof(type), type);
        return sw;
    }"""

        for m in s.methods do
            yield ""
            yield generateSerializerMethod s m

        yield "}"
    } |> String.concat nl

let private generateDeserializerMethod (s : ServiceDef) (m : MethodDef) =
    let paramSerdes (s:string) name t = // s=ISerdes name, name=parameter name, t=parameter type
        let tname = paramTypeName t
        match t with
        | Basic Int8   -> $"{tname} {name} = {s}.Int8(\"{name}\", 0)"
        | Basic Int16  -> $"{tname} {name} = {s}.Int16(\"{name}\", 0)"
        | Basic Int32  -> $"{tname} {name} = {s}.Int32(\"{name}\", 0)"
        | Basic UInt8  -> $"{tname} {name} = {s}.UInt8(\"{name}\", 0)"
        | Basic UInt16 -> $"{tname} {name} = {s}.UInt16(\"{name}\", 0)"
        | Basic UInt32 -> $"{tname} {name} = {s}.UInt32(\"{name}\", 0)"
        | Basic Bool   -> $"{tname} {name} = {s}.Bool8(\"{name}\", false)"
        | Basic String -> $"{tname} {name} = {s}.String32Utf8(\"{name}\", 0)"
        | Enum e ->
            match e.backingType with
            | EnumBackingType.Int8   -> $"{tname} {name} = {s}.Enum8<{tname}>(\"{name}\", default)"
            | EnumBackingType.Int16  -> $"{tname} {name} = {s}.Enum16<{tname}>(\"{name}\", default)"
            | EnumBackingType.Int32  -> $"{tname} {name} = {s}.Enum32<{tname}>(\"{name}\", default)"
            | EnumBackingType.UInt8  -> $"{tname} {name} = {s}.EnumU8<{tname}>(\"{name}\", default)"
            | EnumBackingType.UInt16 -> $"{tname} {name} = {s}.EnumU16<{tname}>(\"{name}\", default)"
            | EnumBackingType.UInt32 -> $"{tname} {name} = {s}.EnumU32<{tname}>(\"{name}\", default)"
        | Struct t -> $"{tname} {name} = {pascalCase t.name}.Serdes(\"{name}\", null, {s})"
        | Array (Basic UInt8) -> $"{tname} {name} = {s}.Bytes32(\"{name}\", null)"
        | Array t             -> $"{tname} {name} = {s}.List32(\"{name}\", null, {t}.Serdes)"
        | Service _ -> failwith "Services cannot be members of structs"
        | _ -> failwith "Unsupported"

    let resultSerdes (m : MethodDef) s =
        let tname = returnTypeName m.returnType
        match m.returnType with
        | Basic Void -> ""
        | Basic Int8   -> $"{s}.Int8(nameof(result), result)"
        | Basic Int16  -> $"{s}.Int16(nameof(result), result)"
        | Basic Int32  -> $"{s}.Int32(nameof(result), result)"
        | Basic UInt8  -> $"{s}.UInt8(nameof(result), result)"
        | Basic UInt16 -> $"{s}.UInt16(nameof(result), result)"
        | Basic UInt32 -> $"{s}.UInt32(nameof(result), result)"
        | Basic Bool   -> $"{s}.Bool8(nameof(result), result)"
        | Basic String -> $"{s}.String32Utf8(nameof(result), result)"
        | Enum e ->
            match e.backingType with
            | EnumBackingType.Int8   -> $"{s}.Enum8(nameof(result), result)"
            | EnumBackingType.Int16  -> $"{s}.Enum16(nameof(result), result)"
            | EnumBackingType.Int32  -> $"{s}.Enum32(nameof(result), result)"
            | EnumBackingType.UInt8  -> $"{s}.EnumU8(nameof(result), result)"
            | EnumBackingType.UInt16 -> $"{s}.EnumU16(nameof(result), result)"
            | EnumBackingType.UInt32 -> $"{s}.EnumU32(nameof(result), result)"
        | Struct _ -> $"{tname}.Serdes(nameof(result), result, {s})"
        | Array (Basic UInt8) ->
            $"{s}.Bytes32(nameof(result), result)"
        | Array t ->
            $"{s}.List32(nameof(result), result, {typeName t}.Serdes)"
        | Service _ -> failwith "Services cannot be method return values"

    seq {
        let args =
            m.parameters
            |> List.map (fun (p, pt) -> $"{camelCase p}")
            |> String.concat ", "

        yield $$"""    void Handle{{m.name}}(ISerdes sr, ISerdes sw)
    {"""
        for (p, pt) in m.parameters do
            yield $$"""        {{(paramSerdes "sw" (camelCase p) pt)}};""";

        if (m.returnType = Basic Void) then
            yield $$"""        _receiver.{{m.name}}({{args}});"""
        else
            yield $$"""        var result = _receiver.{{m.name}}({{args}});
        {{resultSerdes m "sr"}};"""

        yield "    }"
    } |> String.concat nl

let private generateDeserializer ns (s : ServiceDef) =
    seq {
        yield $$"""using SerdesNet;
#nullable enable

namespace {{ns}};

public sealed class {{s.name}}Deserializer
{
    readonly I{{s.name}} _receiver;

    public {{s.name}}Deserializer(I{{s.name}} receiver)
    {
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
    }

    public ReadOnlyMemory<byte> HandleMessage(ReadOnlyMemory<byte> buffer)
    {
        MemoryReaderSerdes sr = new(buffer);
        var type = sr.EnumU16<{{s.name}}Message>("type", default);

        MemoryWriterSerdes sw = new();
        switch (type)
        {"""

        for m in s.methods do
            yield $$"""            case {{s.name}}Message.{{m.name}}: Handle{{m.name}}(sr, sw); break;"""

        yield """        }

        return sw.GetMemory();
    }"""

        for m in s.methods do
            yield ""
            yield generateDeserializerMethod s m

        yield "}"
    } |> String.concat nl

let private generateType (ns : string) (t : TypeDef)  : GeneratedFile seq =
    match t with
    | Enum e -> [{ name = $"{e.name}.g.cs"; text = generateEnum ns e }]
    | Struct s -> [{ name = $"{s.name}.g.cs"; text = generateStruct ns s }]
    | Service s ->
        [
            { name = $"{s.name}Message.g.cs"; text = generateInterfaceMessageEnum ns s }
            { name = $"I{s.name}.g.cs"; text = generateInterface ns s }
            { name = $"{s.name}Serializer.g.cs"; text = generateSerializer ns s }
            { name = $"{s.name}Deserializer.g.cs"; text = generateDeserializer ns s }
        ]
    | _ -> failwithf "Unsupported type %A" t

let generate ns types : GeneratedFile list =
    types
    |> Seq.map (generateType ns)
    |> Seq.concat
    |> List.ofSeq
    |> List.map (fun gf -> { gf with text =  "// Generated by LizardGenFs" + nl + gf.text })
