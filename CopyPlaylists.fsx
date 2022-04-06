#r "nuget: FSharp.Json"

open System
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open FSharp.Json

module AsyncResult =
    let bind (binder: 'T -> Async<Result<'U, 'TError>>) (asyncResult: Async<Result<'T, 'TError>>): Async<Result<'U, 'TError>> = async {
        let! result = asyncResult
        match result with
        | Ok value -> return! binder value
        | Error e -> return Error e
    }

module Models =
    type Profile = {
        [<JsonField("id")>]
        Id: string;
        [<JsonField("email")>]
        Email: string;
        [<JsonField("display_name")>]
        DisplayName: string;
    }

    type Playlist = {
        [<JsonField("id")>]
        Id: string;
        [<JsonField("name")>]
        Name: string;
    }

    type PagedResponse<'T> = {
        [<JsonField("limit")>]
        Limit: int;
        [<JsonField("offset")>]
        Offset: int;
        [<JsonField("total")>]
        Total: int;
        [<JsonField("items")>]
        Items: list<'T>;
        [<JsonField("next")>]
        Next: string option;
    }

module Api =

    let client = new HttpClient()

    client.BaseAddress <- new Uri("https://api.spotify.com")

    let createRequestMessage method uri =
        let request = new HttpRequestMessage()
        request.RequestUri <- new Uri(uri)
        request.Method <- method
        request

    let withAuthorization token (request: HttpRequestMessage) =
        request.Headers.Authorization <- new AuthenticationHeaderValue("Bearer", token)
        request

    let send<'Response> request = async {
        let! response = Async.AwaitTask <| client.SendAsync(request)
        let! responseContent = Async.AwaitTask <| response.Content.ReadAsStringAsync()
        let result = 
            match response.StatusCode with
            | HttpStatusCode.OK ->
                Ok (Json.deserialize<'Response> responseContent)
            | _ ->
                Error responseContent
        return result
    }

    let get<'Response> token uri = async {
        let request =
            createRequestMessage HttpMethod.Get uri
            |> withAuthorization token
        return! send<'Response> request
    }

    let rec getAllPages<'Response> token pageUri : Async<Result<list<'Response>, string>> = async {
        let pageResult = get<Models.PagedResponse<'Response>> token pageUri
        return! pageResult
            |> AsyncResult.bind(fun page ->
                match page.Next with
                | Some nextPageUri ->
                    async {
                        let! remainingPages = getAllPages token nextPageUri
                        return remainingPages
                        |> Result.bind (fun remainingItems ->
                            Ok (page.Items @ remainingItems))
                    }
                | None ->
                    async {
                        return Ok page.Items
                    })
    }

    let getCurrentUser token = async {
        return! get<Models.Profile> token "https://api.spotify.com/v1/me"
    }

    let getCurrentUserPlaylists token = async {
        return! get<Models.PagedResponse<Models.Playlist>> token "https://api.spotify.com/v1/me/playlists"
    }

    let getAllCurrentUserPlaylists token = async {
        return! getAllPages<Models.Playlist> token "https://api.spotify.com/v1/me/playlists"
    }
