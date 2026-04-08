using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using DivineDragon.Msbt;
using Dragonstone;
using DivineDragon;

namespace DivineDragon.Msbt.Editor
{
    public static class MsbtPaths
    {
        public static string GameBuildPath => EngageAddressableSettings.GameBuildPath;
        public static string MessageRoot => GameBuildPath + "/fe_assets_message";

        // Japanese
        public static string JapaneseMessageBundlePath => MessageRoot + "/jp/jpja";
        public static string JapaneseExtractedPath => "Assets/Share/Addressables/Message/JP/JPja";
        public static string JapaneseDumpedPath => "Assets/Share/Addressables/Message/JP/JPja_scripts";

        // English (US)
        public static string EnglishMessageBundlePath => MessageRoot + "/us/usen";
        public static string EnglishExtractedPath => "Assets/Share/Addressables/Message/US/USen";
        public static string EnglishDumpedPath => "Assets/Share/Addressables/Message/US/USen_scripts";

        public static bool IsConfigured => !string.IsNullOrEmpty(GameBuildPath);
    }

    public static class MsbtDumper
    {
        [MenuItem("Divine Dragon/MSBT/Extract Japanese Messages")]
        public static void ExtractJapaneseMessages()
        {
            if (!MsbtPaths.IsConfigured)
            {
                EditorUtility.DisplayDialog("Error", "Divine Dragon Core settings are not configured. Please set the game path in Divine Dragon Core Settings.", "OK");
                return;
            }

            string bundleFolder = MsbtPaths.JapaneseMessageBundlePath;
            if (!Directory.Exists(bundleFolder))
            {
                EditorUtility.DisplayDialog("Error", $"Japanese message bundle folder not found at: {bundleFolder}", "OK");
                return;
            }

            string[] bundleFiles = Directory.GetFiles(bundleFolder, "*.bundle", SearchOption.AllDirectories);
            if (bundleFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Info", "No .bundle files found in the Japanese message folder.", "OK");
                return;
            }

            bool success = Dumper.ExtractMultipleAssets(bundleFiles);
            AssetDatabase.Refresh();

            if (success)
            {
                Debug.Log($"Successfully extracted {bundleFiles.Length} Japanese message bundles.");
            }
            else
            {
                Debug.LogError("Failed to extract Japanese message bundles.");
            }
        }

        [MenuItem("Divine Dragon/MSBT/Dump Japanese Messages")]
        public static void DumpJapaneseMessages()
        {
            string extractedFolder = MsbtPaths.JapaneseExtractedPath;
            if (!Directory.Exists(extractedFolder))
            {
                EditorUtility.DisplayDialog("Error", "Extracted message files not found. Please extract Japanese messages first using 'Extract Japanese Messages'.", "OK");
                return;
            }

            string[] bytesFiles = Directory.GetFiles(extractedFolder, "*.bytes", SearchOption.AllDirectories);
            if (bytesFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Info", "No .bytes files found in the extracted message folder.", "OK");
                return;
            }

            string outputFolder = MsbtPaths.JapaneseDumpedPath;
            Directory.CreateDirectory(outputFolder);

            int processedCount = 0;
            int errorCount = 0;

            for (int i = 0; i < bytesFiles.Length; i++)
            {
                string filePath = bytesFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string outputPath = Path.Combine(outputFolder, fileName + ".txt");

                EditorUtility.DisplayProgressBar("Dumping MSBT Files", $"Processing {fileName}", (float)i / bytesFiles.Length);

                try
                {
                    MessageBundle messageBundle = MessageBundle.Load(filePath);
                    string scriptContent = messageBundle.ToAstraScript();
                    File.WriteAllText(outputPath, scriptContent);
                    processedCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to process {filePath}: {e.Message}");
                    errorCount++;
                }
            }

            EditorUtility.ClearProgressBar();
            Debug.Log($"MSBT dump complete. Processed: {processedCount}, Errors: {errorCount}");
            Debug.Log($"Scripts dumped to: {outputFolder}");
        }

