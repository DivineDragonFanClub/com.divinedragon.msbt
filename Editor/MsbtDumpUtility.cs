using UnityEngine;
using UnityEditor;
using System.IO;
using DivineDragon.Msbt;
using Dragonstone;

namespace DivineDragon.Msbt.Editor
{
    public static class MsbtPaths
    {
        public static string GameBuildPath => EngageAddressableSettings.GameBuildPath;
        public static string MessageRoot => GameBuildPath + "/fe_assets_message";

        public static bool IsConfigured => !string.IsNullOrEmpty(GameBuildPath);

        /// <summary>
        /// Builds the source bundle / project extracted / project dumped paths for any
        /// 4-char Engage-convention language code (e.g., "USen", "JPja", "EUfr").
        /// </summary>
        public static (string bundleFolder, string extractedPath, string dumpedPath) For(Language lang)
        {
            string country = lang.Country;
            string codeLower = lang.Code.ToLowerInvariant();
            string countryLower = country.ToLowerInvariant();
            return (
                bundleFolder:  $"{MessageRoot}/{countryLower}/{codeLower}",
                extractedPath: $"Assets/Share/Addressables/Message/{country}/{lang.Code}",
                dumpedPath:    $"Assets/Share/Addressables/Message/{country}/{lang.Code}_dumped"
            );
        }
    }

    public static class MsbtDumper
    {
        /// <summary>
        /// Loads one MSBT .bytes file and writes its AstraScript dump to outputPath.
        /// Creates outputPath's directory if missing. Returns true on success.
        /// </summary>
        public static bool DumpFile(string bytesPath, string outputPath)
        {
            try
            {
                string outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                MessageBundle bundle = MessageBundle.Load(bytesPath);
                File.WriteAllText(outputPath, bundle.ToAstraScript());
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MsbtDumper] Failed to dump {bytesPath} -> {outputPath}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads every .bytes MSBT under sourceFolder and writes the AstraScript text dump
        /// to outputFolder as .txt files. Returns the number of files successfully processed.
        /// </summary>
        public static int DumpMsbtFolder(string sourceFolder, string outputFolder)
        {
            if (!Directory.Exists(sourceFolder))
            {
                Debug.LogWarning($"[MsbtDumper] Source folder not found: {sourceFolder}");
                return 0;
            }

            string[] bytesFiles = Directory.GetFiles(sourceFolder, "*.bytes", SearchOption.AllDirectories);
            if (bytesFiles.Length == 0)
            {
                return 0;
            }

            Directory.CreateDirectory(outputFolder);

            int processedCount = 0;
            int errorCount = 0;

            try
            {
                for (int i = 0; i < bytesFiles.Length; i++)
                {
                    string filePath = bytesFiles[i];
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string outputPath = Path.Combine(outputFolder, fileName + ".txt");

                    EditorUtility.DisplayProgressBar("Dumping MSBT Files",
                        $"Processing {fileName}",
                        (float)i / bytesFiles.Length);

                    if (DumpFile(filePath, outputPath))
                    {
                        processedCount++;
                    }
                    else
                    {
                        errorCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"MSBT dump: processed {processedCount}, errors {errorCount}, output {outputFolder}");
            return processedCount;
        }

        /// <summary>
        /// Walks Assets/Share/Addressables/Patch/*/Message/&lt;country&gt;/&lt;lang&gt;/*.bytes and dumps each
        /// patch's MSBT folder to a &lt;lang&gt;_dumped sibling. Returns total files processed across patches.
        /// </summary>
        public static int DumpPatchMessages(string lang)
        {
            if (string.IsNullOrEmpty(lang))
            {
                return 0;
            }

            const string patchRoot = "Assets/Share/Addressables/Patch";
            if (!Directory.Exists(patchRoot))
            {
                return 0;
            }

            int total = 0;
            foreach (string patchDir in Directory.GetDirectories(patchRoot))
            {
                string messageDir = Path.Combine(patchDir, "Message").Replace("\\", "/");
                if (!Directory.Exists(messageDir)) continue;

                foreach (string countryDir in Directory.GetDirectories(messageDir))
                {
                    string langDir = Path.Combine(countryDir, lang).Replace("\\", "/");
                    if (!Directory.Exists(langDir)) continue;

                    string dumpedDir = langDir + "_dumped";
                    total += DumpMsbtFolder(langDir, dumpedDir);
                }
            }

            if (total > 0)
            {
                AssetDatabase.Refresh();
            }

            return total;
        }
    }
}
