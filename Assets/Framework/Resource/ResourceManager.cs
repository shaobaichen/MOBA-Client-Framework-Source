using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;


public class ResourceManager : MonoBehaviour
{
    private static ResourceManager _instance;
    public static ResourceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[ResourceManager]");
                _instance = go.AddComponent<ResourceManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    /// <summary>
    /// Bundle运行时缓存。
    /// Key: bundleName
    /// Value: Bundle运行时信息，包括AssetBundle、RefCount、依赖列表
    /// </summary>
    private readonly Dictionary<string, ResourceItem> _bundleCache = new Dictionary<string, ResourceItem>();

    /// <summary>
    /// 资源名到AB包名的映射。
    /// 教学阶段先硬编码，后续可由打包工具生成配置。
    /// </summary>
    private readonly Dictionary<string, string> _assetToBundle = new Dictionary<string, string>();

    /// <summary>
    /// AssetBundleManifest 是 Unity 生成的依赖描述文件。
    /// 它能告诉我们：某个 Bundle 依赖哪些其他 Bundle。
    /// </summary>
    private AssetBundleManifest _manifest;

    /// <summary>
    /// AB包基础路径。
    /// 默认从 StreamingAssets/AssetBundles 读取。
    /// 热更新完成后，VersionManager 会调用 SetBasePath 切换到 persistentDataPath。
    /// </summary>
    private string _basePath;

