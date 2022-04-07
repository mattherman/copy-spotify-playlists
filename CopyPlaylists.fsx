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

    let send<'Response> request (expectedStatusCodes: HttpStatusCode list)= async {
        let! response = Async.AwaitTask <| client.SendAsync(request)
        let! responseContent = Async.AwaitTask <| response.Content.ReadAsStringAsync()
        if expectedStatusCodes |> List.contains response.StatusCode then
            return Ok (Json.deserializeEx<'Response> config responseContent)
        else
            return Error responseContent
    }

    let get<'Response> token path = async {
        let request =
            createRequestMessage HttpMethod.Get path
            |> withAuthorization token
        return! send<'Response> request [ HttpStatusCode.OK ]
    }

    let post<'Request, 'Response> token (body: 'Request) path = async {
        let request =
            createRequestMessage HttpMethod.Post path
            |> withJsonBody body
            |> withAuthorization token
        return! send<'Response> request [ HttpStatusCode.Created ]
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

    let getCurrentUser token : Async<Result<Models.Profile, string>> = async {
        return! get<Models.Profile> token "v1/me"
    }

    let getAllCurrentUserPlaylists token = async {
        return! getAllPages<Models.Playlist> token "v1/me/playlists"
    }

    let getAllTracksForPlaylist token (playlist: Models.Playlist) = async {
        return! getAllPages<Models.TrackDetails> token playlist.Tracks.Href
    }

    let createPlaylist token userId (createPlaylistRequest: Models.CreatePlaylistRequest) = async {
        return! post<Models.CreatePlaylistRequest, Models.Playlist> token createPlaylistRequest $"v1/users/{userId}/playlists"
    }

    let addItemsToPlaylist token playlistId (addPlaylistItemsRequest: Models.AddPlaylistItemsRequest) = async {
        return! post<Models.AddPlaylistItemsRequest, Models.AddPlaylistItemsResponse> token addPlaylistItemsRequest $"v1/playlists/{playlistId}/tracks"
    }

let createPlaylistWithTracks token (createPlaylistRequest: Models.CreatePlaylistRequest) (tracks: Models.TrackDetails list) = async {
    let trackUris =
        tracks
        |> List.map (fun track -> track.Track.Uri)

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

let copyPlaylistsTest token =
    Api.getAllCurrentUserPlaylists token
    |> AsyncResult.bind (fun playlists ->
        copyPlaylist token token playlists.[10])
    |> Async.RunSynchronously
