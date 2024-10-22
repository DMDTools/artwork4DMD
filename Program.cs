// Artwork4DMD: create artwork for DMD by fetching artwork from Launchbox Game Database
//              [Launchbox Game Database](https://gamesdb.launchbox-app.com/)
//
//                                            ##          ##
//                                              ##      ##         )  )
//                                            ##############
//                                          ####  ######  ####
//                                        ######################
//                                        ##  ##############  ##     )   )
//                                        ##  ##          ##  ##
//                                              ####  ####
//
//                                     Copyright (C) 2024 Olivier JACQUES
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along
// with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using ImageMagick;
using System.Net.Http;
using System.Xml;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Ini;
using System.Linq;
using System.Configuration;
using System.Net;
using System.Threading;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

class Program
{
    // Structure to store game information
    public struct GameInfo
    {
        public string DatabaseID;
        public string Name;
        public string Platform;
        public string LogoFileName;
        // Constructor with default values
        public GameInfo(string databaseID = null, string name = null, string platform = null, string logoFileName = null)
        {
            DatabaseID = databaseID;
            Name = name;
            Platform = platform;
            LogoFileName = logoFileName;
        }
    }


    // List of Games
    public static List<GameInfo> Games = new List<GameInfo>();
    public sealed class Settings
    {
        public required bool Overwrite { get; set; }
        public required string Platforms { get; set; }
        public required string OutputFolder { get; set; }
        public required string OutputSizes { get; set; }
        public string LocalGifFolder { get; set; } = string.Empty;
        public bool ConvertLocalGif { get; set; } = false;
        public bool ConvertOnlineLaunchboxGamesDB { get; set; } = true;
        public uint GifColors { get; set; } = 32;
        public string GifScaleMode { get; set; } = "fill";
        public string BackgroundColor { get; set; } = "#000000";
    }

    public static Settings gSettings;
    static async Task Main(string[] args)
    {

        if (args.Length > 0 && args[0] == "--list-platforms")
        {
            await ListPlatforms();
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Set the MAGICK_HOME environment variable to the directory containing ImageMagick libraries
            Environment.SetEnvironmentVariable("MAGICK_HOME", AppDomain.CurrentDomain.BaseDirectory);
        }

        string iniPath = Path.Combine(Environment.CurrentDirectory, "settings.ini");
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddIniFile(iniPath)
            .Build();
        gSettings = config.GetRequiredSection("Settings").Get<Settings>();
        // Set up logging to a file
        Trace.Listeners.Add(new TextWriterTraceListener("debug.log") { TraceOutputOptions = TraceOptions.Timestamp });
        Trace.AutoFlush = true;
        if (gSettings.ConvertOnlineLaunchboxGamesDB) {
            await DownloadMetadataAndExtract();
            LoadAndParse();
            await DownloadPictures();
        }
        ConvertPictures();
    }

