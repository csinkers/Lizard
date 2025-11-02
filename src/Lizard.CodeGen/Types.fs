module LizardGenFs.Types

type EnumBackingType =
    | Int8
    | Int16
    | Int32
    | UInt8
    | UInt16
    | UInt32

type BasicType =
    | Void
    | Bool
    | Int8
    | Int16
    | Int32
    | UInt8
    | UInt16
    | UInt32
    | String

// All the 'Raw' types are used as the output of the parser and the input to the type checker, which then converts them to actual TypeDef types.

type TypeReference = string
type RawEnumDef =
    {
        name : string
        backingType : TypeReference
        values : (string * string option) list
    }

type RawStructDef =
    {
        name : string
        members : (string * TypeReference) list
    }

type RawMethodDef =
    {
        name : string
        returnType : TypeReference
        parameters : (string * TypeReference) list
    }

type RawServiceDef =
    {
        name : string
        isClient : bool
        callbackService : string option
        methods : RawMethodDef list
    }

type RawTypeDef =
    | Array of RawTypeDef
    | Basic of BasicType
    | Enum of RawEnumDef
    | Struct of RawStructDef
    | Service of RawServiceDef

type EnumDef =
    {
        name : string
        backingType : EnumBackingType
        values : (string * int64) list
    }

type TypeDef =
    | Basic of BasicType
    | Enum of EnumDef
    | Struct of StructDef
    | Array of TypeDef
    | Service of ServiceDef
and StructDef =
    {
        name : string
        members : (string * TypeDef) list
    }
and MethodDef =
    {
        name : string
        returnType : TypeDef
        parameters : (string * TypeDef) list
    }
and ServiceDef =
    {
        name : string
        isClient : bool
        callbackService : TypeDef option
        methods : MethodDef list
    }

type GeneratedFile =
    {
        name : string
        text : string
    }
