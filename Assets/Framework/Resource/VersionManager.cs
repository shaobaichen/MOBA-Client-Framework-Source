using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// VersionManager - 版本管理 + 热更新核心
/// 
/// 职责：
/// 1. 管理本地版本号
/// 2. 下载远程 version.txt 和 files.txt
/// 3. 对比文件差异，找出需要更新的文件
/// 4. 增量下载 + 断点续传
/// 
/// 文件结构：
/// - version.txt: 只包含版本号，如 "1.0.3"
/// - files.txt: 每行一个文件信息 "文件名|大小|哈希"
/// 
/// 存储路径：
/// - 本地资源: Application.streamingAssetsPath/AssetBundles/
/// - 更新资源: Application.persistentDataPath/AssetBundles/
/// 
/// 为什么用 persistentDataPath 存更新文件？
/// StreamingAssets 是只读的，无法写入。
/// persistentDataPath 是可读写的，用于存放下载的更新文件。
/// 加载资源时，优先加载 persistentDataPath 下的文件（更新的版本）。
/// </summary>
public class VersionManager : MonoBehaviour
{
    private static VersionManager _instance;
    public static VersionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[VersionManager]");
                _instance = go.AddComponent<VersionManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public static event System.Action OnComplete;

    // ========== 路径配置 ==========
    /// <summary>
    /// 本地只读资源路径（打包进安装包）
    /// </summary>
    private string _localPath;

    /// <summary>
    /// 可读写路径（存放更新后的文件）
    /// </summary>
    private string _updatePath;

    /// <summary>
    /// 远程服务器地址（模拟用）
    /// </summary>
    private string _remoteUrl = "http://127.0.0.1:8080/AssetBundles/";

    // ========== 版本数据 ==========
    private string _localVersion = "0.0.0";
    private string _remoteVersion = "0.0.0";

    public string LocalVersion => _localVersion; //供外部获取当前版本号
    private Dictionary<string, FileInfoData> _remoteFileInfo = new Dictionary<string, FileInfoData>();

    /// <summary>
    /// 文件信息数据结构
    /// </summary>
    public class FileInfoData
    {
        public string FileName;  // 文件名
        public long Size;        // 文件大小
        public string Hash;      // 文件哈希
    }

    /// <summary>
    /// 热更状态
    /// </summary>
    public enum UpdateState
    {
        None,           // 未开始
        CheckingVersion,// 检查版本
        NeedUpdate,     // 需要更新
        Downloading,    // 下载中
        Complete,       // 更新完成
        Error           // 出错
    }

    public UpdateState CurrentState { get; private set; } = UpdateState.None;

    /// <summary>
    /// 下载进度（0-1）
    /// </summary>
    public float Progress { get; private set; }

    /// <summary>
    /// 当前操作描述
    /// </summary>
    public string StatusMessage { get; private set; }

    

    private void Awake()
    {
        _localPath = Application.streamingAssetsPath + "/AssetBundles/";
        _updatePath = Application.persistentDataPath + "/AssetBundles/";
    }

    // ========== 公共方法 ==========

    /// <summary>
    /// 开始检查更新
    /// 外部调用入口
    /// </summary>
    public void StartUpdate()
    {
        CurrentState = UpdateState.CheckingVersion;
        StatusMessage = "检查版本...";
        StartCoroutine(CheckVersion());
    }

    /// <summary>
    /// 检查版本号
    /// 下载远程version.txt对比本地版本
    /// </summary>
    private System.Collections.IEnumerator CheckVersion()
    {
        // 1. 读取本地版本号
        _localVersion = ReadLocalVersion();
        Debug.Log($"[VersionManager] 本地版本: {_localVersion}");

        // 2. 下载远程版本号
        using (UnityWebRequest req = UnityWebRequest.Get(_remoteUrl + "version.txt"))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("[VersionManager] 无法连接服务器，直接进入游戏");
                CurrentState = UpdateState.Complete;
                OnComplete?.Invoke();
                yield break;
            }

