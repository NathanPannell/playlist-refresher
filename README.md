# Record Of The Day

**Pin the playlist below and wake up to a new essential album each day!**  
It updates automatically at **3:14 AM PST** with a new pick from [Tom Moon’s _1,000 Recordings to Hear Before You Die_](https://en.wikipedia.org/wiki/1,000_Recordings_to_Hear_Before_You_Die).

[Follow the Playlist on Spotify](https://open.spotify.com/playlist/42nArFgYJDbc0f6VzHfCdZ)

## Inspiration

This project was created out of a desire to explore great music without friction.

Tom Moon's book curates 1,000 essential albums, and this tool brings them right to you every single day.

---

## What It Does

Every day, the script:

1. Picks the next album from a pre-generated list of Spotify album IDs [(album index)](https://open.spotify.com/playlist/4KQDdglJ7HGcqNozbJTlM3)
2. Updates the target Spotify playlist with that album's tracks
3. Renames the playlist with the album title and release info

Everything is handled via the Spotify Web API using a scheduled GitHub Action!

## [![Daily Playlist Refresh](https://github.com/nathanpannell/record-of-the-day/actions/workflows/main.yml/badge.svg)](https://github.com/nathanpannell/record-of-the-day/actions/workflows/main.yml)

---

## Repository Structure

### `Main.fsx`

The core script that:

- Picks the day's album based on the number of days since the project start date
- Replaces the playlist’s tracks and metadata

### `GetAlbumIds.fsx`

A helper script to generate `album_ids.txt` by:

- Scanning a source Spotify playlist (e.g. a list matching Tom Moon’s 1,000 albums)
- Extracting and de-duplicating album IDs

### `album_ids.txt`

A list of album IDs used to rotate daily.

---

## Running Locally

This project uses environment variables to store your Spotify credentials:

```env
client_id=your_client_id
client_secret=your_client_secret
refresh_token=your_refresh_token
target_playlist_id=your_target_playlist_id
start_date=MM-DD-YYYY
```

You can create a `.env` file in the project root for local use.
Secrets are accessed via `dotenv.net` unless already set in the environment (e.g. GitHub Secrets).

To run the script:

```bash
dotnet fsi .\Main.fsx
```

To regenerate album IDs:

```bash
dotnet fsi .\GetAlbumIds.fsx
```

**NOTE:** update the `sourcePlaylistId` parameter in `GetAlbumIds.fsx` to reference albums from a different playlist.

---

## License

This project is licensed under the [MIT License](LICENSE).
