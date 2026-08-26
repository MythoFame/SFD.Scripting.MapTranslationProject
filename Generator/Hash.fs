module Hash

open System.Text

/// FNV-1a 64-bit over the UTF-8 bytes of `kindTag + "\u001F" + original`.
let compute (kindTag: string) (original: string) : uint64 =
    let prime = 0x100000001b3UL
    let bytes = Encoding.UTF8.GetBytes(kindTag + "\u001F" + original)
    let mutable h = 0xcbf29ce484222325UL
    for b in bytes do
        h <- (h ^^^ uint64 b) * prime
    h

let toHex (h: uint64) : string = h.ToString("x16")

let toLiteral (h: uint64) : string = "0x" + h.ToString("x16") + "UL"
