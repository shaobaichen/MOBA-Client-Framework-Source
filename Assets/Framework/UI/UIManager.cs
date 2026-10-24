using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UIManager - UI管理器
/// 
/// 职责：
/// - 创建UI根Canvas，管理三个层级（Normal/PopUp/Top）
/// - 提供统一的OpenPanel/ClosePanel接口
/// - 缓存已打开的面板，避免重复加载
/// 
/// 为什么分层级？
/// 游戏中主界面、弹窗、Loading提示需要不同的显示优先级，
/// 分三层可以精确控制谁在前谁在后。
/// </summary>
public class UIManager : MonoBehaviour
{
    private static UIManager _instance;
    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[UIManager]");
                _instance = go.AddComponent<UIManager>();
                DontDestroyOnLoad(go);
                _instance.Init();
            }
            return _instance;
        }
    }

    public enum UILayer
    {
        Normal, // 普通界面层
        PopUp,  // 弹窗层
        Top     // 顶层（Loading等）
    }

    private Transform _normalRoot;
    private Transform _popUpRoot;
    private Transform _topRoot;

    private Dictionary<string, BasePanel> _panelCache = new Dictionary<string, BasePanel>();

    private void Init()
    {
        CreateUIRoot();
    }

    /// <summary>
    /// 创建UI根节点，包含三个层级Canvas
    /// </summary>
    private void CreateUIRoot()
    {
        GameObject root = new GameObject("UIRoot");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
        
        DontDestroyOnLoad(es);
        DontDestroyOnLoad(root);

        // 创建三个层级，每个层级独立Canvas控制排序
        _normalRoot = CreateLayer(root, "Normal", 0);
        _popUpRoot = CreateLayer(root, "PopUp", 100);
        _topRoot = CreateLayer(root, "Top", 200);
    }

    private Transform CreateLayer(GameObject parent, string name, int order)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(parent.transform, false);
        Canvas c = layer.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        layer.AddComponent<GraphicRaycaster>(); 
        return layer.transform;
    }

    /// <summary>
    /// 打开面板
    /// </summary>
    public T OpenPanel<T>(UILayer layer, string panelName = null) where T : BasePanel
    {
        if (string.IsNullOrEmpty(panelName))
            panelName = typeof(T).Name;

        // 如果已存在，直接激活并刷新
        if (_panelCache.TryGetValue(panelName, out BasePanel cached))
        {
            T panel = cached as T;
            if (panel != null)
            {
                panel.gameObject.SetActive(true);
                panel.transform.SetAsLastSibling();
                panel.OnRefresh();
                return panel;
            }
        }

        // 先尝试从AssetBundle加载。
        // V4阶段改为句柄化加载，由句柄负责引用释放。
        ResourceHandle<GameObject> prefabHandle = ResourceManager.Instance.LoadAssetHandle<GameObject>(panelName);
        GameObject prefab = prefabHandle != null ? prefabHandle.Asset : null;
        // AB加载失败时，从Resources/Panels/加载（开发阶段用）
        if (prefab == null)
            prefab = Resources.Load<GameObject>("Panels/" + panelName);
        if (prefab == null)
        {
            Debug.LogError($"[UIManager] 找不到面板资源: {panelName}");
            return null;
        }

        GameObject go = Object.Instantiate(prefab);
        go.name = panelName;

        // 绑定句柄到UI实例生命周期，面板销毁时自动Release。
        if (prefabHandle != null)
            ResourceManager.Instance.BindHandleToOwner(prefabHandle, go);

        Transform layerRoot = GetLayerRoot(layer);
        go.transform.SetParent(layerRoot, false);

        T comp = go.GetComponent<T>();
        if (comp == null)
        {
            Debug.LogError($"[UIManager] Prefab缺少{typeof(T).Name}组件");
            Destroy(go);
            return null;
        }

        _panelCache[panelName] = comp;
        comp.OnCreate();
        comp.OnShow();
        return comp;
    }

    /// <summary>
    /// 关闭面板
    /// </summary>
    public void ClosePanel(string panelName)
    {
        if (_panelCache.TryGetValue(panelName, out BasePanel panel))
        {
            panel.OnHide();
            _panelCache.Remove(panelName);
            Destroy(panel.gameObject);
        }
    }

    public void ClosePanel<T>() where T : BasePanel
    {
        ClosePanel(typeof(T).Name);
    }

    public void OpenPanelByName(string panelName, UILayer layer)
{
    switch (panelName)
    {
        case "LoginPanel": OpenPanel<LoginPanel>(layer); break;
        case "HallPanel": OpenPanel<HallPanel>(layer); break;
        case "UpdatePanel": OpenPanel<UpdatePanel>(layer); break;
        case "ActivityPanel": OpenPanel<ActivityPanel>(layer); break;
    }
}

    /// <summary>
    /// 关闭所有面板
    /// </summary>
    public void CloseAll()
    {
        List<string> keys = new List<string>(_panelCache.Keys);
        foreach (string key in keys)
            ClosePanel(key);
        _panelCache.Clear();
    }

    private Transform GetLayerRoot(UILayer layer)
    {
        switch (layer)
        {
            case UILayer.Normal: return _normalRoot;
            case UILayer.PopUp: return _popUpRoot;
            case UILayer.Top: return _topRoot;
            default: return _normalRoot;
        }
    }
}
