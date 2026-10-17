using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace SeaShanty
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class SeaShantyPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "seachanti.seashanty";
        public const string PluginName = "SeaShanty";
        public const string PluginVersion = "1.1.0";

        private const string CustomPrefix = "seashanty_";
        private static readonly string[] AudioExtensions = { ".ogg", ".wav", ".mp3" };

        private static SeaShantyPlugin _instance;

        private ConfigEntry<float> _volume;
        private ConfigEntry<bool> _shuffle;
        private ConfigEntry<bool> _oceanReplacesSailing;

        private string _musicDir;
        private int _pendingLoads;

        // Folder name (lower case) -> playlist
        private readonly Dictionary<string, Playlist> _playlists = new Dictionary<string, Playlist>();

        private MusicMan _appliedMusicMan;
        private EnvMan _appliedEnvMan;
        private AudioClip _lastClip;

        private class Playlist
        {
            public string Folder;
            public List<AudioClip> Clips = new List<AudioClip>();
            public MusicMan.NamedMusic Music;
            public int Index;
        }

        private void Awake()
        {
            _instance = this;

            _volume = Config.Bind("General", "Volume", 0.7f,
                new ConfigDescription("Volume of custom biome music (also scaled by the in-game music volume).",
                    new AcceptableValueRange<float>(0f, 1f)));
            _shuffle = Config.Bind("General", "Shuffle", false,
                "Shuffle playlists instead of playing them in alphabetical order.");
            _oceanReplacesSailing = Config.Bind("General", "OceanReplacesSailingMusic", true,
                "While on the Ocean biome, play the 'ocean' music instead of the vanilla sailing music.");

            _musicDir = Path.Combine(Paths.ConfigPath, "SeaShanty");
            Directory.CreateDirectory(Path.Combine(_musicDir, "ocean"));

            LoadAllMusic();
            new Harmony(PluginGuid).PatchAll(typeof(SeaShantyPlugin));
        }

        // Music folders: BepInEx/config/SeaShanty/<name>/ and <plugin dir>/music/<name>/
        private void LoadAllMusic()
        {
            string[] roots = { _musicDir, Path.Combine(Path.GetDirectoryName(Info.Location), "music") };
            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }
                foreach (string dir in Directory.GetDirectories(root))
                {
                    string[] files = Directory.GetFiles(dir)
                        .Where(f => AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    if (files.Length == 0)
                    {
                        continue;
                    }

                    string key = Path.GetFileName(dir).ToLowerInvariant();
                    Playlist playlist;
                    if (!_playlists.TryGetValue(key, out playlist))
                    {
                        playlist = new Playlist { Folder = key };
                        _playlists[key] = playlist;
                    }
                    foreach (string file in files)
                    {
                        StartCoroutine(LoadClip(file, playlist));
                    }
                }
            }

            if (_playlists.Count == 0)
            {
                Logger.LogWarning("No music found. Put .ogg/.wav/.mp3 files in " + Path.Combine(_musicDir, "ocean"));
            }
        }

        private IEnumerator LoadClip(string path, Playlist playlist)
        {
            _pendingLoads++;
            string url = "file:///" + path.Replace('\\', '/');
            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, GetAudioType(path)))
            {
                ((DownloadHandlerAudioClip)req.downloadHandler).compressed = true;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                    clip.name = Path.GetFileNameWithoutExtension(path);
                    playlist.Clips.Add(clip);
                    Logger.LogInfo("Loaded " + playlist.Folder + "/" + Path.GetFileName(path));
                }
                else
                {
                    Logger.LogError("Failed to load " + path + ": " + req.error);
                }
            }
            _pendingLoads--;

            if (_pendingLoads == 0)
            {
                foreach (Playlist p in _playlists.Values)
                {
                    p.Clips.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                    if (_shuffle.Value)
                    {
                        Shuffle(p.Clips);
                    }
                }
            }
        }

        private void Update()
        {
            if (_pendingLoads > 0 || _playlists.Count == 0)
            {
                return;
            }

            if (MusicMan.instance != null && MusicMan.instance != _appliedMusicMan)
            {
                _appliedMusicMan = MusicMan.instance;
                _appliedEnvMan = null;
                ApplyToMusicMan(_appliedMusicMan);
            }
            if (EnvMan.instance != null && EnvMan.instance != _appliedEnvMan && _appliedMusicMan != null)
            {
                _appliedEnvMan = EnvMan.instance;
                ApplyToEnvMan(_appliedEnvMan);
            }

            if (_appliedMusicMan != null)
            {
                AdvancePlaylists();
            }
        }

        private void ApplyToMusicMan(MusicMan musicMan)
        {
            WriteAvailableMusic(musicMan);
            var hashes = Traverse.Create(musicMan).Field("m_musicHashes").GetValue<Dictionary<int, MusicMan.NamedMusic>>();

            foreach (Playlist p in _playlists.Values)
            {
                if (p.Clips.Count == 0)
                {
                    continue;
                }

                MusicMan.NamedMusic music = musicMan.m_music.FirstOrDefault(
                    m => string.Equals(m.m_name, p.Folder, StringComparison.OrdinalIgnoreCase));
                if (music == null)
                {
                    if (ParseBiome(p.Folder) == Heightmap.Biome.None)
                    {
                        Logger.LogWarning("'" + p.Folder + "' is not a biome or music name. See available_music.txt");
                        continue;
                    }
                    // New looping track for a biome
                    music = new MusicMan.NamedMusic
                    {
                        m_name = CustomPrefix + p.Folder,
                        m_volume = _volume.Value,
                        m_fadeInTime = 3f,
                        m_alwaysFadeout = true,
                        m_loop = true,
                        m_ambientMusic = true,
                    };
                    musicMan.m_music.Add(music);
                    hashes[music.m_name.GetStableHashCode()] = music;
                }

                music.m_enabled = true;
                music.m_resume = p.Clips.Count == 1;
                music.m_clips = new[] { p.Clips[0] };
                p.Music = music;
                hashes[music.m_name.GetStableHashCode()] = music;
                Logger.LogInfo("Replaced music '" + music.m_name + "' with " + p.Clips.Count + " track(s)");
            }
        }

        private void ApplyToEnvMan(EnvMan envMan)
        {
            foreach (Playlist p in _playlists.Values)
            {
                Heightmap.Biome biome = ParseBiome(p.Folder);
                if (biome == Heightmap.Biome.None || p.Music == null || !p.Music.m_name.StartsWith(CustomPrefix))
                {
                    continue;
                }
                foreach (BiomeEnvSetup setup in envMan.m_biomes.Where(b => b.m_biome == biome))
                {
                    setup.m_musicMorning = p.Music.m_name;
                    setup.m_musicDay = p.Music.m_name;
                    setup.m_musicEvening = p.Music.m_name;
                    setup.m_musicNight = p.Music.m_name;
                }
            }
        }

        // Valheim picks a random clip from m_clips, so we keep one clip in there and step through the playlist.
        private void AdvancePlaylists()
        {
            AudioSource source = Traverse.Create(_appliedMusicMan).Field("m_musicSource").GetValue<AudioSource>();
            if (source == null || source.clip == _lastClip)
            {
                return;
            }
            _lastClip = source.clip;

            foreach (Playlist p in _playlists.Values)
            {
                if (p.Music == null || p.Clips.Count < 2 || !p.Clips.Contains(source.clip))
                {
                    continue;
                }
                source.loop = false;
                p.Index = (p.Clips.IndexOf(source.clip) + 1) % p.Clips.Count;
                if (p.Index == 0 && _shuffle.Value)
                {
                    Shuffle(p.Clips);
                }
                p.Music.m_clips = new[] { p.Clips[p.Index] };
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MusicMan), "GetSailingMusic")]
        private static void GetSailingMusicPostfix(ref string __result)
        {
            if (!_instance._oceanReplacesSailing.Value || Player.m_localPlayer == null
                || Player.m_localPlayer.GetCurrentBiome() != Heightmap.Biome.Ocean)
            {
                return;
            }
            Playlist ocean;
            if (_instance._playlists.TryGetValue("ocean", out ocean) && ocean.Music != null)
            {
                __result = ocean.Music.m_name;
            }
        }

        private void WriteAvailableMusic(MusicMan musicMan)
        {
            var lines = new List<string>
            {
                "Make a folder in BepInEx/config/SeaShanty/ named after a biome or a music name",
                "and put .ogg/.wav/.mp3 files in it. Several files = playlist.",
                "",
                "Biomes (custom music plays everywhere in that biome):",
            };
            lines.AddRange(Enum.GetNames(typeof(Heightmap.Biome))
                .Where(n => n != "None" && n != "All" && n != "Land")
                .Select(n => "  " + n.ToLowerInvariant()));
            lines.Add("");
            lines.Add("Valheim music names (replaces that music):");
            lines.AddRange(musicMan.m_music
                .Where(m => !m.m_name.StartsWith(CustomPrefix))
                .Select(m => "  " + m.m_name)
                .Distinct()
                .OrderBy(n => n));
            File.WriteAllLines(Path.Combine(_musicDir, "available_music.txt"), lines.ToArray());
        }

        private static Heightmap.Biome ParseBiome(string name)
        {
            foreach (Heightmap.Biome b in Enum.GetValues(typeof(Heightmap.Biome)))
            {
                if (b != Heightmap.Biome.All && string.Equals(b.ToString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    return b;
                }
            }
            return Heightmap.Biome.None;
        }

        private static void Shuffle(List<AudioClip> clips)
        {
            for (int i = clips.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                AudioClip tmp = clips[i];
                clips[i] = clips[j];
                clips[j] = tmp;
            }
        }

        private static AudioType GetAudioType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".wav": return AudioType.WAV;
                case ".mp3": return AudioType.MPEG;
                default: return AudioType.OGGVORBIS;
            }
        }
    }
}
