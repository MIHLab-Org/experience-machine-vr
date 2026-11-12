using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click upload of the newest release APK to a Meta release channel (default ALPHA),
/// using Meta's ovr-platform-util (the same tool the Oculus Platform Tool window runs).
///
///   MIHLab > Upload > Set App ID and Secret...   (once per PC)
///   MIHLab > Upload > Upload newest APK to ALPHA
///
/// The App ID and App Secret are stored in EditorPrefs (this PC's user registry), never in the
/// project, so they are not committed. The log (.claude-runs/upload-log.txt) never contains the
/// secret. ovr-platform-util.exe is downloaded to Library/MIHLab/ (not committed) on first use.
/// MetaUpload.Upload(path, channel) can also be called from scripts.
/// </summary>
public static class MetaUpload
{
    const string IdKey = "MIHLab_MetaAppId";
    const string SecretKey = "MIHLab_MetaAppSecret";
    const string UtilUrl = "https://www.oculus.com/download_app/?id=1076686279105243";
    const string UtilPath = "Library/MIHLab/ovr-platform-util.exe";
    public const string LogPath = ".claude-runs/upload-log.txt";

    public static bool HasCredentials => !string.IsNullOrEmpty(EditorPrefs.GetString(IdKey, "")) && !string.IsNullOrEmpty(EditorPrefs.GetString(SecretKey, ""));
    public static bool Busy { get; private set; }

    [MenuItem("MIHLab/Upload/Set App ID and Secret...", false, 400)]
    static void OpenSettings() => MetaUploadSettingsWindow.Open();

    [MenuItem("MIHLab/Upload/Upload newest APK to ALPHA", false, 401)]
    static void UploadNewestMenu() => Upload(NewestApk(), "ALPHA");

    public static string NewestApk()
    {
        var dir = new DirectoryInfo("Builds/release");
        if (!dir.Exists) return null;
        var f = dir.GetFiles("*.apk").OrderByDescending(x => x.LastWriteTimeUtc).FirstOrDefault();
        return f?.FullName;
    }

    /// <summary>Starts the upload in the background; progress goes to .claude-runs/upload-log.txt.</summary>
    public static bool Upload(string apkPath, string channel = "ALPHA", string notes = null)
    {
        Directory.CreateDirectory(".claude-runs");
        if (Busy) { Log("An upload is already running."); return false; }
        if (string.IsNullOrEmpty(apkPath) || !File.Exists(apkPath)) { Log("No APK found: " + apkPath); return false; }
        if (!HasCredentials) { Log("No App ID / App Secret yet: MIHLab > Upload > Set App ID and Secret..."); OpenSettings(); return false; }

        string appId = EditorPrefs.GetString(IdKey, "").Trim();
        string secret = EditorPrefs.GetString(SecretKey, "").Trim();
        string util = Path.GetFullPath(UtilPath);
        string apk = Path.GetFullPath(apkPath);
        notes = notes ?? ("v" + PlayerSettings.bundleVersion + " (" + PlayerSettings.Android.bundleVersionCode + ")");

        File.WriteAllText(LogPath, $"{DateTime.Now}: uploading {Path.GetFileName(apk)} to {channel}\n");
        Busy = true;
        new Thread(() =>
        {
            try
            {
                if (!File.Exists(util))
                {
                    Log("Downloading ovr-platform-util...");
                    Directory.CreateDirectory(Path.GetDirectoryName(util));
                    using (var wc = new WebClient()) wc.DownloadFile(UtilUrl, util);
                    Log("Downloaded (" + new FileInfo(util).Length / 1024 + " KB).");
                }
                string args = $"upload-quest-build --app-id \"{appId}\" --app-secret \"{secret}\" --apk \"{apk}\" --channel \"{channel}\" --notes \"{notes.Replace("\"", "'")}\"";
                var psi = new ProcessStartInfo(util, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var p = new Process { StartInfo = psi })
                {
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) Log(Redact(e.Data, secret)); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) Log("ERR " + Redact(e.Data, secret)); };
                    p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    p.WaitForExit();
                    Log($"finished exit={p.ExitCode} {(p.ExitCode == 0 ? "SUCCESS" : "FAILED")}");
                }
            }
            catch (Exception ex) { Log("EXCEPTION " + Redact(ex.Message, secret)); Log("finished FAILED"); }
            finally { Busy = false; }
        }) { IsBackground = true }.Start();
        return true;
    }

    static string Redact(string s, string secret) => string.IsNullOrEmpty(secret) ? s : s.Replace(secret, "[secret]");

    static readonly object logLock = new object();
    static void Log(string line)
    {
        lock (logLock) File.AppendAllText(LogPath, line + "\n");
        UnityEngine.Debug.Log("[MetaUpload] " + line);
    }

    internal static void Save(string appId, string secret)
    {
        EditorPrefs.SetString(IdKey, appId.Trim());
        if (!string.IsNullOrEmpty(secret)) EditorPrefs.SetString(SecretKey, secret.Trim());
    }
    internal static string SavedAppId => EditorPrefs.GetString(IdKey, "");
    internal static bool SecretSaved => !string.IsNullOrEmpty(EditorPrefs.GetString(SecretKey, ""));
}

public class MetaUploadSettingsWindow : EditorWindow
{
    string appId, secret = "";

    public static void Open()
    {
        var w = GetWindow<MetaUploadSettingsWindow>(true, "Meta upload settings");
        w.appId = MetaUpload.SavedAppId;
        w.minSize = new Vector2(460, 170);
        w.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Both values are on the Meta developer dashboard: your app > Development > API. They are saved on this PC only (EditorPrefs), never in the project.", MessageType.Info);
        appId = EditorGUILayout.TextField("App ID", appId);
        secret = EditorGUILayout.PasswordField(MetaUpload.SecretSaved ? "App Secret (saved)" : "App Secret", secret);
        GUILayout.Space(8);
        if (GUILayout.Button("Save"))
        {
            MetaUpload.Save(appId ?? "", secret);
            secret = "";
            ShowNotification(new GUIContent("Saved"));
        }
    }
}
