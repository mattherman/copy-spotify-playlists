module Error

open Giraffe.ViewEngine

let view error =
    Layout.view [
        h1 [] [ str "Error" ]
        pre [] [ str error ]
    ]
