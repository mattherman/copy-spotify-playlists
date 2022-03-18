module Token

open Giraffe.ViewEngine
open Shared

let view (accessToken: string) (refreshToken: string) =
    html [] [
        head [] [
            title [] [ str "Spotify Token Generator" ]
        ]
        body [] [
            h1 [] [ str "Logged In" ]
            h3 [] [ str "Access Token" ]
            pre [] [ str accessToken ]
            a [ _href $"/refresh?token={refreshToken}" ] [ str "Refresh Token" ]
        ]
    ]
