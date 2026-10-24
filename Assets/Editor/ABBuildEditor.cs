using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

/// <summary>
/// ABBuildEditor - AssetBundle打包工具
///
/// 放在Editor文件夹下，只在编辑器中运行
///
/// 功能：
/// 1. 一键打包所有AB
/// 2. 打包后自动生成files.txt（用于热更新版本管理）
/// 3. 清空AB包
///
/// 使用方法：
/// 在Unity菜单栏点击 Tools → Build AssetBundles
///
/// 为什么需要自动生成 files.txt？
/// files.txt 是热更新模块 VersionManager 判断哪些文件需要更新的依据。
/// 每行格式：文件名|文件大小|哈希（当前哈希用0占位，后续可升级为MD5）
/// 手动维护 files.txt 容易出错，打包时自动生成最可靠。
/// </summary>
public class ABBuildEditor : EditorWindow
{
    // AB输出目录
    private static string OutputPath = "Assets/StreamingAssets/AssetBundles"; 

    [MenuItem("Tools/Build AssetBundles")]
    public static void BuildAllBundles()
    {
        // 确保输出目录存在
        if (!Directory.Exists(OutputPath))
        {
            Directory.CreateDirectory(OutputPath);
        }

        // 构建AB包
        // BuildAssetBundleOptions.None: 使用默认选项
        // BuildTarget.StandaloneWindows64: 当前平台
        BuildPipeline.BuildAssetBundles(
            OutputPath,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64
        );

        //将Lua脚本 也复制到 输出目录
        string luaSourceDir = "Assets/Lua";
        string luaOutputDir = OutputPath + "/Lua/";
        if (Directory.Exists(luaSourceDir))
        {
            if (Directory.Exists(luaOutputDir))
                Directory.Delete(luaOutputDir, true);
            Directory.CreateDirectory(luaOutputDir);
            foreach (string luaFile in Directory.GetFiles(luaSourceDir, "*.lua", SearchOption.AllDirectories))
            {
                string relativePath = luaFile.Substring(luaSourceDir.Length);
                string destPath = luaOutputDir + relativePath;
                string destDir = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                File.Copy(luaFile, destPath, true);
            }
            Debug.Log($"[ABBuildEditor] Lua脚本已复制到: {luaOutputDir}");
        }

        Debug.Log($"[ABBuildEditor] AB包已打包到: {OutputPath}");

        // 打包完成后自动生成 files.txt
        GenerateFileList(OutputPath);

        // 刷新Project窗口
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 单独生成 files.txt（不重新打包）
    /// 如果之前已经打包过，但 files.txt 丢了或被修改了，可以用这个重新生成
    /// </summary>
    [MenuItem("Tools/Generate files.txt")]
    public static void GenerateFileListOnly()
    {
        if (!Directory.Exists(OutputPath))
        {
            Debug.LogError($"[ABBuildEditor] 输出目录不存在: {OutputPath}，请先打包");
            return;
        }
        GenerateFileList(OutputPath);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 自动生成 files.txt
    /// 遍历AB输出目录，收集每个AB文件的文件大小，生成 files.txt
    /// </summary>
    private static void GenerateFileList(string outputPath)
    {
        var sb = new StringBuilder();
        
        // 获取输出目录下所有文件（含子目录）
        string[] allFiles = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);

        foreach (string filePath in allFiles)
        {
            string fileName = Path.GetFileName(filePath);

            // 跳过以下文件类型：
            // - .manifest：Unity自动生成的清单文件，不是AB包本身
            // - .meta：Unity资源元数据文件
            // - AssetBundles / AssetBundles.manifest：总的AB清单，不是业务AB包
            if (fileName.EndsWith(".manifest") || fileName.EndsWith(".meta"))
                continue;
            if (fileName == "AssetBundles" || fileName == "AssetBundles.manifest")
                continue;
            // 跳过 files.txt 和 version.txt 自身（如果已存在）
            if (fileName == "files.txt" || fileName == "version.txt")
                continue;

            // 获取文件大小（字节）
            string relativePath = filePath.Substring(outputPath.Length + 1).Replace("\\", "/");

            FileInfo info = new FileInfo(filePath);
            long fileSize = info.Length;

            sb.AppendLine($"{relativePath}|{fileSize}|0");
        }

        // 写入 files.txt
        string filesPath = Path.Combine(outputPath, "files.txt");
        File.WriteAllText(filesPath, sb.ToString());

        Debug.Log($"[ABBuildEditor] files.txt 已生成，共记录 {sb.ToString().Trim().Split('\n').Length} 个文件");
    }

    [MenuItem("Tools/Clear AssetBundles")]
    public static void ClearAllBundles()
    {
        if (Directory.Exists(OutputPath))
        {
            Directory.Delete(OutputPath, true);
            AssetDatabase.Refresh();
            Debug.Log("[ABBuildEditor] AB包已清理");
        }
    }
}
