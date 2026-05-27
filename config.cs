using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DesktopWallpaper
{
    public static class Config
    {
        public static bool comets = true;
        public static string channels = "";
        public static string longi = ""; //longi as in longitude but without the tude
        public static string lat = "";
        public static string privateUsername = "";
        public static string discordPath = "";
        public static string termiusPath = "";

        public static string SettingsFilePath => Path.Combine(FileSystem.AppDataDirectory, "settings.txt");

        public static void Load()
        {
            if (!File.Exists(SettingsFilePath))
            {
                Save();
                return;
            }

            foreach (var line in File.ReadAllLines(SettingsFilePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex < 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..];

                switch (key)
                {
                    case nameof(comets):
                        if (bool.TryParse(value, out var parsedComets))
                        {
                            comets = parsedComets;
                        }
                        break;
                    case nameof(channels):
                        channels = value;
                        break;
                    case nameof(longi):
                        longi = value;
                        break;
                    case nameof(lat):
                        lat = value;
                        break;
                    case nameof(privateUsername):
                        privateUsername = value;
                        break;
                    case nameof(discordPath):
                        discordPath = value;
                        break;
                    case nameof(termiusPath):
                        termiusPath = value;
                        break;
                }
            }
        }

        public static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);

            var lines = new[]
            {
                $"{nameof(comets)}={comets}",
                $"{nameof(channels)}={channels}",
                $"{nameof(longi)}={longi}",
                $"{nameof(lat)}={lat}",
                $"{nameof(privateUsername)}={privateUsername}",
                $"{nameof(discordPath)}={discordPath}",
                $"{nameof(termiusPath)}={termiusPath}"
            };

            File.WriteAllLines(SettingsFilePath, lines);
        }
    }
}
