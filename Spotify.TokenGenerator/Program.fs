open Saturn
open Giraffe
open System.Net.Http
open System
open System.Text
open System.Net.Http.Headers
open System.Net
open FSharp.Json
open Shared

let base64Encode (value: string) =
    Convert.ToBase64String(Encoding.UTF8.GetBytes(value))

let envVar key = Environment.GetEnvironmentVariable(key)

let clientId = envVar "SPOTIFY_CLIENT_ID"
let clientSecret = envVar "SPOTIFY_CLIENT_SECRET"

let browser = pipeline {
    plug acceptHtml
    plug putSecureBrowserHeaders
}

let spotifyBaseUrl = "https://accounts.spotify.com"

let spotifyClient =
    let client = new HttpClient()
    client.BaseAddress <- new Uri(spotifyBaseUrl)
    client

let initiateLogin () =
    let redirectUri = "http://localhost:5000/token"
    let scope = "user-read-private user-read-email"
    let queryString = $"response_type=code&client_id={clientId}&scope={scope}&redirect_uri={redirectUri}"
    redirectTo false $"{spotifyBaseUrl}/authorize?{queryString}"

type TokenGrantType =
    | AuthorizationCode of string
    | RefreshToken of string

let getToken<'T> grantType redirectUri = task {
    let formData =
        match grantType with
        | AuthorizationCode code ->
            Map [
                ("grant_type", "authorization_code")
                ("code", code)
                ("redirect_uri", redirectUri)
            ]
        | RefreshToken token ->
            Map [
                ("grant_type", "refresh_token")
                ("refresh_token", token)
                ("redirect_uri", redirectUri)
            ]

    let request = new HttpRequestMessage()
    request.RequestUri <- new Uri($"{spotifyBaseUrl}/api/token")
    request.Method <- HttpMethod.Post

    let content = new FormUrlEncodedContent(formData)
    request.Content <- content

    let authorization = base64Encode $"{clientId}:{clientSecret}"
    request.Headers.Authorization <- new AuthenticationHeaderValue("Basic", authorization)

    let! response = spotifyClient.SendAsync(request)
    let! responseContent = response.Content.ReadAsStringAsync()

    let result =
        match response.StatusCode with
        | HttpStatusCode.OK ->
            Ok (Json.deserialize<'T> responseContent)
        | _ ->
            Error responseContent
    return result
}

let retrieveToken () : HttpHandler =
    fun next ctx -> task {
        let code =
            match ctx.TryGetQueryStringValue "code" with
            | Some code -> code
            | None -> ""

        let! result = getToken<TokenResponse> (AuthorizationCode code) "http://localhost:5000/token"

        let view =
            match result with
            | Ok tokenResult ->
                Token.view tokenResult.AccessToken tokenResult.RefreshToken
            | Error msg ->
                Error.view msg
        return! (htmlView view) next ctx
    }

let refreshToken () : HttpHandler =
    fun next ctx -> task {
        let refreshToken =
            match ctx.TryGetQueryStringValue "token" with
            | Some token -> token
            | None -> ""

        let! result = getToken<RefreshTokenResponse> (RefreshToken refreshToken) "http://localhost:5000/token"

        let view =
            match result with
            | Ok tokenResult ->
                Token.view tokenResult.AccessToken refreshToken
            | Error msg ->
                Error.view msg
        return! (htmlView view) next ctx
    }

let appRouter'' = router {
    pipe_through browser

    get "/" (htmlView Index.view)
    get "/index.html" (redirectTo false "/")
    get "/default.html" (redirectTo false "/")

    get "/login" (initiateLogin ())
    get "/token" (retrieveToken ())
    get "/refresh" (refreshToken ())
}

let app = application {
    use_router appRouter''
}

run app
