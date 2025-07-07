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



let sourcePlaylistId = "4KQDdglJ7HGcqNozbJTlM3" // 1000 Recordings to Hear Before You Die (Tom Moon): Album Index
let spotifyBaseUri = "https://api.spotify.com/v1"
type NewToken = JsonProvider<"sample_json/new_token.json">
type PlaylistItems = JsonProvider<"sample_json/playlist_items.json">

let getAccessToken () : Async<string> = async {

    let refreshToken = requireEnv "refresh_token"
    let clientId = requireEnv "client_id"
    let clientSecret = requireEnv "client_secret"

    printfn "Retrieving a new access token..."
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
    |> fun newToken ->
        printfn "Successfully found access token: %s" newToken.AccessToken
        newToken.AccessToken
}

let rec getPlaylistContents (accessToken: string) (offset: int) : Async<list<string>> = async {
    
    printfn "Retrieving items %d-%d from playlist..." offset (offset + 49)
    let! response = 
        http {
            GET $"{spotifyBaseUri}/playlists/{sourcePlaylistId}/tracks"
            AuthorizationBearer accessToken
            query [
                "limit", "50"
                "offset", offset.ToString()
            ]
        }
        |> Request.sendAsync

    let playlistItems = 
        response
        |> Response.assert2xx
        |> Response.toText
        |> PlaylistItems.Parse

    let albumIds = 
        playlistItems.Items
        |> Array.map _.Track.Album.Id
        |> Array.toList

    if playlistItems.Total > offset + 50 then
        let! rest = getPlaylistContents accessToken (offset+50)
        return albumIds @ rest
    else
        return albumIds
}

let main _ =
    async {
        let! accessToken = getAccessToken ()
        let! albumIds = getPlaylistContents accessToken 0
        File.WriteAllLines("album_ids.txt", albumIds)
    }
    |> Async.RunSynchronously
    0
main ()