        [MenuItem("Divine Dragon/MSBT/Extract and Dump Japanese Messages")]
        public static void ExtractAndDumpJapaneseMessages()
        {
            if (!MsbtPaths.IsConfigured)
            {
                EditorUtility.DisplayDialog("Error", "Divine Dragon Core settings are not configured. Please set the game path in Divine Dragon Core Settings.", "OK");
                return;
            }

            string bundleFolder = MsbtPaths.JapaneseMessageBundlePath;
            if (!Directory.Exists(bundleFolder))
            {
                EditorUtility.DisplayDialog("Error", $"Japanese message bundle folder not found at: {bundleFolder}", "OK");
                return;
            }

            string[] bundleFiles = Directory.GetFiles(bundleFolder, "*.bundle", SearchOption.AllDirectories);
            if (bundleFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Info", "No .bundle files found in the Japanese message folder.", "OK");
                return;
            }

            bool success = Dumper.ExtractMultipleAssets(bundleFiles);
            AssetDatabase.Refresh();

            if (!success)
            {
                EditorUtility.DisplayDialog("Error", "Failed to extract Japanese message bundles.", "OK");
                return;
            }

            string extractedFolder = MsbtPaths.JapaneseExtractedPath;
            string[] bytesFiles = Directory.GetFiles(extractedFolder, "*.bytes", SearchOption.AllDirectories);
            if (bytesFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Info", "No .bytes files found after extraction.", "OK");
                return;
            }

            string outputFolder = MsbtPaths.JapaneseDumpedPath;
            Directory.CreateDirectory(outputFolder);

            int processedCount = 0;
            int errorCount = 0;

            for (int i = 0; i < bytesFiles.Length; i++)
            {
                string filePath = bytesFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string outputPath = Path.Combine(outputFolder, fileName + ".txt");

                EditorUtility.DisplayProgressBar("Dumping MSBT Files", $"Processing {fileName}", (float)i / bytesFiles.Length);

                try
                {
                    MessageBundle messageBundle = MessageBundle.Load(filePath);
                    string scriptContent = messageBundle.ToAstraScript();
                    File.WriteAllText(outputPath, scriptContent);
                    processedCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to process {filePath}: {e.Message}");
                    errorCount++;
                }
            }

            EditorUtility.ClearProgressBar();
            Debug.Log($"Extract and dump complete. Processed: {processedCount}, Errors: {errorCount}");
            Debug.Log($"Scripts dumped to: {outputFolder}");
        }

        [MenuItem("Divine Dragon/MSBT/Extract English Messages")]
        public static void ExtractEnglishMessages()
        {
            if (!MsbtPaths.IsConfigured)
            {
                EditorUtility.DisplayDialog("Error", "Divine Dragon Core settings are not configured.", "OK");
                return;
            }

            string bundleFolder = MsbtPaths.EnglishMessageBundlePath;
            if (!Directory.Exists(bundleFolder))
            {
                EditorUtility.DisplayDialog("Error", $"English message bundle folder not found at: {bundleFolder}", "OK");
                return;
            }

            string[] bundleFiles = Directory.GetFiles(bundleFolder, "*.bundle", SearchOption.AllDirectories);
            if (bundleFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Info", "No .bundle files found in the English message folder.", "OK");
                return;
            }

            bool success = Dumper.ExtractMultipleAssets(bundleFiles);
            AssetDatabase.Refresh();

            if (success)
                Debug.Log($"Successfully extracted {bundleFiles.Length} English message bundles.");
            else
                Debug.LogError("Failed to extract English message bundles.");
        }

        [MenuItem("Divine Dragon/MSBT/Dump English Messages")]
        public static void DumpEnglishMessages()
        {
            string extractedFolder = MsbtPaths.EnglishExtractedPath;
            if (!Directory.Exists(extractedFolder))
            {
                EditorUtility.DisplayDialog("Error", "Extracted English files not found. Please extract first.", "OK");
                return;
            }

            string[] bytesFiles = Directory.GetFiles(extractedFolder, "*.bytes", SearchOption.AllDirectories);
            if (bytesFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Info", "No .bytes files found.", "OK");
                return;
            }

            string outputFolder = MsbtPaths.EnglishDumpedPath;
            Directory.CreateDirectory(outputFolder);

            int processedCount = 0;
            int errorCount = 0;

            for (int i = 0; i < bytesFiles.Length; i++)
            {
                string filePath = bytesFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string outputPath = Path.Combine(outputFolder, fileName + ".txt");

                EditorUtility.DisplayProgressBar("Dumping English MSBT", $"Processing {fileName}", (float)i / bytesFiles.Length);

                try
                {
                    MessageBundle messageBundle = MessageBundle.Load(filePath);
                    string scriptContent = messageBundle.ToAstraScript();
                    File.WriteAllText(outputPath, scriptContent);
                    processedCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to process {filePath}: {e.Message}");
                    errorCount++;
                }
            }

            EditorUtility.ClearProgressBar();
            Debug.Log($"English dump complete. Processed: {processedCount}, Errors: {errorCount}");
            Debug.Log($"Scripts dumped to: {outputFolder}");
        }

