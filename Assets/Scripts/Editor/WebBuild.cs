using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MojiBattle.EditorTools
{
    public static class WebBuild
    {
        [MenuItem("MojiBattle/Build Web (Vercel)")]
        public static void Build()
        {
            string output = Environment.GetEnvironmentVariable("MOJI_WEB_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) output = "Builds/WebGL";
            output = Path.GetFullPath(output);
            AchievementSetup.Prepare();
            ProjectSetup.SetupChallengeScene();
            ProjectSetup.SetupDraftScene();
            ProjectSetup.SetupRunScene();
            ProjectSetup.SetupWorldCupScene();
            PlayerSettings.WebGL.template = "PROJECT:MojiBattle";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.runInBackground = false;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || !scenes[0].EndsWith("/Title.unity"))
                throw new InvalidOperationException("The Web build must start with Title.unity.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Web build failed: " + report.summary.result);
            File.Copy(Path.Combine(Application.dataPath, "WebGLTemplates/MojiBattle/vercel.json"),
                Path.Combine(output, "vercel.json"), true);
            File.Copy(Path.Combine(Application.dataPath, "WebGLTemplates/MojiBattle/.vercelignore"),
                Path.Combine(output, ".vercelignore"), true);
            foreach (string license in Directory.GetFiles(Path.Combine(Application.dataPath, "Art/Fonts"), "OFL.txt", SearchOption.AllDirectories))
            {
                string fontName = new DirectoryInfo(Path.GetDirectoryName(license)).Name;
                string directory = Path.Combine(output, "Licenses", fontName);
                Directory.CreateDirectory(directory);
                File.Copy(license, Path.Combine(directory, "OFL.txt"), true);
            }
            string unicodeLicense = Path.Combine(Application.dataPath,"Resources/Achievements/Unicode-LICENSE.txt");
            string cupFontLicense=Path.Combine(Application.dataPath,"Resources/UI/WorldCup/Fonts/OFL.txt");
            if(File.Exists(cupFontLicense))
            {string dir=Path.Combine(output,"Licenses/WorldCupHeading");Directory.CreateDirectory(dir);File.Copy(cupFontLicense,Path.Combine(dir,"OFL.txt"),true);}
            if(File.Exists(unicodeLicense))
            {
                string licenseDir=Path.Combine(output,"Licenses/Unicode"); Directory.CreateDirectory(licenseDir);
                File.Copy(unicodeLicense,Path.Combine(licenseDir,"LICENSE.txt"),true);
            }
            File.WriteAllText(Path.Combine(output, "build-info.json"), JsonUtility.ToJson(new BuildInfo
            {
                unity = Application.unityVersion,
                version = Application.version,
                builtAtUtc = DateTime.UtcNow.ToString("O")
            }, true));
            Debug.Log("[MojiBattle] Web build ready: " + output + " (" + report.summary.totalSize + " bytes)");
        }

        [Serializable]
        class BuildInfo
        {
            public string unity, version, builtAtUtc;
        }
    }
}
