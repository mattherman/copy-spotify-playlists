module Error

open Giraffe.ViewEngine

let view error =
    html [] [
        head [] [
            title [] [ str "Spotify Token Generator" ]
        ]
        body [] [
            h1 [] [ str "Error" ]
            pre [] [ str error ]
        ]
    ]
