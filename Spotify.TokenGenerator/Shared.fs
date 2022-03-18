module Shared

open FSharp.Json

type TokenResponse = {
    [<JsonField("token_type")>]
    TokenType: string;
    [<JsonField("access_token")>]
    AccessToken: string;
    [<JsonField("refresh_token")>]
    RefreshToken: string;
    [<JsonField("scope")>]
    Scope: string;
    [<JsonField("expires_in")>]
    ExpiresIn: int;
}

type RefreshTokenResponse = {
    [<JsonField("token_type")>]
    TokenType: string;
    [<JsonField("access_token")>]
    AccessToken: string;
    [<JsonField("scope")>]
    Scope: string;
    [<JsonField("expires_in")>]
    ExpiresIn: int;
}