        [MenuItem("Divine Dragon/MSBT/Extract and Dump English Messages")]
        public static void ExtractAndDumpEnglishMessages()
        {
            if (!MsbtPaths.IsConfigured)
            {
                EditorUtility.DisplayDialog("Error", "Divine Dragon Core settings are not configured.", "OK");
                return;
            }

            string bundleFolder = MsbtPaths.EnglishMessageBundlePath;
            if (!Directory.Exists(bundleFolder))
            {
                EditorUtility.DisplayDialog("Error", $"English message bundle folder not found at: {bundleFolder}", "OK");
                return;
            }

            string[] bundleFiles = Directory.GetFiles(bundleFolder, "*.bundle", SearchOption.AllDirectories);
            if (bundleFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Info", "No .bundle files found.", "OK");
                return;
            }

            bool success = Dumper.ExtractMultipleAssets(bundleFiles);
            AssetDatabase.Refresh();

            if (!success)
            {
                EditorUtility.DisplayDialog("Error", "Failed to extract English message bundles.", "OK");
                return;
            }

            string extractedFolder = MsbtPaths.EnglishExtractedPath;
            string[] bytesFiles = Directory.GetFiles(extractedFolder, "*.bytes", SearchOption.AllDirectories);
            if (bytesFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Info", "No .bytes files found after extraction.", "OK");
                return;
            }

            string outputFolder = MsbtPaths.EnglishDumpedPath;
            Directory.CreateDirectory(outputFolder);

            int processedCount = 0;
            int errorCount = 0;

            for (int i = 0; i < bytesFiles.Length; i++)
            {
                string filePath = bytesFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string outputPath = Path.Combine(outputFolder, fileName + ".txt");

                EditorUtility.DisplayProgressBar("Dumping English MSBT", $"Processing {fileName}", (float)i / bytesFiles.Length);

                try
                {
                    MessageBundle messageBundle = MessageBundle.Load(filePath);
                    string scriptContent = messageBundle.ToAstraScript();
                    File.WriteAllText(outputPath, scriptContent);
                    processedCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to process {filePath}: {e.Message}");
                    errorCount++;
                }
            }

            EditorUtility.ClearProgressBar();
            Debug.Log($"English extract and dump complete. Processed: {processedCount}, Errors: {errorCount}");
            Debug.Log($"Scripts dumped to: {outputFolder}");
        }

        [MenuItem("Divine Dragon/MSBT/Dump Single MSBT File")]
        public static void DumpSingleMsbtFile()
        {
            string sourceFile = EditorUtility.OpenFilePanel("Select MSBT File", "", "bytes");
            if (string.IsNullOrEmpty(sourceFile))
                return;

            string fileName = Path.GetFileNameWithoutExtension(sourceFile);
            string outputFile = EditorUtility.SaveFilePanel("Save MSBT Dump", "", fileName, "txt");
            if (string.IsNullOrEmpty(outputFile))
                return;

            try
            {
                MessageBundle messageBundle = MessageBundle.Load(sourceFile);
                string scriptContent = messageBundle.ToAstraScript();
                File.WriteAllText(outputFile, scriptContent);
                Debug.Log($"Successfully dumped {fileName} to {outputFile}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to process {sourceFile}: {e.Message}");
            }
        }

        [MenuItem("Divine Dragon/MSBT/Show Message Paths")]
        public static void ShowMessagePaths()
        {
            if (!MsbtPaths.IsConfigured)
            {
                Debug.Log("Divine Dragon Core settings are not configured.");
                return;
            }

            Debug.Log($"Game Build Path: {MsbtPaths.GameBuildPath}");
            Debug.Log($"Message Root: {MsbtPaths.MessageRoot}");
            Debug.Log($"--- Japanese ---");
            Debug.Log($"  Bundle Path: {MsbtPaths.JapaneseMessageBundlePath}");
            Debug.Log($"  Extracted: {MsbtPaths.JapaneseExtractedPath}");
            Debug.Log($"  Scripts: {MsbtPaths.JapaneseDumpedPath}");
            Debug.Log($"--- English ---");
            Debug.Log($"  Bundle Path: {MsbtPaths.EnglishMessageBundlePath}");
            Debug.Log($"  Extracted: {MsbtPaths.EnglishExtractedPath}");
            Debug.Log($"  Scripts: {MsbtPaths.EnglishDumpedPath}");
        }
    }
}