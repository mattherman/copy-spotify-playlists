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

    let map (mapper: 'T -> 'U) (asyncResult: Async<Result<'T, 'TError>>): Async<Result<'U, 'TError>> = async {
        let! result = asyncResult
        match result with
        | Ok value -> return Ok (mapper value)
        | Error e -> return Error e
    }

    let mapError (mapper: 'TError -> 'UError) (asyncResult: Async<Result<'T, 'TError>>): Async<Result<'T, 'UError>> = async {
        let! result = asyncResult
        match result with
        | Ok value -> return Ok value
        | Error e -> return Error (mapper e)
    }

module Models =
    type Profile = {
        Id: string;
        Email: string;
        DisplayName: string;
    }

    type User = {
        Id: string;
        DisplayName: string;
        Type: string;
    }

    type PlaylistTracks = {
        Href: string;
        Total: int;
    }

    type Playlist = {
        Id: string;
        Name: string;
        Description: string option;
        Public: bool option;
        Owner: User;
        Tracks: PlaylistTracks;
    }

    type Track = {
        Uri: string
        Name: string;
    }

    type TrackDetails = {
        Track: Track;
    }

    type PagedResponse<'T> = {
        Limit: int;
        Offset: int;
        Total: int;
        Items: list<'T>;
        Next: string option;
    }

    type CreatePlaylistRequest = {
        Name: string;
        Description: string option;
        Public: bool option;
    }

    type AddPlaylistItemsRequest = {
        Uris: string list;
    }

    type AddPlaylistItemsResponse = {
        SnapshotId: string;
    }

    type ErrorDetail = {
        Error: {| Status: int; Message: string |}
    }

    type Error =
    | GetUserError of ErrorDetail
    | GetPlaylistsError of ErrorDetail
    | GetTracksError of ErrorDetail
    | CreatePlaylistError of ErrorDetail
    | AddPlaylistItemsError of ErrorDetail

module Api =
    open System.Text
    open System.Net.Mime

    let config = JsonConfig.create(jsonFieldNaming = Json.snakeCase)

    let client = new HttpClient()

    client.BaseAddress <- new Uri("https://api.spotify.com/")

    let createRequestMessage (method: HttpMethod) (path: string) =
        let request = new HttpRequestMessage(method, path)
        request

    let withJsonBody body (request: HttpRequestMessage) =
        request.Content <- new StringContent(
            Json.serializeEx config body,
            Encoding.UTF8,
            MediaTypeNames.Application.Json
        )
        request

    let withAuthorization token (request: HttpRequestMessage) =
        request.Headers.Authorization <- new AuthenticationHeaderValue("Bearer", token)
        request

    let send<'Response, 'ErrorResponse> request (expectedStatusCodes: HttpStatusCode list)= async {
        let! response = Async.AwaitTask <| client.SendAsync(request)
        let! responseContent = Async.AwaitTask <| response.Content.ReadAsStringAsync()
        if expectedStatusCodes |> List.contains response.StatusCode then
            return Ok (Json.deserializeEx<'Response> config responseContent)
        else
            return Error (Json.deserializeEx<'ErrorResponse> config responseContent)
    }

    let get<'Response, 'ErrorResponse> token path = async {
        let request =
            createRequestMessage HttpMethod.Get path
            |> withAuthorization token
        return! send<'Response, 'ErrorResponse> request [ HttpStatusCode.OK ]
    }

    let post<'Request, 'Response, 'ErrorResponse> token (body: 'Request) path = async {
        let request =
            createRequestMessage HttpMethod.Post path
            |> withJsonBody body
            |> withAuthorization token
        return! send<'Response, 'ErrorResponse> request [ HttpStatusCode.Created ]
    }

    let rec getAllPages<'Response, 'ErrorResponse> token pageUri : Async<Result<list<'Response>, 'ErrorResponse>> = async {
        let pageResult = get<Models.PagedResponse<'Response>, 'ErrorResponse> token pageUri
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
        return! get<Models.Profile, Models.ErrorDetail> token "v1/me"
        |> AsyncResult.mapError Models.GetUserError
    }

    let getAllCurrentUserPlaylists token = async {
        return! getAllPages<Models.Playlist, Models.ErrorDetail> token "v1/me/playlists"
        |> AsyncResult.mapError Models.GetPlaylistsError
    }

    let getAllTracksForPlaylist token (playlist: Models.Playlist) = async {
        return! getAllPages<Models.TrackDetails, Models.ErrorDetail> token playlist.Tracks.Href
        |> AsyncResult.mapError Models.GetTracksError
    }

    let createPlaylist token userId (createPlaylistRequest: Models.CreatePlaylistRequest) = async {
        return! post<Models.CreatePlaylistRequest, Models.Playlist, Models.ErrorDetail> token createPlaylistRequest $"v1/users/{userId}/playlists"
        |> AsyncResult.mapError Models.CreatePlaylistError
    }

    let addItemsToPlaylist token playlistId (addPlaylistItemsRequest: Models.AddPlaylistItemsRequest) = async {
        return! post<Models.AddPlaylistItemsRequest, Models.AddPlaylistItemsResponse, Models.ErrorDetail> token addPlaylistItemsRequest $"v1/playlists/{playlistId}/tracks"
        |> AsyncResult.mapError Models.AddPlaylistItemsError
    }

let createPlaylistWithTracks token (createPlaylistRequest: Models.CreatePlaylistRequest) (tracks: Models.TrackDetails list) = async {
    let trackUris =
        tracks
        |> List.map (fun track -> track.Track.Uri)
        //|> List.truncate 100 // TODO: Limit of 100 per request, need to update to make multiple requests

    return! Api.getCurrentUser token
    |> AsyncResult.bind (fun userProfile -> Api.createPlaylist token userProfile.Id createPlaylistRequest)
    |> AsyncResult.bind (fun newPlaylist -> Api.addItemsToPlaylist token newPlaylist.Id { Uris = trackUris })
}

let copyPlaylist srcToken destToken (playlist: Models.Playlist) = async {
    let playlistRequest: Models.CreatePlaylistRequest = {
        Name = $"Test: {playlist.Name}";
        Description = playlist.Description;
        Public = playlist.Public
    }

    return! Api.getAllTracksForPlaylist srcToken playlist
    |> AsyncResult.bind (createPlaylistWithTracks destToken playlistRequest)
}

let copyPlaylists srcToken destToken =
    let playlists = 
        Api.getAllCurrentUserPlaylists srcToken
        |> AsyncResult.map (List.skip 3 >> List.take 1)
        |> Async.RunSynchronously

    match playlists with
    | Ok playlists ->
        printfn $"Copying {playlists.Length} playlist(s)"
        for playlist in playlists do
            printfn $"Copying playlist: {playlist.Name}"
            let copyResult = copyPlaylist srcToken destToken playlist |> Async.RunSynchronously
            match copyResult with
            | Ok _ -> printfn "=> Success"
            | Error errorDetail -> printfn $"=> Failed: {errorDetail}"
    | Error errorDetail ->
        printfn $"Failed: {errorDetail}"