    /// <summary>
    /// Manifest 所在的 Bundle 名。
    /// Unity 默认会生成一个和输出目录同名的主 Manifest Bundle。
    /// 如果你的AB输出目录叫 AssetBundles，则通常主包名就是 AssetBundles。
    /// </summary>
    private string _manifestBundleName = "AssetBundles";

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _basePath = Application.streamingAssetsPath + "/AssetBundles/";
        InitAssetMap();
        LoadManifest();
    }

    /// <summary>
    /// 初始化资源映射。
    /// 商业项目中，这部分应该由资源构建流程生成 catalog/json。
    /// </summary>
    private void InitAssetMap()
    {
        _assetToBundle["LoginPanel"] = "loginpanel";
        _assetToBundle["LoginBg"] = "loginbg";
        _assetToBundle["HallPanel"] = "hallpanel";
        _assetToBundle["UpdatePanel"] = "updatepanel";
        _assetToBundle["ActivityPanel"] = "activitypanel";
        _assetToBundle["RegisterPanel"] = "registerpanel";
        _assetToBundle["FriendPanel"] = "friendpanel";
        _assetToBundle["ChatPanel"] = "chatpanel";
        _assetToBundle["ConfirmPanel"] = "confirmpanel";
        _assetToBundle["TipPanel"] = "tippanel";

        _assetToBundle["spr_bg_spring"] = "activity_spring";
        _assetToBundle["spr_bg_dragon"] = "activity_dragon";
        _assetToBundle["spr_bg_summer"] = "activity_summer";
        _assetToBundle["spr_bg_IG"] = "activity_IG";
        _assetToBundle["spr_bg_winter"] = "activity_winter";

        _assetToBundle["activity_config"] = "config_activity";

        _assetToBundle["lua/controller/logincontroller.lua"] = "luascripts";

    }

    /// <summary>
    /// V2 核心：加载 AssetBundleManifest。
    ///
    /// Manifest 本身也存放在一个 AssetBundle 里。
    /// 加载流程：
    /// 1. 找到主 Manifest Bundle 文件
    /// 2. AssetBundle.LoadFromFile 加载主包
    /// 3. 从主包里 LoadAsset&lt;AssetBundleManifest&gt;("AssetBundleManifest")
    /// 4. 卸载主包本身，但保留 Manifest 对象
    /// </summary>
    public bool LoadManifest(string manifestBundleName = null)
    {
        if (!string.IsNullOrEmpty(manifestBundleName))
            _manifestBundleName = manifestBundleName;

        string manifestPath = GetBundlePath(_manifestBundleName);
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning($"[ResourceManager] 未找到Manifest文件: {manifestPath}。当前将以无依赖模式运行。请确认是否已经构建AssetBundle。");
            _manifest = null;
            return false;
        }

        AssetBundle manifestBundle = AssetBundle.LoadFromFile(manifestPath);
        if (manifestBundle == null)
        {
            Debug.LogError($"[ResourceManager] Manifest Bundle加载失败: {manifestPath}");
            _manifest = null;
            return false;
        }

        _manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
        manifestBundle.Unload(false);

        if (_manifest == null)
        {
            Debug.LogError("[ResourceManager] 主包中没有找到 AssetBundleManifest 资源");
            return false;
        }

        Debug.Log("[ResourceManager] Manifest加载成功");
        return true;
    }

    /// <summary>
    /// 从AB加载资源。
    /// V3关键点：资源加载成功后，才对目标Bundle和依赖Bundle增加引用计数。
    /// </summary>
    public T LoadAsset<T>(string assetName) where T : Object
    {
        ResourceHandle<T> handle = LoadAssetHandle<T>(assetName);
        return handle != null ? handle.Asset : null;
    }

    /// <summary>
    /// V4核心入口：加载资源并返回句柄。
    ///
    /// 句柄的意义：
    /// 1. 保存Asset，业务可以通过 handle.Asset 使用资源
    /// 2. 保存assetName，Release时可以自动回到ResourceManager释放引用
    /// 3. 记录是否已经释放，避免重复Release导致RefCount错误
    /// </summary>
    public ResourceHandle<T> LoadAssetHandle<T>(string assetName) where T : Object
    {
            if (!_assetToBundle.TryGetValue(assetName, out string bundleName))
            {
                Debug.Log($"[ResourceManager] 资源 '{assetName}' 从Resources加载");
                T fallbackAsset = LoadFromResources<T>(assetName);
                return fallbackAsset != null ? new ResourceHandle<T>(assetName, fallbackAsset, false, this) : null;
            }

        AssetBundle bundle = LoadBundleWithDependencies(bundleName);
        if (bundle == null)
        {
            Debug.LogWarning($"[ResourceManager] AB加载失败，尝试从Resources加载: {assetName}");
            T fallbackAsset = LoadFromResources<T>(assetName);
            return fallbackAsset != null ? new ResourceHandle<T>(assetName, fallbackAsset, false, this) : null;
        }

        T asset = bundle.LoadAsset<T>(assetName);
        if (asset == null)
        {
            Debug.LogError($"[ResourceManager] 从AB包 '{bundleName}' 中加载资源 '{assetName}' 失败");
            T fallbackAsset = LoadFromResources<T>(assetName);
            return fallbackAsset != null ? new ResourceHandle<T>(assetName, fallbackAsset, false, this) : null;
        }

        RetainBundleTree(bundleName);
        return new ResourceHandle<T>(assetName, asset, true, this);
    }

    /// <summary>
    /// 将资源句柄绑定到某个GameObject。
    /// 当GameObject被Destroy时，ResourceHandleOwner会自动Release句柄。
    /// </summary>
    public void BindHandleToOwner(ResourceHandle handle, GameObject owner)
    {
        if (handle == null || owner == null)
            return;

        ResourceHandleOwner handleOwner = owner.GetComponent<ResourceHandleOwner>();
        if (handleOwner == null)
            handleOwner = owner.AddComponent<ResourceHandleOwner>();

        handleOwner.AddHandle(handle);
    }

    /// <summary>
    /// 释放一个资源对应的Bundle引用。
    ///
    /// V3阶段业务需要在资源不再使用时主动调用。
    /// 例如UI面板关闭时：ReleaseAsset("LoginPanel")。
    ///
    /// 注意：这里释放的是资源对应Bundle树的引用，不是直接Destroy场景对象。
    /// </summary>
    public void ReleaseAsset(string assetName)
    {
        if (!_assetToBundle.TryGetValue(assetName, out string bundleName))
            return;

        ReleaseBundleTree(bundleName);
    }

    /// <summary>
    /// V2 核心：加载Bundle及其所有依赖。
    ///
    /// 注意加载顺序：
    /// 1. 先加载依赖 Bundle
    /// 2. 再加载目标 Bundle
    ///
    /// 为什么？
    /// 因为目标资源在反序列化时，可能需要依赖包中的材质、贴图、Shader、字体等对象。
    /// </summary>
    private AssetBundle LoadBundleWithDependencies(string bundleName)
    {
        bundleName = NormalizeBundleName(bundleName);

        LoadDependencies(bundleName);
        return LoadBundle(bundleName);
    }

    /// <summary>
    /// 加载依赖。
    ///
    /// Manifest.GetAllDependencies(bundleName) 返回所有依赖，包含直接依赖和间接依赖。
    /// 例如：A -> B -> C，那么 GetAllDependencies(A) 通常会返回 B 和 C。
    /// </summary>
    private void LoadDependencies(string bundleName)
    {
        if (_manifest == null)
        {
            // 没有Manifest时无法知道依赖，只能降级为V1模式。
            return;
        }

        string[] dependencies = _manifest.GetAllDependencies(bundleName);
        for (int i = 0; i < dependencies.Length; i++)
        {
            string dependencyBundle = NormalizeBundleName(dependencies[i]);
            LoadBundle(dependencyBundle);
        }
    }

    /// <summary>
    /// 加载单个Bundle。
    /// 这里只负责单包加载，不处理依赖。
    /// </summary>
    private AssetBundle LoadBundle(string bundleName)
    {
        bundleName = NormalizeBundleName(bundleName);

        if (_bundleCache.TryGetValue(bundleName, out ResourceItem cachedItem))
        {
            return cachedItem.Bundle;
        }

        string path = GetBundlePath(bundleName);
        if (!File.Exists(path))
        {
            Debug.LogError($"[ResourceManager] AB包文件不存在: {path}");
            return null;
        }

        AssetBundle bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null)
        {
            Debug.LogError($"[ResourceManager] AB包加载失败: {path}");
            return null;
        }

        string[] dependencies = GetDependencies(bundleName);
        _bundleCache[bundleName] = new ResourceItem(bundleName, bundle, dependencies);
        Debug.Log($"[ResourceManager] 已加载AB包: {bundleName}");
        return bundle;
    }

    private string[] GetDependencies(string bundleName)
    {
        if (_manifest == null)
            return new string[0];

        string[] deps = _manifest.GetAllDependencies(NormalizeBundleName(bundleName));
        for (int i = 0; i < deps.Length; i++)
            deps[i] = NormalizeBundleName(deps[i]);
        return deps;
    }

    /// <summary>
    /// 对目标Bundle和它的所有依赖增加引用计数。
    ///
    /// 为什么依赖也要加？
    /// 因为目标资源运行时依赖公共图集、材质、Shader等依赖包。
    /// 只给目标Bundle加引用，公共依赖仍可能被提前释放。
    /// </summary>
    private void RetainBundleTree(string bundleName)
    {
        bundleName = NormalizeBundleName(bundleName);

        string[] dependencies = GetDependencies(bundleName);
        for (int i = 0; i < dependencies.Length; i++)
            RetainBundle(dependencies[i]);

        RetainBundle(bundleName);
    }

    private void RetainBundle(string bundleName)
    {
        bundleName = NormalizeBundleName(bundleName);

        if (_bundleCache.TryGetValue(bundleName, out ResourceItem item))
        {
            item.RefCount++;
            Debug.Log($"[ResourceManager] Retain {bundleName}, RefCount={item.RefCount}");
        }
    }

    /// <summary>
    /// 对目标Bundle和它的所有依赖减少引用计数。
    /// RefCount降到0时才卸载。
    /// </summary>
    private void ReleaseBundleTree(string bundleName)
    {
        bundleName = NormalizeBundleName(bundleName);

        string[] dependencies = GetDependencies(bundleName);
        for (int i = 0; i < dependencies.Length; i++)
            ReleaseBundle(dependencies[i]);

        ReleaseBundle(bundleName);
    }

    private void ReleaseBundle(string bundleName)
    {
        bundleName = NormalizeBundleName(bundleName);

        if (!_bundleCache.TryGetValue(bundleName, out ResourceItem item))
            return;

        item.RefCount--;
        if (item.RefCount < 0)
        {
            Debug.LogWarning($"[ResourceManager] {bundleName} RefCount小于0，说明Release调用次数多于Load调用次数");
            item.RefCount = 0;
        }

        Debug.Log($"[ResourceManager] Release {bundleName}, RefCount={item.RefCount}");

        if (item.RefCount == 0)
        {
            item.Bundle.Unload(false);
            _bundleCache.Remove(bundleName);
            Debug.Log($"[ResourceManager] RefCount为0，已卸载AB包: {bundleName}");
        }
    }

    /// <summary>
    /// 开发期降级加载。
    /// 如果AB没打出来，仍然可以从Resources目录测试UI和配置流程。
    /// </summary>
    private T LoadFromResources<T>(string assetName) where T : Object
    {
        T asset = Resources.Load<T>("Panels/" + assetName);
        if (asset != null)
            return asset;

        return Resources.Load<T>(assetName);
    }

    /// <summary>
    /// V2阶段的卸载仍然是粗粒度卸载。
    /// 真正安全的卸载需要V3引用计数来保证。
    /// </summary>
    public void UnloadBundle(string bundleName, bool unloadAllLoadedObjects = false)
    {
        bundleName = NormalizeBundleName(bundleName);

        if (_bundleCache.TryGetValue(bundleName, out ResourceItem item))
        {
            item.Bundle.Unload(unloadAllLoadedObjects);
            _bundleCache.Remove(bundleName);
            Debug.Log($"[ResourceManager] 已卸载AB包: {bundleName}");
        }
    }

    /// <summary>
    /// 卸载所有已缓存Bundle。
    /// V2阶段不知道谁还在使用这些资源，所以通常只建议在切大场景或退出游戏时调用。
    /// </summary>
    public void UnloadAll()
    {
        foreach (var kvp in _bundleCache)
        {
            kvp.Value.Bundle.Unload(false);
        }

        _bundleCache.Clear();
        Debug.Log("[ResourceManager] 已卸载所有AB包");
    }

    /// <summary>
    /// 获取Bundle路径。
    /// 热更新资源优先：persistentDataPath/AssetBundles。
    /// 安装包资源兜底：StreamingAssets/AssetBundles。
    /// </summary>
    public string GetBundlePath(string bundleName)
    {
        bundleName = NormalizeBundleName(bundleName);

        string updatePath = Application.persistentDataPath + "/AssetBundles/" + bundleName;
        if (File.Exists(updatePath))
            return updatePath;

        return _basePath + bundleName;
    }

    /// <summary>
    /// 热更新完成后，VersionManager调用该接口切换基础路径。
    /// 切换路径后需要重新加载Manifest，因为新资源目录下的依赖关系可能变化。
    /// </summary>
    public void SetBasePath(string path)
    {
        _basePath = NormalizeDirectory(path);
        LoadManifest();
    }

    public void SetManifestBundleName(string manifestBundleName)
    {
        _manifestBundleName = manifestBundleName;
        LoadManifest();
    }

    private static string NormalizeBundleName(string bundleName)
    {
        return string.IsNullOrEmpty(bundleName) ? string.Empty : bundleName.Replace("\\", "/").ToLowerInvariant();
    }

    private static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        path = path.Replace("\\", "/");
        return path.EndsWith("/") ? path : path + "/";
    }
}

