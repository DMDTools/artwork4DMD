# Artwork4DMD

Artwork4DMD is a C# application that creates artwork for DMD (Dot Matrix Display) by fetching artwork from the [Launchbox Game Database](https://www.launchbox-app.com/).

This artwork can be used for example by
[DOF2DMD](https://github.com/DMDTools/DOF2DMD) to display game marquees on a
DMD.

![Output](output.png)

## Description

This application processes game information from Launchbox's `Metadata.xml`
file, downloads game logos, and converts them into a format suitable for use
with DMD displays. It's particularly useful for arcade and retro gaming
enthusiasts who want to enhance their gaming setup with custom artwork.

## Features

- Parses Metadata.xml file from Launchbox Game Database
- Downloads game logos for specified platforms
- Converts images to a suitable format for DMD displays (128x32, high contrast, black background, centered)
- Supports all gaming platforms from Launchbox (configurable)

## Prerequisites

- .NET Core 3.1 or later
- ImageMagick (for image processing)

## Configuration

The application uses a `settings.ini` file for configuration. You can specify:

- Platforms to include
- Image processing parameters
- Output directories
- Output sizes (for 128x32 DMD and 256x64 DMD for example)

```ini
[Settings]
Platforms=Arcade
;Platforms=Arcade,Amstrad CPC,Commodore Amiga,Commodore 64,Atari ST
OutputFolder=.
Overwrite=false
OutputSizes=128x32,256x64
```

## Usage

1. Download the binary from the [Release section](https://github.com/DMDTools/Artwork4DMD/releases)
2. Configure your `settings.ini` file with desired parameters.
3. Run the application

## Building

To build the application as a single file:

```shell
dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

Replace `win-x64` with your target runtime identifier if different.

## License

This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation; either version 2 of the License, or (at your option) any later version.

## Acknowledgments

- [Launchbox Game Database](https://gamesdb.launchbox-app.com/) for providing the game metadata and artwork.