    static async Task ListPlatforms()
    {
        string platformsFile = "Platforms.xml";

        if (!File.Exists(platformsFile))
        {
            Console.WriteLine("Platforms.xml not found. Downloading metadata...");
            await DownloadMetadataAndExtract();
            
            if (!File.Exists(platformsFile))
            {
                Console.WriteLine("Error: Failed to download or extract Platforms.xml.");
                return;
            }
        }

        HashSet<string> platforms = new HashSet<string>();

        using (XmlReader reader = XmlReader.Create("Platforms.xml"))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "Name":
                            string platform = reader.ReadElementContentAsString();
                            platforms.Add(platform);
                            break;
                    }
                }
            }
        }

        Console.WriteLine("Available platforms:");
        foreach (string platform in platforms.OrderBy(p => p))
        {
            Console.WriteLine($"- {platform}");
        }
    }

    /// Download http://gamesdb.launchbox-app.com/Metadata.zip and extract it
    static async Task DownloadMetadataAndExtract()
    {
        string metadataZipPath = "Metadata.zip";
        if (!File.Exists(metadataZipPath))
        {
            // Download the zip file
            Console.WriteLine("Downloading Metadata.zip...");
            var httpClient = new HttpClient();
            var httpResult = await httpClient.GetAsync("http://gamesdb.launchbox-app.com/Metadata.zip");
            using var resultStream = await httpResult.Content.ReadAsStreamAsync();
            using var fileStream = File.Create("Metadata.zip");
            resultStream.CopyTo(fileStream);
            fileStream.Close();
        }
        else
        {
            Console.WriteLine("Metadata.zip already exists. Skipping download.");
        }
        // Extract the zip file
        Console.WriteLine("Extracting Metadata.zip...");
        System.IO.Compression.ZipFile.ExtractToDirectory("Metadata.zip", ".", true);
    }

    /// Download picture for game and save it as <Platform>/<game name>.png
    static async Task DownloadPicture(GameInfo game)
    {
        Directory.CreateDirectory($"{gSettings.OutputFolder}/orig/{game.Platform}");
        // If overwrite config is false AND file already exist, then don't do anything
        string filePath = Path.Combine(gSettings.OutputFolder, "orig", game.Platform, $"{game.Name}.png");
        if ((gSettings.Overwrite == false) && File.Exists(filePath))
        {
            Console.WriteLine($"Skipping download of {filePath} because it already exists");
            return;
        }
        if (game.Name.Contains(":")) {
            Console.WriteLine($"Skipping download of {game.Platform}/{game.Name} because it contains a colon - invalid character for a filename");
            return;
        }
        Console.WriteLine($"Downloading picture for {game.Platform}/{game.Name}: https://images.launchbox-app.com/{game.LogoFileName}");
        var httpClient = new HttpClient();
        var httpResult = await httpClient.GetAsync($"https://images.launchbox-app.com/{game.LogoFileName}");
        using var resultStream = await httpResult.Content.ReadAsStreamAsync();
        // Create folder if it doesn't exist
        Directory.CreateDirectory($"{gSettings.OutputFolder}/orig/{game.Platform}");
        using var fileStream = File.Create($"{gSettings.OutputFolder}/orig/{game.Platform}/{game.Name}.png");
        resultStream.CopyTo(fileStream);
        fileStream.Close();
    }

    /// <summary>
    /// Download all pictures
    /// </summary>
    /// <returns></returns>
    static async Task DownloadPictures()
    {
        // Sort the Games list alphabetically by Name
        var sortedGames = Games.OrderBy(g => g.Name).ToList();
        
        foreach (var game in Games)
        {
            if (game.LogoFileName != null)
            {
                await DownloadPicture(game);
            }
        }
    }

    /// <summary>
    /// Go through all files in the output folder, and if the file name contains .orig.png, convert it to .png
    /// </summary>
    /// <param name="inputFile"></param>
    /// <param name="outputFile"></param>
    static void ConvertPictures()
    {
        // Second pass: Convert all downloaded .orig.png files

        if (gSettings.ConvertOnlineLaunchboxGamesDB)
        {
            string[] origPngFiles = Directory.GetFiles(gSettings.OutputFolder + "/orig", "*.*", SearchOption.AllDirectories)
                                             .Where(f => Path.GetFileName(f).ToLower().EndsWith(".png"))
                                             .ToArray();
            Console.WriteLine($"Converting {origPngFiles.Length} pictures...");

            foreach (string origPngFile in origPngFiles)
            {
                ConvertPicture(origPngFile, false);
            }
        }

        // If convertLocalGif is true, scan the GIFs from the folder provided
        if (gSettings.ConvertLocalGif)
        {
            string[] gifFiles = Directory.GetFiles(gSettings.LocalGifFolder, "*.*", SearchOption.AllDirectories)
                                         .Where(f => Path.GetFileName(f).ToLower().EndsWith(".gif"))
                                         .ToArray();
            Console.WriteLine($"Converting {gifFiles.Length} GIFs...");
            foreach (string gifFile in gifFiles)
            {
                ConvertPicture(gifFile, true);
            }
        }
    }

    /// <summary>
    /// Load Metadata.xml file, and for each game in <LaunchBox> root, create a structure with fields from <Game>: <Platform>, <DatabaseID>, <Name>/
    /// Then, from <LaunchBox><GameImage>, add in the <Game> structure the <FileName>, with the key <DatabaseID>
    /// </summary>
    static void LoadAndParse()
    {
        List<Task> downloadTasks = new List<Task>();
        List<string> platforms;
        // List of platforms
        if (!string.IsNullOrWhiteSpace(gSettings.Platforms))
        {
            platforms = new List<string>(gSettings.Platforms.Split(",").Select(x => x.ToLower().Trim()));
        }
        else
        {
            platforms = new List<string> { "Arcade" };
        }

        using (XmlReader reader = XmlReader.Create("Metadata.xml"))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "Game":
                            // Process Game
                            //   <Game>
                            //     < Name > Bodyconscious Digital Rave!Part 1: Shinjuku & amp; Takashi </ Name >
                            //     < ReleaseDate > 1994 - 12 - 23T00: 00:00 - 08:00 </ ReleaseDate >
                            //     < Overview > Another one of those Japanese-only 'games' that truly defined the 3DO as the ultimate multimedia masterpiece of the early 90s. (In other words, it's just a video formatted to only play on the 3DO system)  A oddity,  Body Conscious Part 1- Shinjuku &amp; Takashi Digital Rave ! simulates the nightlife of Japan by taking you to the club scene where you can engage in simulated partying.</Overview>
                            //     < MaxPlayers > 1 </ MaxPlayers >
                            //     < ReleaseType > Released </ ReleaseType >
                            //     < Cooperative > false </ Cooperative >
                            //     < VideoURL > https://www.youtube.com/watch?v=RLko537roak</VideoURL>
                            //     < DatabaseID > 109297 </ DatabaseID >
                            //     < CommunityRating > 3.3666666666666667 </ CommunityRating >
                            //     < Platform > 3DO Interactive Multiplayer </ Platform >
                            //     < ESRB > Not Rated </ ESRB >
                            //     < CommunityRatingCount > 15 </ CommunityRatingCount >
                            //     < Genres />
                            //     < Developer > Transpegasus </ Developer >
                            //     < Publisher > Transpegasus </ Publisher >
                            //   </ Game >
                            // Print on Console <Name> element
                            using (var innerReader = reader.ReadSubtree())
                            {
                                // Add game to list
                                GameInfo game = new GameInfo();
                                innerReader.ReadToFollowing("Name");
                                game.Name = innerReader.ReadElementContentAsString();
                                innerReader.ReadToFollowing("DatabaseID");
                                game.DatabaseID = innerReader.ReadElementContentAsString();
                                innerReader.ReadToFollowing("Platform");
                                game.Platform = innerReader.ReadElementContentAsString();
                                // Add the game if the platform is part of the list of platforms to scrap

                                // If platforms list matches game.Platform
                                if (platforms.Any(x => x.Contains(game.Platform.ToLower().Trim())))
                                {
                                    Games.Add(game);
                                    // Add game to list
                                    Console.WriteLine($"Added Game: {game.Platform}/{game.Name} (id {game.DatabaseID})");
                                }
                                break;
                            }
                        case "GameImage":
                            // Process GameImage
                            using (var innerReader = reader.ReadSubtree())
                            {
                                innerReader.ReadToFollowing("DatabaseID");
                                string databaseID = innerReader.ReadElementContentAsString();
                                // If databaseID > 100, break
                                // if (int.Parse(databaseID) > 10000)
                                // {
                                //     break;
                                // }
                                innerReader.ReadToFollowing("FileName");
                                string fileName = innerReader.ReadElementContentAsString();
                                innerReader.ReadToFollowing("Type");
                                string type = innerReader.ReadElementContentAsString();
                                if (type == "Clear Logo") {
                                    // Find game in list and add logo filename
                                    // try
                                    // {
                                    var game = Games.Find(x => x.DatabaseID == databaseID);
                                    if (game.DatabaseID != null) {
                                        if (platforms.Any(x => x.Contains(game.Platform.ToLower())))
                                        {
                                            game.LogoFileName = fileName;
                                            downloadTasks.Add(DownloadPicture(game));
                                            //Console.WriteLine($"GameImage for {game.Platform}/{game.Name} {game.DatabaseID}: {game.LogoFileName}");
                                        }

                                    }

                                    // }
                                    // catch (Exception e)
                                    // {
                                    //     Console.WriteLine($"Error: {e.Message}");
                                    // }
                                }
                            }
                            //Trace.WriteLine($"GameImage: {reader.GetAttribute("FileName")}, {reader.GetAttribute("DatabaseID")}");
                            //ConvertImage($"clear-logos/{reader.GetAttribute("FileName")}", $"output/{reader.GetAttribute("DatabaseID")}.png");
                            break;
                        // ... handle other elements as needed
                    }
                }
            }
        }
    }


    /// <summary>
    /// Convert an image to a DMD compatible format, size driven by settings, high contrast, black background, centered
    /// </summary>
    /// <param name="inputPath"></param>
    static void ConvertPicture(string inputPath, bool isGif)
    {
        var outputSizes = gSettings.OutputSizes.Split(',').Select(s => s.Trim()).ToList();

        foreach (var size in outputSizes)
        {
            var dimensions = size.Split('x');
            if (dimensions.Length != 2 || !uint.TryParse(dimensions[0], out uint width) || !uint.TryParse(dimensions[1], out uint height))
            {
                Console.WriteLine($"Invalid size format in settings.ini: {size}. Skipping.");
                continue;
            }

            // inputPath is in the form "{gSettings.OutputFolder}/orig/{platform}/{gameName}.png"
            // outputPath is in the form "{gSettings.OutputFolder}/{gSettings.OutputSize}/{platform}/{gameName}.png
            string inputExtension = Path.GetExtension(inputPath).ToLowerInvariant();
            string outputExtension = (inputExtension == ".gif" || inputExtension == ".png") ? inputExtension : ".png";

            string outputPath = Path.Combine(
                gSettings.OutputFolder,
                $"{width}x{height}",
                Path.GetFileName(Path.GetDirectoryName(inputPath)),
                $"{Path.GetFileNameWithoutExtension(inputPath)}{outputExtension}"
            );

            // Check if the output file exists and is newer than the input file
            if (File.Exists(outputPath))
            {
                DateTime inputFileDate = File.GetLastWriteTime(inputPath);
                DateTime outputFileDate = File.GetLastWriteTime(outputPath);
                if (outputFileDate >= inputFileDate)
                {
                    Console.WriteLine($"Skipping {outputPath} - it is newer than or same age as source file.");
                    continue;
                } 
            }
            
            //Console.WriteLine($"Converting {Path.GetFileName(inputPath)} to {Path.GetFileName(outputPath)}");
            Console.WriteLine($"Creating {outputPath}");

            try
            {
                // Original conversion parameters from imagemagick's convert command: "-modulate 100,150,100 -trim -sample 128x32 -extent 128x32 -background black -compose Over -gravity center"
                using (var image = new ImageMagick.MagickImage(inputPath))
                {
                    // Create output folder if it doesn't exist
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    image.Strip();  // Remove metadata
                    if (!isGif)
                    {
                        image.Format = MagickFormat.Png32;
                        image.Modulate(new Percentage(100), new Percentage(150), new Percentage(100)); // Adjust brightness, saturation, and hue
                                                                                                       //image.Trim(); // Trim the image to its bounding box
                        image.BackgroundColor = new MagickColor(gSettings.BackgroundColor); // Set the background color to what the user likes (default is black)
                                                                               //image.Alpha(AlphaOption.Opaque);
                        image.Compose = CompositeOperator.Over; // Over composite the image
                        image.Sample(width, height); // Resize the image to size provided by settings
                        image.Extent(width, height, Gravity.Center); // Ensure the image is centered and has the correct dimensions
                        image.Write(outputPath); // Save the image
                    }
                    else {
                        // Original ImageMagic command: "convert input.gif -coalesce -scale widthxheight -fuzz 2% +dither -remap ( input.gif[0] +dither -colors XX ) -layers Optimize output.gif"
                        using var gifFrames = new MagickImageCollection(inputPath);
                        gifFrames.Coalesce();
                        // Find average color to setup background, from first image
                        var colorImage = new ImageMagick.MagickImage(gifFrames.First());
                        colorImage.Resize(1, 1);
                        var averageColor = colorImage.GetPixels().First().ToColor();
                        foreach (var frame in gifFrames)
                        {
                            frame.BackgroundColor = new MagickColor(averageColor);
                            frame.Compose = CompositeOperator.Over;
                            if (gSettings.GifScaleMode.ToLower() == "fit")
                            {
                                // Fit mode: Resize to fit within the target dimensions while maintaining aspect ratio
                                var frameSize = new MagickGeometry($"{width}x{height}");
                                frameSize.IgnoreAspectRatio = false;
                                frame.Resize(frameSize);
                                frame.Extent(width, height, Gravity.Center);
                            }
                            else if (gSettings.GifScaleMode.ToLower() == "fill")
                            {
                                // Fill mode: Resize to fill the entire target dimensions and crop if necessary
                                var frameSize = new MagickGeometry($"{width}x{height}^");
                                frameSize.IgnoreAspectRatio = false;
                                frame.Resize(frameSize);
                                frame.Extent(width, height, Gravity.Center);
                            }
                            frame.ColorFuzz = new Percentage(2);
                            frame.Modulate(new Percentage(100), new Percentage(150), new Percentage(100));
                        }
                        // Optimize
                        var settings = new QuantizeSettings
                        {
                            Colors = gSettings.GifColors,
                            DitherMethod = DitherMethod.FloydSteinberg
                        };
                        gifFrames.Quantize(settings);
                        gifFrames.Optimize();
                        gifFrames.Write(outputPath);    // Save the GIF
                    }
                }
            }
            catch (MagickException ex)
            {
                Console.WriteLine($"Error converting {Path.GetFileName(inputPath)}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error converting {Path.GetFileName(inputPath)}: {ex.Message}");
            }
        }
    }
}
