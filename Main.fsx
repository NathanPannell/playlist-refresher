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


let spotifyBaseURI = "https://api.spotify.com/v1"
let playlistId = Environment.GetEnvironmentVariable "target_playlist_id"
let daysSinceStart = 
    Environment.GetEnvironmentVariable "start_date"
    |> DateTime.Parse
    |> fun startDate -> (DateTime.Now - startDate).TotalDays
    |> Convert.ToInt32
type Album = JsonProvider<"sample_json/get_album.json">
type NewToken = JsonProvider<"sample_json/new_token.json">


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


// Fetches album object. Used for generating a list of tracks and album metadata
let getAlbum (accessToken: string) (albumId: string) = async {

    let! response = 
        http {
            GET (spotifyBaseURI + "/albums/" + albumId)
            AuthorizationBearer accessToken
        }
        |> Request.sendAsync

    return response
    |> Response.assert2xx
    |> Response.toText
    |> Album.Parse
}


let getNextAlbumId () : Async<string> = async {
    let albumIds = File.ReadAllLines "album_ids.txt"
    return albumIds[daysSinceStart % albumIds.Length] // Loop after all ids are exhausted
}


// Overwrites playlist items with provided track ids
let updatePlaylistTracks (accessToken: string) (trackIds: string) : Async<Response> = async {
    let! response =
        http {
            PUT (spotifyBaseURI + "/playlists/" + playlistId + "/tracks")
            AuthorizationBearer accessToken
            query [
                "uris", trackIds
            ]
        }
        |> Request.sendAsync

    return response
    |> Response.assert2xx
}


// Overwrites playlist name and description with those provided
let updatePlaylistDetails (accessToken: string) (name: string) (description: string) : Async<Response> = async {

    let! response =
        http {
            PUT (spotifyBaseURI + "/playlists/" + playlistId)
            AuthorizationBearer accessToken

            body
            jsonSerialize 
                {|
                    name = name
                    description = description
                |}
        }
        |> Request.sendAsync
    
    return response
    |> Response.assert2xx
}


let main _ =
    async {
        let! accessToken = getAccessToken ()
        let! nextAlbumId = getNextAlbumId ()
        let! album = getAlbum accessToken nextAlbumId

        let trackIds = 
            album.Tracks.Items 
            |> Array.map (fun item -> $"spotify:track:{item.Id}")
            |> String.concat ","
        do! updatePlaylistTracks accessToken trackIds |> Async.Ignore

        let name = $"Day {daysSinceStart}: {album.Name}"
        let description = $"This {album.AlbumType} was released by {album.Artists[0].Name} on {album.ReleaseDate.ToLongDateString()}"
        do! updatePlaylistDetails accessToken name description |> Async.Ignore
    }
    |> Async.RunSynchronously
    0
main ()