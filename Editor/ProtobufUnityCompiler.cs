using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;

namespace E7.Protobuf
{
    // Changes taken from https://github.com/GITAI/protobuf-unity/commit/efab3ccd73894075a7581f7bd647e0bf0320b0d4

    [InitializeOnLoad]
    internal class ProtobufUnityCompiler // : AssetPostprocessor
    {
        static ProtobufUnityCompiler()
        {
            UnityEngine.Debug.Log("Initializing ProtobufUnityCompiler");

            CompileAllInProject(true);
        }

        /// <summary>
        /// Path to the file of all protobuf files in your Unity folder.
        /// </summary>
        static string[] AllProtoFiles
        {
            get
            {
                string[] protoFiles = Directory.GetFiles(Application.dataPath, "*.proto", SearchOption.AllDirectories);
                return protoFiles;
            }
        }

        /// <summary>
        /// A parent folder of all protobuf files found in your Unity project collected together.
        /// This means all .proto files in Unity could import each other freely even if they are far apart.
        /// </summary>
        static string[] IncludePaths
        {
            get
            {
                string[] protoFiles = AllProtoFiles;

                string[] includePaths = new string[protoFiles.Length];
                for (int i = 0; i < protoFiles.Length; i++)
                {
                    string protoFolder = Path.GetDirectoryName(protoFiles[i]);
                    includePaths[i] = protoFolder;
                }
                return includePaths;
            }
        }

#if USE_POSTPROCESS
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool anyChanges = false;
            anyChanges = false;
            if (ProtoPrefs.enabled == false)
            {
                return;
            }

            foreach (string str in importedAssets)
            {
                if (CompileProtobufAssetPath(str, IncludePaths, true) == true)
                {
                    anyChanges = true;
                }
            }

            /*
            for (int i = 0; i < movedAssets.Length; i++)
            {
                CompileProtobufAssetPath(movedAssets[i]);
            }
            */

            if (anyChanges)
            {
                UnityEngine.Debug.Log(nameof(ProtobufUnityCompiler));
                AssetDatabase.Refresh();
            }
        }
#endif
        /// <summary>
        /// Called from Force Compilation button in the prefs.
        /// </summary>
        internal static void CompileAllInProject(bool ifSourceMissing)
        {
            foreach (string s in AllProtoFiles)
            {
                CompileProtobufSystemPath(s, IncludePaths, ifSourceMissing);
            }
            UnityEngine.Debug.Log(nameof(ProtobufUnityCompiler));
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static bool CompileProtobufAssetPath(string assetPath, string[] includePaths, bool ifSourceMissing)
        {
            string protoFileSystemPath = Directory.GetParent(Application.dataPath) + Path.DirectorySeparatorChar.ToString() + assetPath;
            return CompileProtobufSystemPath(protoFileSystemPath, includePaths, false);
        }

        private static bool CompileProtobufSystemPath(string protoFileSystemPath, string[] includePaths, bool ifSourceMissing)
        {
            //Do not compile changes coming from UPM package.
            if (protoFileSystemPath.Contains("Packages/com.e7.protobuf-unity")) return false;

            if (Path.GetExtension(protoFileSystemPath) == ".proto")
            {
                string csharpFilePath = protoFileSystemPath.Replace(".proto", ".cs");
                if (ifSourceMissing)
                {
                    // If .cs file exists, just return
                    if (File.Exists(csharpFilePath))
                    {
                        UnityEngine.Debug.Log("Target cs file exists, skip converting: " + csharpFilePath);
                        return false;
                    }
                    else
                    {
                        UnityEngine.Debug.Log("Target cs file does not exist, converting: " + csharpFilePath);
                    }
                }
                if (ProtoPrefs.logStandard)
                {
                    UnityEngine.Debug.Log("Protobuf Unity : Compiling " + protoFileSystemPath);
                }

                string outputPath = Path.GetDirectoryName(protoFileSystemPath);

                string options = $" --csharp_out \"{outputPath}\" ";
                foreach (string s in includePaths)
                {
                    options += $" --proto_path \"{s}\"";
                }

                // Checking if the user has set valid path (there is probably a better way)
                if (ProtoPrefs.grpcPath == "ProtobufUnity_GrpcPath" || !string.IsNullOrEmpty(ProtoPrefs.grpcPath))
                {
                    options += $" --grpc_out={outputPath} --plugin=protoc-gen-grpc={ProtoPrefs.grpcPath}";
                }
                //string combinedPath = string.Join(" ", optionFiles.Concat(new string[] { protoFileSystemPath }));

                string finalArguments = $"\"{protoFileSystemPath}\"" + options;

                if (ProtoPrefs.logStandard)
                {
                    UnityEngine.Debug.Log("Protobuf Unity : Final arguments :\n" + finalArguments);
                }

                ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = ProtoPrefs.excPath, Arguments = finalArguments };

                Process proc = new Process() { StartInfo = startInfo };
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.Start();

                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (ProtoPrefs.logStandard)
                {
                    if (output != "")
                    {
                        UnityEngine.Debug.Log("Protobuf Unity : " + output);
                    }
                    if (error == "")
                    {
                        UnityEngine.Debug.Log("Protobuf Unity : Compiled " + Path.GetFileName(protoFileSystemPath));
                    }
                }

                if (ProtoPrefs.logError && error != "")
                {
                    UnityEngine.Debug.LogError("Protobuf Unity : " + error);
                }

                if (error == "")
                {
                    // Get the asset path of the csharp file
                    string projectPath = Directory.GetParent(Application.dataPath) + "/";
                    projectPath = projectPath.Replace("\\", "/");

                    csharpFilePath = csharpFilePath.Replace("\\", "/");
                    string csAssetPath = csharpFilePath.Replace(projectPath, "");

                    // Force import the csharp file
                    AssetDatabase.ImportAsset(csAssetPath, ImportAssetOptions.ForceUpdate);
                }
            }
            return false;
        }
    }
}