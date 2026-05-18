using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DivineDragon.Msbt.Editor
{
    /// <summary>
    /// Reads AstraScript-dumped MSBT files from the Unity project, preserving the
    /// per-file structure. Lazy: nothing is read until a caller asks for a specific
    /// (file, language) pair. Auto-invalidates on asset reimport via a postprocessor.
    /// </summary>
    public static class MsbtProvider
    {
        private const string PrefsLanguage = "TerrainLocalizer_Language";
        private const string DefaultLanguage = "USen";
        private const string DumpedSuffix = "_dumped";
        private const string MessageRelativePath = "Share/Addressables/Message";
        private const string PatchRelativePath = "Share/Addressables/Patch";

        private static Language _currentLanguage;
        private static bool _currentLanguageLoaded;
        private static Language[] _availableLanguagesCache;

        // Lang → (FileId → project-relative path). Built lazily per lang.
        private static readonly Dictionary<Language, Dictionary<FileId, string>> _pathIndex =
            new Dictionary<Language, Dictionary<FileId, string>>();

        // Lang → (FileId → parsed contents). Entries appear only when callers ask for them.
        private static readonly Dictionary<Language, Dictionary<FileId, IReadOnlyDictionary<MessageId, string>>> _fileCache =
            new Dictionary<Language, Dictionary<FileId, IReadOnlyDictionary<MessageId, string>>>();

        public static event Action OnLanguageChanged;
        public static event Action OnDataChanged;

        /// <summary>
        /// Sort comparator that puts USen then JPja first (Nintendo's "main" pair),
        /// then everything else alphabetically. Use for consistent language ordering
        /// across pickers.
        /// </summary>
        public static readonly IComparer<Language> MainsFirst = new MainsFirstComparer();

        private sealed class MainsFirstComparer : IComparer<Language>
        {
            private static int Rank(Language l)
            {
                if (l.Code == "USen") return 0;
                if (l.Code == "JPja") return 1;
                return 2;
            }

            public int Compare(Language a, Language b)
            {
                int ra = Rank(a), rb = Rank(b);
                if (ra != rb) return ra - rb;
                return StringComparer.OrdinalIgnoreCase.Compare(a.Code, b.Code);
            }
        }

        public static Language CurrentLanguage
        {
            get
            {
                if (!_currentLanguageLoaded)
                {
                    _currentLanguage = new Language(EditorPrefs.GetString(PrefsLanguage, DefaultLanguage));
                    _currentLanguageLoaded = true;
                }
                return _currentLanguage;
            }
        }

        public static Language[] AvailableLanguages
        {
            get
            {
                if (_availableLanguagesCache == null)
                {
                    _availableLanguagesCache = DetectLanguages();
                }
                return _availableLanguagesCache;
            }
        }

        /// <summary>
        /// True if at least one language has a dumped GameData.txt under Assets.
        /// </summary>
        public static bool HasLocalization
        {
            get
            {
                Language[] langs = AvailableLanguages;
                if (langs == null || langs.Length == 0) return false;

                // The fallback `[ DefaultLanguage ]` array from DetectLanguages doesn't imply
                // real on-disk presence — verify by file existence.
                foreach (Language lang in langs)
                {
                    if (File.Exists(BaseGameDataFullPath(lang)))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static void SetLanguage(Language lang)
        {
            if (string.IsNullOrEmpty(lang.Code) || lang.Equals(CurrentLanguage))
            {
                return;
            }
            _currentLanguage = lang;
            _currentLanguageLoaded = true;
            EditorPrefs.SetString(PrefsLanguage, lang.Code);
            OnLanguageChanged?.Invoke();
        }

        /// <summary>
        /// Returns the localized string for (file, id, lang), or `id.Value` as a fallback when
        /// the file/key isn't present. Mirrors TerrainLocalizer.GetLocalizedName's old semantics.
        /// </summary>
        public static string Get(FileId file, MessageId id, Language lang)
        {
            IReadOnlyDictionary<MessageId, string> dict = GetFile(file, lang);
            if (dict != null && dict.TryGetValue(id, out string value))
            {
                return value;
            }
            return id.Value;
        }

        /// <summary>
        /// Returns the parsed contents of one MSBT file for a language, or null if no such
        /// file is indexed. Caches the result for the editor session.
        /// </summary>
        public static IReadOnlyDictionary<MessageId, string> GetFile(FileId file, Language lang)
        {
            if (!_fileCache.TryGetValue(lang, out Dictionary<FileId, IReadOnlyDictionary<MessageId, string>> perFile))
            {
                perFile = new Dictionary<FileId, IReadOnlyDictionary<MessageId, string>>();
                _fileCache[lang] = perFile;
            }

            if (perFile.TryGetValue(file, out IReadOnlyDictionary<MessageId, string> cached))
            {
                return cached;
            }

            Dictionary<FileId, string> index = GetPathIndex(lang);
            if (index == null || !index.TryGetValue(file, out string projectPath))
            {
                return null;
            }

            IReadOnlyDictionary<MessageId, string> loaded = ParseScriptFile(projectPath);
            perFile[file] = loaded;
            return loaded;
        }

        /// <summary>
        /// All FileIds known to exist on disk for a language. Populated lazily on first call.
        /// </summary>
        public static IEnumerable<FileId> ListFiles(Language lang)
        {
            Dictionary<FileId, string> index = GetPathIndex(lang);
            return index != null ? (IEnumerable<FileId>)index.Keys : Array.Empty<FileId>();
        }

        public static void InvalidateCache(Language lang = default)
        {
            if (string.IsNullOrEmpty(lang.Code))
            {
                _fileCache.Clear();
                _pathIndex.Clear();
                _availableLanguagesCache = null;
            }
            else
            {
                _fileCache.Remove(lang);
                _pathIndex.Remove(lang);
                _availableLanguagesCache = null;
            }
            OnDataChanged?.Invoke();
        }

        // ---- internals ----

        private static Dictionary<FileId, string> GetPathIndex(Language lang)
        {
            if (_pathIndex.TryGetValue(lang, out Dictionary<FileId, string> cached))
            {
                return cached;
            }
            Dictionary<FileId, string> built = BuildPathIndex(lang);
            _pathIndex[lang] = built;
            return built;
        }

        private static Dictionary<FileId, string> BuildPathIndex(Language lang)
        {
            var result = new Dictionary<FileId, string>();
            if (string.IsNullOrEmpty(lang.Code) || string.IsNullOrEmpty(lang.Country))
            {
                return result;
            }

            string assetsPath = Application.dataPath;

            // Base messages
            string baseRoot = Path.Combine(assetsPath, MessageRelativePath).Replace("\\", "/");
            ScanForDumpedFiles(baseRoot, lang, result);

            // Patches
            string patchRoot = Path.Combine(assetsPath, PatchRelativePath).Replace("\\", "/");
            if (Directory.Exists(patchRoot))
            {
                foreach (string patchDir in Directory.GetDirectories(patchRoot))
                {
                    string msgDir = Path.Combine(patchDir, "Message").Replace("\\", "/");
                    if (Directory.Exists(msgDir))
                    {
                        ScanForDumpedFiles(msgDir, lang, result);
                    }
                }
            }

            return result;
        }

        private static void ScanForDumpedFiles(string root, Language lang, Dictionary<FileId, string> sink)
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            string suffix = lang.Code + DumpedSuffix;

            try
            {
                foreach (string countryDir in Directory.GetDirectories(root))
                {
                    string dumpedDir = Path.Combine(countryDir, suffix).Replace("\\", "/");
                    if (!Directory.Exists(dumpedDir)) continue;

                    foreach (string txt in Directory.GetFiles(dumpedDir, "*.txt"))
                    {
                        var fileId = new FileId(Path.GetFileNameWithoutExtension(txt));
                        string projectRelative = ToProjectRelativePath(txt);
                        if (sink.ContainsKey(fileId))
                        {
                            Debug.LogWarning(
                                $"[MsbtProvider] FileId '{fileId}' collides for {lang}: keeping {sink[fileId]}, ignoring {projectRelative}");
                            continue;
                        }
                        sink[fileId] = projectRelative;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MsbtProvider] ScanForDumpedFiles({root}) failed: {ex.Message}");
            }
        }

        private static IReadOnlyDictionary<MessageId, string> ParseScriptFile(string projectRelativePath)
        {
            var result = new Dictionary<MessageId, string>();
            string fullPath = ToFullPath(projectRelativePath);

            try
            {
                string[] lines = File.ReadAllLines(fullPath);
                string currentKey = null;
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentKey = trimmed.Substring(1, trimmed.Length - 2);
                    }
                    else if (!string.IsNullOrEmpty(trimmed) && currentKey != null)
                    {
                        result[new MessageId(currentKey)] = trimmed;
                        currentKey = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MsbtProvider] ParseScriptFile({fullPath}) failed: {ex.Message}");
            }
            return result;
        }

        private static Language[] DetectLanguages()
        {
            var langs = new List<Language>();
            string root = Path.Combine(Application.dataPath, MessageRelativePath).Replace("\\", "/");
            if (Directory.Exists(root))
            {
                SearchForDumpedLangs(root, langs);
            }

            if (langs.Count == 0)
            {
                return new[] { new Language(DefaultLanguage) };
            }

            langs.Sort(MainsFirst);
            return langs.ToArray();
        }

        private static void SearchForDumpedLangs(string root, List<Language> langs)
        {
            try
            {
                foreach (string dir in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(dir);
                    if (name.EndsWith(DumpedSuffix, StringComparison.Ordinal))
                    {
                        if (File.Exists(Path.Combine(dir, "GameData.txt")))
                        {
                            string code = name.Substring(0, name.Length - DumpedSuffix.Length);
                            var l = new Language(code);
                            if (!langs.Contains(l)) langs.Add(l);
                        }
                    }
                }
                foreach (string dir in Directory.GetDirectories(root))
                {
                    SearchForDumpedLangs(dir, langs);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MsbtProvider] SearchForDumpedLangs({root}) failed: {ex.Message}");
            }
        }

        private static string BaseGameDataFullPath(Language lang)
        {
            if (string.IsNullOrEmpty(lang.Code) || string.IsNullOrEmpty(lang.Country)) return null;
            return Path.Combine(Application.dataPath, MessageRelativePath, lang.Country, lang.Code + DumpedSuffix, "GameData.txt")
                .Replace("\\", "/");
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            string assetsPath = Application.dataPath;
            if (!string.IsNullOrEmpty(absolutePath) &&
                absolutePath.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
            {
                return ("Assets" + absolutePath.Substring(assetsPath.Length)).Replace("\\", "/");
            }
            return absolutePath;
        }

        private static string ToFullPath(string projectRelativePath)
        {
            if (string.IsNullOrEmpty(projectRelativePath))
            {
                return projectRelativePath;
            }
            if (projectRelativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                projectRelativePath.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(projectRoot, projectRelativePath).Replace("\\", "/");
            }
            return projectRelativePath;
        }

        // Called by the AssetPostprocessor below when Unity notices any asset change.
        internal static void OnAssetsChanged(string[] paths)
        {
            if (paths == null) return;

            bool indexNeedsRebuild = false;
            bool anyMatched = false;

            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;

                // Is this path currently indexed? If so, drop just that (lang, file).
                bool matchedExisting = false;
                foreach (KeyValuePair<Language, Dictionary<FileId, string>> langEntry in _pathIndex)
                {
                    foreach (KeyValuePair<FileId, string> pair in langEntry.Value)
                    {
                        if (string.Equals(pair.Value, path, StringComparison.OrdinalIgnoreCase))
                        {
                            if (_fileCache.TryGetValue(langEntry.Key, out var perFile))
                            {
                                perFile.Remove(pair.Key);
                            }
                            matchedExisting = true;
                            anyMatched = true;
                            break;
                        }
                    }
                    if (matchedExisting) break;
                }

                if (matchedExisting) continue;

                // Not currently indexed but looks like one of ours — invalidate the path index
                // so we re-discover newly-added/removed files on next access.
                if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                    path.IndexOf("/Message/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    path.IndexOf(DumpedSuffix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    indexNeedsRebuild = true;
                    anyMatched = true;
                }
            }

            if (indexNeedsRebuild)
            {
                _pathIndex.Clear();
                _availableLanguagesCache = null;
            }

            if (anyMatched)
            {
                OnDataChanged?.Invoke();
            }
        }
    }

    internal sealed class MsbtFileWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            MsbtProvider.OnAssetsChanged(importedAssets);
            MsbtProvider.OnAssetsChanged(deletedAssets);
            MsbtProvider.OnAssetsChanged(movedAssets);
            MsbtProvider.OnAssetsChanged(movedFromAssetPaths);
        }
    }
}