/// <summary>
/// Bundle运行时信息。
///
/// V2只知道Bundle是否已加载。
/// V3要知道Bundle当前有多少使用者，所以需要RefCount。
/// </summary>
public class ResourceItem
{
    public string BundleName;
    public AssetBundle Bundle;
    public string[] Dependencies;
    public int RefCount;

    public ResourceItem(string bundleName, AssetBundle bundle, string[] dependencies)
    {
        BundleName = bundleName;
        Bundle = bundle;
        Dependencies = dependencies ?? new string[0];
        RefCount = 0;
    }
}

/// <summary>
/// V4新增：资源句柄基类。
///
/// 用基类的原因：ResourceHandleOwner需要同时保存不同类型的句柄，
/// 例如GameObject、Sprite、TextAsset，所以需要一个非泛型基类统一管理。
/// </summary>
public abstract class ResourceHandle
{
    public string AssetName { get; protected set; }
    public bool IsReleased { get; protected set; }

    public abstract void Release();
}

/// <summary>
/// V4新增：资源句柄。
///
/// T是实际资源类型，例如GameObject、Sprite、TextAsset。
/// </summary>
public class ResourceHandle<T> : ResourceHandle where T : Object
{
    public T Asset { get; private set; }

    private readonly bool _fromAssetBundle;
    private readonly ResourceManager _resourceManager;

    public ResourceHandle(string assetName, T asset, bool fromAssetBundle, ResourceManager resourceManager)
    {
        AssetName = assetName;
        Asset = asset;
        _fromAssetBundle = fromAssetBundle;
        _resourceManager = resourceManager;
        IsReleased = false;
    }

    public override void Release()
    {
        if (IsReleased)
            return;

        IsReleased = true;

        if (_fromAssetBundle && _resourceManager != null)
            _resourceManager.ReleaseAsset(AssetName);

        Asset = null;
    }
}

/// <summary>
/// V4新增：资源句柄生命周期绑定组件。
///
/// 挂到GameObject上后，GameObject销毁时会自动释放所有绑定的资源句柄。
/// </summary>
public class ResourceHandleOwner : MonoBehaviour
{
    private readonly List<ResourceHandle> _handles = new List<ResourceHandle>();

    public void AddHandle(ResourceHandle handle)
    {
        if (handle == null || _handles.Contains(handle))
            return;

        _handles.Add(handle);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _handles.Count; i++)
        {
            _handles[i]?.Release();
        }

        _handles.Clear();
    }
}