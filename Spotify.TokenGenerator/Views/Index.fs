module Index

open Giraffe.ViewEngine

let view =
    Layout.view [
        a [ _href "/login" ] [ str "Generate Token" ]
    ]
