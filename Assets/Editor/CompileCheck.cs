// CompileCheck — the entry point used by .github/workflows/compile-check.yml.
//
// It does nothing on purpose. Unity in batch mode compiles every script before it can
// call an -executeMethod; if any script fails to compile, Unity aborts with
// "Scripts have compiler errors" and a non-zero exit code, so the CI job turns red.
// If this method runs at all, every script compiled for the active build target
// (Android in CI), and it exits with 0. No build is made.
//
// Setup and caveats: Docs/compile-check.md

using UnityEditor;
using UnityEngine;

public static class CompileCheck
{
    public static void Run()
    {
        Debug.Log("CompileCheck: all scripts compiled for " + EditorUserBuildSettings.activeBuildTarget + ".");
        EditorApplication.Exit(0);
    }
}
