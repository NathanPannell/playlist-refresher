#r "nuget: FsHttp"
#r "nuget: dotenv.net"

open FsHttp
open dotenv.net

// http {
//     GET "https://www.google.com"
// }
// |> Request.send
// |> Response.saveFile "google.html"

DotEnv.Load()

printfn "foo: %A" (System.Environment.GetEnvironmentVariable("foo"))