            _remoteVersion = req.downloadHandler.text.Trim();
            Debug.Log($"[VersionManager] 远程版本: {_remoteVersion}");
        }

        // 3. 比较版本号
        if (CompareVersion(_localVersion, _remoteVersion) >= 0)
        {
            // 本地版本 >= 远程版本，不需要更新
            Debug.Log("[VersionManager] 已是最新版本");
            CurrentState = UpdateState.Complete;
            OnComplete?.Invoke();
            yield break;
        }

        // 4. 需要更新，下载远程文件列表
        CurrentState = UpdateState.NeedUpdate;
        StatusMessage = $"发现新版本: {_remoteVersion}";
        yield return StartCoroutine(DownloadFileList());
    }

    /// <summary>
    /// 下载远程文件列表
    /// </summary>
    private System.Collections.IEnumerator DownloadFileList()
    {
        StatusMessage = "获取文件列表...";

        using (UnityWebRequest req = UnityWebRequest.Get(_remoteUrl + "files.txt"))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                StatusMessage = "获取文件列表失败";
                CurrentState = UpdateState.Error;
                OnComplete?.Invoke();
                yield break;
            }

            // 解析远程文件列表
            ParseFileList(req.downloadHandler.text);
            Debug.Log($"[VersionManager] 远程文件数: {_remoteFileInfo.Count}");
        }

        // 开始增量下载
        yield return StartCoroutine(DownloadFiles());
    }

    /// <summary>
    /// 增量下载文件
    /// 只下载本地没有的、或哈希不一致的文件
    /// </summary>
    private System.Collections.IEnumerator DownloadFiles()
    {
        CurrentState = UpdateState.Downloading;
        int totalFiles = _remoteFileInfo.Count;
        int downloadedCount = 0;

        foreach (var kvp in _remoteFileInfo)
        {
            string fileName = kvp.Key;
            FileInfoData fileInfo = kvp.Value;

            // 检查本地文件是否存在且哈希一致
            string localFilePath = _updatePath + fileName;
            if (File.Exists(localFilePath) && VerifyFileHash(localFilePath, fileInfo.Hash))
            {
                // 文件已存在且一致，跳过
                downloadedCount++;
                continue;
            }

            // 需要下载此文件
            StatusMessage = $"下载 {fileName} ({downloadedCount + 1}/{totalFiles})";
            yield return StartCoroutine(DownloadFile(fileName, fileInfo));

            downloadedCount++;
            Progress = (float)downloadedCount / totalFiles;
        }

        // 所有文件下载完毕，更新本地版本号
        SaveLocalVersion(_remoteVersion);

        // 通知ResourceManager切换加载路径到更新目录
        ResourceManager.Instance.SetBasePath(_updatePath);

        StatusMessage = "更新完成！";
        CurrentState = UpdateState.Complete;
        Debug.Log("[VersionManager] 热更新完成！");
        OnComplete?.Invoke();
    }

    /// <summary>
    /// 下载单个文件（支持断点续传）
    /// </summary>
   /// <summary>
/// 下载单个文件（支持断点续传）
/// </summary>
private System.Collections.IEnumerator DownloadFile(string fileName, FileInfoData fileInfo)
{
    string filePath = _updatePath + fileName;
    string tempPath = filePath + ".tmp"; // 临时文件

    // 确保目录存在
    string dir = Path.GetDirectoryName(filePath);
    if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);

    // 检查是否有未完成的下载（断点续传）
    long existLength = 0;
    if (File.Exists(tempPath))
    {
        FileInfo tempInfo = new FileInfo(tempPath);
        existLength = tempInfo.Length;
    }

    // 如果已下载完成大小等于文件大小，跳过
    if (existLength >= fileInfo.Size)
    {
        File.Move(tempPath, filePath);
        yield break;
    }

    // 发起断点续传请求
    using (UnityWebRequest req = UnityWebRequest.Get(_remoteUrl + fileName))
    {
        // 设置断点续传的请求头
        if (existLength > 0)
        {
            req.SetRequestHeader("Range", $"bytes={existLength}-");
            Debug.Log($"[VersionManager] 断点续传 {fileName} 已有 {existLength} 字节");
        }

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[VersionManager] 下载失败: {fileName}");
            yield break;
        }

        // 追加写入文件
        byte[] data = req.downloadHandler.data;
        using (FileStream fs = new FileStream(tempPath, FileMode.Append, FileAccess.Write))
        {
            fs.Write(data, 0, data.Length);
        }

        // 下载完成后重命名
        FileInfo finalInfo = new FileInfo(tempPath);
        if (finalInfo.Length >= fileInfo.Size)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
            File.Move(tempPath, filePath);
            Debug.Log($"[VersionManager] 下载完成: {fileName}");
        }
    }
}

    // ========== 工具方法 ==========

    /// <summary>
    /// 读取本地版本号
    /// </summary>
    private string ReadLocalVersion()
    {
        string path = _updatePath + "version.txt";
        // 优先读更新目录下的版本号
        if (File.Exists(path))
            return File.ReadAllText(path).Trim();

        // 没有更新过，读初始版本
        path = _localPath + "version.txt";
        if (File.Exists(path))
            return File.ReadAllText(path).Trim();

        return "0.0.0";
    }

    /// <summary>
    /// 保存本地版本号
    /// </summary>
    private void SaveLocalVersion(string version)
    {
        string dir = _updatePath;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(_updatePath + "version.txt", version);
        _localVersion = version;
    }

    /// <summary>
    /// 解析files.txt
    /// 格式: 文件名|大小|哈希
    /// </summary>
    private void ParseFileList(string text)
    {
        _remoteFileInfo.Clear();
        string[] lines = text.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            string[] parts = trimmed.Split('|');
            if (parts.Length >= 3)
            {
                FileInfoData info = new FileInfoData
                {
                    FileName = parts[0],
                    Size = long.Parse(parts[1]),
                    Hash = parts[2]
                };
                _remoteFileInfo[info.FileName] = info;
            }
        }
    }

    /// <summary>
    /// 验证文件哈希
    /// </summary>
    private bool VerifyFileHash(string filePath, string targetHash)
    {
        if (!File.Exists(filePath))
            return false;
        
        // 简单实现：用文件大小做验证（实际项目用MD5）
        FileInfo info = new FileInfo(filePath);
        return info.Length.ToString() == targetHash;
    }

    /// <summary>
    /// 比较版本号
    /// 返回: >0 本地更新, 0 相同, <0 远程更新
    /// </summary>
    private int CompareVersion(string local, string remote)
    {
        Version vLocal = new Version(local);
        Version vRemote = new Version(remote);
        return vLocal.CompareTo(vRemote);
    }
}
