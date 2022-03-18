module Index

open Giraffe.ViewEngine

let view =
    html [] [
        head [] [
            title [] [ str "Spotify Token Generator" ]
        ]
        body [] [
            a [ _href "/login" ] [ str "Generate Token" ]
        ]
    ]
