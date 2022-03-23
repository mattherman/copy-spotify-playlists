#r "nuget: FSharp.Json"

open System
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open FSharp.Json

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

    type Playlists = {
        [<JsonField("limit")>]
        Limit: int;
        [<JsonField("offset")>]
        Offset: int;
        [<JsonField("total")>]
        Total: int;
        [<JsonField("items")>]
        Items: list<Playlist>
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

    let sendRequest<'Response> request = async {
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

    let getCurrentUser token = async {
        let request =
            createRequestMessage HttpMethod.Get "https://api.spotify.com/v1/me"
            |> withAuthorization token
        return! sendRequest<Models.Profile> request
    }

    let getCurrentUserPlaylists token = async {
        let request =
            createRequestMessage HttpMethod.Get "https://api.spotify.com/v1/me/playlists"
            |> withAuthorization token
        return! sendRequest<Models.Playlists> request
    }