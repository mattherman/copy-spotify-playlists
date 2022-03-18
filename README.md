# copy-spotify-playlists

Scripts for copying playlists between accounts using the Spotify API.

To run the scripts you will need an API token. The Spotify.TokenGenerator project is a web app that will allow you to authenticate with Spotify via OAuth and generate a token.

Before running the application you will need to create an app in your Spotfify Developer console and add a "Redirect URI" of http://localhost:5000/tokenin the settings. Then you will need to set the `SPOTIFY_CLIENT_ID` and `SPOTIFY_CLIENT_SECRET` environment variables to the values provided to you in the console. Finally, run the application with `dotnet run` and navigate to http://localhost:5000.
