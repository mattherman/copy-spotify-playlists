module Layout

open Giraffe.ViewEngine

let view bodyElements =
    html [] [
        head [] [
            title [] [ str "Spotify Token Generator" ]
        ]
        body [] bodyElements
    ]
