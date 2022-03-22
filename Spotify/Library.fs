namespace Spotify

open System
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open FSharp.Json

module Api =

    module Models =
        type Profile = {
            Id: int;
            Email: string;
            DisplayName: string;
        }

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

    let getCurrentUser token = task {
        let request =
            createRequestMessage HttpMethod.Get "/me"
            |> withAuthorization token
        let! response = client.SendAsync(request)
        let! responseContent = response.Content.ReadAsStringAsync()

        let result = 
            match response.StatusCode with
            | HttpStatusCode.OK ->
                Ok (Json.deserialize<Models.Profile> responseContent)
            | _ ->
                Error responseContent
        return result
    }