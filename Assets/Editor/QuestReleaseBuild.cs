using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// MIHLab > Build Quest release and upload to ALPHA.
/// Saves the open scenes, builds the signed release APK to Builds/release/ExperienceMachine-<version>.apk,
/// and, if the build succeeds, uploads it to the ALPHA channel with MetaUpload.
/// The keystore passwords are never stored: if Unity doesn't have them this session, it opens
/// Player Settings > Publishing Settings and asks for them. Log: .claude-runs/build-release-<code>.txt
/// </summary>
public static class QuestReleaseBuild
{
    [MenuItem("MIHLab/Build Quest release and upload to ALPHA", false, 390)]
    public static void BuildAndUpload() => Build(true);

    [MenuItem("MIHLab/Build Quest release (no upload)", false, 391)]
    public static void BuildOnly() => Build(false);

    [MenuItem("MIHLab/Check keystore passwords", false, 392)]
    public static void CheckMenu()
    {
        string msg;
        bool ok = CheckKeystore(out msg);
        Directory.CreateDirectory(".claude-runs");
        File.WriteAllText(".claude-runs/keystore-check.txt", $"{DateTime.Now} {(ok ? "OK" : "FAIL")} {msg}\n");
        EditorUtility.DisplayDialog("Keystore check", ok ? "Both passwords are right." : msg, "OK");
    }

    /// <summary>Asks the JDK's keytool to read the signing key with the passwords Unity has this session.
    /// Exit code 0 means both the keystore and the alias passwords are right. Passwords are never written anywhere.</summary>
    public static bool CheckKeystore(out string message)
    {
        string ks = PlayerSettings.Android.keystoreName, alias = PlayerSettings.Android.keyaliasName;
        string sp = PlayerSettings.Android.keystorePass, kp = PlayerSettings.Android.keyaliasPass;
        if (string.IsNullOrEmpty(sp) || string.IsNullOrEmpty(kp)) { message = "Unity has no keystore or alias password this session."; return false; }
        if (!File.Exists(ks)) { message = "Keystore file not found: " + ks; return false; }
        string keytool = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe");
        if (!File.Exists(keytool)) { message = "keytool not found; skipping the check."; return true; }
        var psi = new System.Diagnostics.ProcessStartInfo(keytool,
            $"-certreq -keystore \"{ks}\" -storepass \"{sp}\" -alias \"{alias}\" -keypass \"{kp}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var p = System.Diagnostics.Process.Start(psi))
        {
            p.StandardOutput.ReadToEnd();
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit(30000);
            if (p.ExitCode == 0) { message = "OK"; return true; }
            bool store = err.IndexOf("keystore password was incorrect", StringComparison.OrdinalIgnoreCase) >= 0
                      || err.IndexOf("password was incorrect", StringComparison.OrdinalIgnoreCase) >= 0 && err.IndexOf("keystore", StringComparison.OrdinalIgnoreCase) >= 0;
            message = store ? "The KEYSTORE password is wrong." : "The keystore password is right, but the ALIAS ('" + alias + "') password is wrong.";
            message += " Retype it in Player > Android > Publishing Settings.";
            return false;
        }
    }

    public static void Build(bool upload)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUtility.DisplayDialog("Quest build", "Switch the build platform to Android first.", "OK");
            return;
        }
        if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
        {
            SettingsService.OpenProjectSettings("Project/Player");
            EditorUtility.DisplayDialog("Quest build",
                "Unity needs the keystore passwords for this session.\n\n" +
                "Player > Android > Publishing Settings: type the Keystore password and the password for the 'peripatos' alias, " +
                "then run MIHLab > Build Quest release again.", "OK");
            return;
        }
        if (!CheckKeystore(out string ksMsg))
        {
            File.WriteAllText(".claude-runs/keystore-check.txt", $"{DateTime.Now} FAIL {ksMsg}\n");
            SettingsService.OpenProjectSettings("Project/Player");
            EditorUtility.DisplayDialog("Quest build", ksMsg, "OK");
            return;
        }
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        string version = PlayerSettings.bundleVersion;
        int code = PlayerSettings.Android.bundleVersionCode;
        Directory.CreateDirectory("Builds/release");
        Directory.CreateDirectory(".claude-runs");
        string apk = $"Builds/release/ExperienceMachine-{version}.apk";
        string log = $".claude-runs/build-release{code}.txt";
        File.WriteAllText(log, $"started {DateTime.Now} v{version} keystorePassSet=True\n");

        EditorUserBuildSettings.buildAppBundle = false;
        var scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.enabled ? s.path : null);
        scenes = Array.FindAll(scenes, s => s != null);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apk,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });
        var sum = report.summary;
        File.AppendAllText(log, $"result {sum.result} time {sum.totalTime} errors {sum.totalErrors}\nfinished {DateTime.Now}\n");
        if (sum.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Quest build", $"The build failed ({sum.totalErrors} errors). See the Console.", "OK");
            return;
        }
        if (upload)
        {
            bool started = MetaUpload.Upload(apk, "ALPHA");
            File.AppendAllText(log, $"upload started {started}\n");
            Debug.Log($"[QuestReleaseBuild] Built {apk}; upload to ALPHA started={started} (progress in .claude-runs/upload-log.txt).");
        }
        else Debug.Log($"[QuestReleaseBuild] Built {apk}.");
    }
}
