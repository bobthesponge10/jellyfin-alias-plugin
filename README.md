# Jellyfin Music Alias Plugin

A Plugin that changes the name of albums, tracks, and artists to be more readable in english.

## Install
To install just add this URL to your plugin repos:
  - [https://raw.githubusercontent.com/bobthesponge10/jellyfin-alias-plugin/refs/heads/master/manifest.json](https://raw.githubusercontent.com/bobthesponge10/jellyfin-alias-plugin/refs/heads/master/manifest.json)


## Workflow
Uses musicbrainz album, track, and artist ids to look for english aliases for albums, tracks, and artists.
  - Only attempts to find a better option if ratio of english to non english characters is below set threshold and musicbrainz states script and language is not english

If no aliases exist attempt to romanize japanese.
  - Only attempts i current name is japanese according to metadata.

## Config
The plugins ability to update albums, tracks, and artists can all be set seperately in the config.
The english character threshold can also be configured.

The ability for the plugin to run on automated scans can also be enabled or disabled. 
  - **If disabled and an automated scan happens, it is possible that previous changes made by this plugin get reset**
  - To fix this just re-run a metadata scan on the affected item. If the item was updated by this plugin before it stored the alias it found and can quickly restore it without any web requests.

## Potentially planned features
  - Ability to change desired language(s)
  - Better checks for valid/invalid names
    - Musicbrainz language/script can be missing/wrong
  - Rescan for Medium/Bad quality aliases
    - Either on a schedule or manually
  - Invalidate button for entire library
