#r "nuget: dotenv.net"
#r "nuget: FSharp.Data"
#r "nuget: FsHttp"

open System
open System.IO
open dotenv.net
open FSharp.Data
open FsHttp

DotEnv.Load()

let SpotifyBaseURI = "https://api.spotify.com/v1"
let SourcePlaylistId = "4KQDdglJ7HGcqNozbJTlM3"
type NewToken = JsonProvider<"sample_json/new_token.json">
type PlaylistItems = JsonProvider<"sample_json/playlist_items.json">

let GetAccessToken () = task {

    let refreshToken = Environment.GetEnvironmentVariable "refresh_token"
    let clientId = Environment.GetEnvironmentVariable "client_id"
    let clientSecret = Environment.GetEnvironmentVariable "client_secret"

    printfn "Retrieving a new access token..."
    let newToken = 
        http {
            POST "https://accounts.spotify.com/api/token"
            AuthorizationUserPw clientId clientSecret

            body

            formUrlEncoded [
                "grant_type", "refresh_token"
                "refresh_token", refreshToken
            ]
        } 
        |> Request.send 
        |> Response.assert2xx
        |> Response.toText
        |> NewToken.Parse
    printfn "Successfully found access token: \"%s\"" newToken.AccessToken

    return newToken
}

let rec GetPlaylistContents (accessToken: string) (offset: int) = task {
    let playlistItems = 
        http {
            GET $"{SpotifyBaseURI}/playlists/{SourcePlaylistId}/tracks"
            AuthorizationBearer accessToken
            query [
                "limit", "50"
                "offset", offset.ToString()
            ]
        }
        |> Request.send
        |> Response.assert2xx
        |> Response.toText
        |> PlaylistItems.Parse

    let albumIds = 
        playlistItems.Items
        |> Array.map   _.Track.Album.Id
        |> Array.toList

    if playlistItems.Total > offset + 50 then
        let! rest = GetPlaylistContents accessToken (offset+50)
        return albumIds @ rest
    else
        return albumIds
}

// Actual execution
let accessToken = GetAccessToken().Result.AccessToken
let playlistItems = GetPlaylistContents accessToken 0 |> fun task -> task.Result
File.WriteAllLines ("album_ids.txt", playlistItems)