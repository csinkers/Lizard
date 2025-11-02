module LizardGenFs.Parse

open FParsec
open Types

type UserState = unit
type Parser<'t> = Parser<'t, UserState>

// --- Comment Parsers ---
let lineComment : Parser<unit> =
    pstring "//" >>. skipRestOfLine true

let blockComment : Parser<unit> =
    pstring "/*" >>. skipManyTill anyChar (pstring "*/")
    |>> fun _ -> ()

let comment : Parser<unit> =
    (attempt lineComment) <|> (attempt blockComment)

// --- Whitespace and Comments Parser ---
let ws : Parser<unit> =
    skipMany (spaces1 <|> comment)

let strWs s = pstring s .>> ws

let identifier : Parser<string> =
    many1Satisfy2L
        isLetter
        (fun c -> isLetter c || isDigit c || c = '_')
        "identifier"
    .>> ws

let convertToInt64 (s: string) : string =
    match System.Int64.TryParse(s) with
    | true, v -> v.ToString()
    | _ -> failwithf "Invalid integer value: %s" s

let convertToInt64Hex (s: string) : string =
    match System.Int64.TryParse(s, System.Globalization.NumberStyles.HexNumber, null) with
    | true, v -> v.ToString()
    | _ -> failwithf "Invalid hexadecimal value: %s" s

let numeric : Parser<string> =
    ((pstring "0x" >>. many1Satisfy isHex |>> convertToInt64Hex ) <|> (many1Satisfy isDigit |>> convertToInt64))
    .>> ws

let typeRef : Parser<string> =
    identifier .>>. opt (strWs "[]") .>> ws
    |>> fun (n, brackets) ->
        match brackets with
        | Some _ -> n + "[]"
        | None   -> n

// Enum parser
let enumValue : Parser<(string * string option)> =
    identifier .>>. opt (ws >>. strWs "=" >>. numeric) .>> opt (strWs ",")

let enumDef : Parser<RawEnumDef> =
    strWs "enum" >>. between (strWs "<") (strWs ">") typeRef .>>. identifier
    .>>. between (strWs "{") (strWs "}") (many enumValue)
    |>> fun ((backingType, name), values) ->
        { name = name; backingType = backingType; values = values }

// Struct parser
let structMember : Parser<(string * string)> =
    typeRef .>>. identifier .>> strWs ";"

let structDef : Parser<RawStructDef> =
    strWs "struct" >>. identifier
    .>>. between (strWs "{") (strWs "}") (many structMember)
    |>> fun (name, members) -> { name = name; members = members }

// Method parser
let param : Parser<(string * string)> =
    typeRef .>>. identifier

let paramList : Parser<(string * string) list> =
    between
        (strWs "(")
        (strWs ")")
        (sepBy param (strWs ","))
    .>> ws

let methodDef : Parser<RawMethodDef> =
    typeRef .>>. identifier .>>. paramList .>> strWs ";"
    |>> fun ((ret, name), args) -> { name = name; returnType = ret; parameters = args }

// Service parser (client/server)
let clientDef : Parser<RawServiceDef> =
    strWs "client" >>. identifier
    .>>. between
            (strWs "{")
            (strWs "}")
            (many methodDef)
    |>> fun (name, methods) ->
        { name = name; isClient = true; callbackService = None; methods = methods }

let serverDef : Parser<RawServiceDef> =
    strWs "server"
    >>. opt (between (strWs "<") (strWs ">") identifier)
    .>>. identifier
    .>>. between (strWs "{") (strWs "}") (many methodDef)
    |>> fun (((cb, name), methods)) ->
        { name = name; isClient = false; callbackService = cb; methods = methods }

let statement : Parser<RawTypeDef> =
    (      enumDef |>> RawTypeDef.Enum)
    <|> (structDef |>> RawTypeDef.Struct)
    <|> (clientDef |>> RawTypeDef.Service)
    <|> (serverDef |>> RawTypeDef.Service)

let protocolFile: Parser<RawTypeDef list> =
    ws >>. many statement .>> eof
