using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitFramework();
    }

    private void Start()
    {
        NetManager.Instance.Connect("127.0.0.1", 8888);

        VersionManager.OnComplete += () =>
        {
            UIManager.Instance.OpenPanel<LoginPanel>(UIManager.UILayer.Normal);
        };

        UIManager.Instance.OpenPanel<UpdatePanel>(UIManager.UILayer.Top);
        VersionManager.Instance.StartUpdate();
    }

    private void StartHotUpdate()
    {
        UIManager.Instance.OpenPanel<UpdatePanel>(UIManager.UILayer.Top);
        VersionManager.Instance.StartUpdate();
    }

    private void InitFramework()
    {
        Debug.Log("[GameManager] Init Start");

        var uiMgr = UIManager.Instance;
        var resMgr = ResourceManager.Instance;
        var cfgMgr = ConfigManager.Instance;
        var luaMgr = LuaManager.Instance;
        var verMgr = VersionManager.Instance;
        var eventCenter = EventCenter.Instance;
        var msgRouter = MessageRouter.Instance;

#if UNITY_EDITOR
        var mockServer = MockServer.Instance;
#endif

        var gameNetwork = GameNetwork.Instance;
        var dataMgr = DataManager.Instance;

        EventCenter.Instance.AddListener<string>("OnShowTip", ShowTipPopup);

        Debug.Log("[GameManager] Init Done");
    }

    private void ShowTipPopup(string msg)
    {
        var panel = UIManager.Instance.OpenPanel<TipPanel>(UIManager.UILayer.PopUp);
        panel.SetMessage(msg);
    }
    public void ShowConfirmPanel(string msg, string applicantAccount, string applicantName)
    {
        var panel = UIManager.Instance.OpenPanel<ConfirmPanel>(UIManager.UILayer.PopUp);
        panel.ApplicantAccount = applicantAccount;
        panel.ApplicantName = applicantName;
        panel.SetMessage(msg);
    }
}
