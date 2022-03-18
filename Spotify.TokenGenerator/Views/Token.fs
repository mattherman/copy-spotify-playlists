module Token

open Giraffe.ViewEngine

let view (accessToken: string) (refreshToken: string) =
    Layout.view [
        h1 [] [ str "Logged In" ]
        h3 [] [ str "Access Token" ]
        pre [] [ str accessToken ]
        a [ _href $"/refresh?token={refreshToken}" ] [ str "Refresh Token" ]
    ]
