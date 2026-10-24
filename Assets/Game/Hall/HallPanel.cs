using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HallPanel - 游戏大厅面板
/// 
/// OnShow 时从 DataManager 读取玩家数据并显示
/// </summary>
public class HallPanel : BasePanel
{
    private Button _settingBtn;
    private Button _friendBtn;
    private Button _activityBtn1;
    private Button _activityBtn2;
    private Button _activityBtn3;
    private Text _statusText;
    private Text _nicknameText;
    private Text _levelText;
    private Transform _friendListContent;

    private GameObject _friendItemPrefab;

    public override void OnCreate()
    {
        _activityBtn1 = Find<Button>("Left/ActivityBtn1");
        _activityBtn2 = Find<Button>("Left/ActivityBtn2");
        _activityBtn3 = Find<Button>("Left/ActivityBtn3");
        _settingBtn = Find<Button>("Left/SettingBtn");
        _friendBtn = Find<Button>("Left/FriendBtn");
        _statusText = Find<Text>("StatusText");
        _nicknameText = Find<Text>("Up/NicknameText");
        _levelText = Find<Text>("Up/LevelText");
        _friendListContent = Find<Transform>("Right/FriendScroll/Viewport/Content");

        _friendItemPrefab = Resources.Load<GameObject>("Panels/FriendItem");
    }

    public override void OnShow()
    {
        if (_activityBtn1 != null) _activityBtn1.onClick.AddListener(() => OnActivityClick(0));
        if (_activityBtn2 != null) _activityBtn2.onClick.AddListener(() => OnActivityClick(1));
        if (_activityBtn3 != null) _activityBtn3.onClick.AddListener(() => OnActivityClick(2));
        if (_settingBtn != null)
            _settingBtn.onClick.AddListener(OnSettingClick);
        if (_friendBtn != null)
            _friendBtn.onClick.AddListener(OnFriendClick);

        RefreshPlayerInfo();

        EventCenter.Instance.AddListener<object>(EventCenter.ON_FRIEND_LIST_UPDATED, OnFriendListUpdated);
        EventCenter.Instance.AddListener<object>(EventCenter.ON_FRIEND_STATUS_CHANGED, OnFriendStatusChanged);
        LuaManager.Instance.CallFunction("Controller.FriendController", "Init");
        LuaManager.Instance.CallFunction("Controller.FriendController", "RefreshList");

        if (_statusText != null)
        {
             _statusText.text = $"版本: {VersionManager.Instance.LocalVersion} | 已连接";
        }
    }

    public override void OnHide()
    {
        if (_activityBtn1 != null) _activityBtn1.onClick.RemoveAllListeners();
        if (_activityBtn2 != null) _activityBtn2.onClick.RemoveAllListeners();
        if (_activityBtn3 != null) _activityBtn3.onClick.RemoveAllListeners();
        if (_settingBtn != null)
            _settingBtn.onClick.RemoveListener(OnSettingClick);
        if (_friendBtn != null)
            _friendBtn.onClick.RemoveListener(OnFriendClick);

        EventCenter.Instance.RemoveListener<object>(EventCenter.ON_FRIEND_LIST_UPDATED, OnFriendListUpdated);
        EventCenter.Instance.RemoveListener<object>(EventCenter.ON_FRIEND_STATUS_CHANGED, OnFriendStatusChanged);
    }

    /// <summary>
    /// 从 DataManager 读取玩家数据并刷新 UI
    /// </summary>
    public void RefreshPlayerInfo()
    {
        if (!DataManager.Instance.IsLoggedIn) return;

        var pd = DataManager.Instance.PlayerData;
        if (_nicknameText != null)
            _nicknameText.text = pd.Nickname;
        if (_levelText != null)
            _levelText.text = $"Lv.{pd.Level}";
    }

    private void OnFriendClick()
    {
        UIManager.Instance.OpenPanel<FriendPanel>(UIManager.UILayer.Normal);
    }

    private void OnFriendListUpdated(object obj)
    {
        var list = obj as Game.Proto.S2C_FriendList;
        if (list == null || _friendListContent == null) return;

        foreach (Transform child in _friendListContent)
            Destroy(child.gameObject);

        
        

        foreach (var f in list.Friends)
        {
            GameObject item = Instantiate(_friendItemPrefab, _friendListContent);
            item.name = f.Account;
            Transform t = item.transform;

            Text nameTxt = t.Find("NicknameText")?.GetComponent<Text>();
            Text levelTxt = t.Find("LevelText")?.GetComponent<Text>();
            Image dot = t.Find("StatusDot")?.GetComponent<Image>();
            Button delBtn = t.Find("DeleteBtn")?.GetComponent<Button>();

            if (nameTxt != null) nameTxt.text = f.Nickname;
            if (levelTxt != null) levelTxt.text = $"Lv.{f.Level}";
            if (dot != null) dot.color = f.Online ? Color.green : Color.gray;
            if (delBtn != null)
            {
                string target = f.Account;
                delBtn.onClick.AddListener(() => OnDeleteFriend(target));
            }

            Button btn = item.GetComponent<Button>();
            string friendAccount = f.Account;
            if (btn != null) btn.onClick.AddListener(() => OpenChat(friendAccount));
        }
    }

    private void OnDeleteFriend(string friendAccount)
    {
        Transform item = _friendListContent.Find(friendAccount);
        if (item != null) Destroy(item.gameObject);

        LuaManager.Instance.CallFunction("Controller.FriendController", "DeleteFriend", friendAccount);
    }


    private void OpenChat(string friendAccount)
    {
        var panel = UIManager.Instance.OpenPanel<ChatPanel>(UIManager.UILayer.PopUp);
        panel.MyAccount = DataManager.Instance.CurrentAccount;
        panel.FriendAccount = friendAccount;
        panel.StartChat();
    }


    private void OnFriendStatusChanged(object obj)
    {
        var notify = obj as Game.Proto.S2C_FriendStatusNotify;
        if (notify == null || _friendListContent == null) return;

        Transform item = _friendListContent.Find(notify.Account);
        if (item == null)
        {
            LuaManager.Instance.CallFunction("Controller.FriendController", "RefreshList");
            return;
        }

        Image dot = item.Find("StatusDot")?.GetComponent<Image>();
        if (dot != null) dot.color = notify.Online ? Color.green : Color.gray;
    }

    private void OnActivityClick(int index)
    {
        Debug.Log($"[HallPanel] 打开活动{index}");
        ActivityPanel.CurrentActivityIndex = index;
        UIManager.Instance.OpenPanel<ActivityPanel>(UIManager.UILayer.PopUp);
    }

    private void OnSettingClick()
    {
        Debug.Log("[HallPanel] 打开设置");
    }
}
