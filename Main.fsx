#r "nuget: dotenv.net"
#r "nuget: FSharp.Data"
#r "nuget: FsHttp"

open System
open System.IO
open dotenv.net
open FSharp.Data
open FsHttp

let getEnv name =
    match Environment.GetEnvironmentVariable name with
    | null | "" -> None
    | value -> Some value

// Load .env if environment variables are not otherwise present.
if getEnv "client_id" |> Option.isNone then
    DotEnv.Load()

let requireEnv name =
    getEnv name |> Option.defaultWith (fun () -> failwithf "Missing environment variable: %s" name)


let SpotifyBaseURI = "https://api.spotify.com/v1"
let PlaylistId = Environment.GetEnvironmentVariable "target_playlist_id"
type NewToken = JsonProvider<"sample_json/new_token.json">
type Album = JsonProvider<"sample_json/get_album.json">
let DaysSinceStart = 
    Environment.GetEnvironmentVariable "start_date"
    |> DateTime.Parse
    |> fun startDate -> (DateTime.Now - startDate).TotalDays
    |> Convert.ToInt32

// Use refresh token to get a new access token for this session
// https://developer.spotify.com/documentation/web-api/tutorials/refreshing-tokens
let getAccessToken () : Async<string> = async {

    let refreshToken = requireEnv "refresh_token"
    let clientId = requireEnv "client_id"
    let clientSecret = requireEnv "client_secret"

    let! response =
        http {
            POST "https://accounts.spotify.com/api/token"
            AuthorizationUserPw clientId clientSecret
            body
            formUrlEncoded [
                "grant_type", "refresh_token"
                "refresh_token", refreshToken
            ]
        }
        |> Request.sendAsync

    return response
    |> Response.assert2xx
    |> Response.toText
    |> NewToken.Parse
    |> fun newToken -> newToken.AccessToken
}

let GetAlbum accessToken albumId = task {
    printfn "Retrieving album with id: %s..." albumId

    let album = 
        http {
            GET (SpotifyBaseURI + "/albums/" + albumId)
            AuthorizationBearer accessToken
        }
        |> Request.send
        |> Response.assert2xx
        |> Response.toText
        |> Album.Parse

    printfn "Successfully found album id=%s with %d/%d tracks" album.Name album.Tracks.Items.Length album.TotalTracks

    return album
}

let GetNextAlbumId () = task {
    let albumIds = File.ReadAllLines "album_ids.txt"
    return albumIds[DaysSinceStart % albumIds.Length] // Loop after all ids are exhausted
}

let UpdatePlaylistTracks accessToken tracks = task {
    let formattedTracks = 
        tracks
        |> Array.map (fun trackId -> "spotify:track:" + trackId)
        |> String.concat ","

    printfn "Updating playlist with %d tracks..." tracks.Length
    let response =
        http {
            PUT (SpotifyBaseURI + "/playlists/" + PlaylistId + "/tracks")
            AuthorizationBearer accessToken
            query [
                "uris", formattedTracks
            ]
        }
        |> Request.send
        |> Response.assert2xx
    printfn "Successfully updated playlist tracks"

    return ()
}

let UpdatePlaylistDetails accessToken name description = task {
    printfn "Updating playlist details\nname=%s\ndesc=%s..." name description
    let response =
        http {
            PUT (SpotifyBaseURI + "/playlists/" + PlaylistId)
            AuthorizationBearer accessToken

            body
            jsonSerialize 
                {|
                    name = name
                    description = description
                |}
        }
        |> Request.send
        |> Response.assert2xx
    printfn "Successfully updated playlist details"

    return ()
}

// TODO: UpdatePlaylistImage




// Actual execution

let accessToken = GetAccessToken().Result.AccessToken
let nextAlbumId = GetNextAlbumId().Result
let album = GetAlbum accessToken nextAlbumId |> fun task -> task.Result

let tracks = album.Tracks.Items |> Array.map (fun item -> item.Id)
UpdatePlaylistTracks accessToken tracks 

let name = $"Day {DaysSinceStart}: {album.Name}"
let description = $"This {album.AlbumType} was released by {album.Artists[0].Name} on {album.ReleaseDate.ToLongDateString()}"
UpdatePlaylistDetails accessToken name description