#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 生成的 .sln 使用 "Any CPU"（带空格），与 csproj 的 AnyCPU 不一致，
/// 会导致 Cursor/OmniSharp 配置映射失败。在工程文件生成后自动纠正。
/// </summary>
public class SlnPlatformFixer : AssetPostprocessor
{
    const string TokenFrom = "Any CPU";
    const string TokenTo = "AnyCPU";

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.delayCall += FixSolutionIfNeeded;
    }

    static void OnGeneratedCSProjectFiles()
    {
        FixSolutionIfNeeded();
    }

    static void FixSolutionIfNeeded()
    {
        var dir = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(dir)) return;

        foreach (var sln in Directory.GetFiles(dir, "*.sln"))
        {
            var text = File.ReadAllText(sln, Encoding.UTF8);
            if (!text.Contains(TokenFrom)) continue;
            File.WriteAllText(sln, text.Replace(TokenFrom, TokenTo), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Debug.Log($"[SlnPlatformFixer] Normalized platform token in {Path.GetFileName(sln)}");
        }
    }
}
#endif
