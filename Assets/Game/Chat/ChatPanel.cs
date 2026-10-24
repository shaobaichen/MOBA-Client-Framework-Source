using System.Collections.Generic;
using Game.Proto;
using UnityEngine;
using UnityEngine.UI;

public class ChatPanel : BasePanel
{
    private Text _titleText;
    private Transform _msgContent;
    private InputField _inputField;
    private Button _sendBtn;
    private Button _backBtn;

    public string MyAccount;
    public string FriendAccount;

    private GameObject _ChatItemPrefab;

    public override void OnCreate()
    {
        _titleText = Find<Text>("TitleText");
        _msgContent = Find<Transform>("ScrollView/Viewport/Content");
        _inputField = Find<InputField>("InputField");
        _sendBtn = Find<Button>("SendBtn");
        _backBtn = Find<Button>("BackBtn");

        _ChatItemPrefab = Resources.Load<GameObject>("Panels/ChatItem");
    }

    public override void OnShow()
    {
        if (_sendBtn != null) _sendBtn.onClick.AddListener(OnSendClick);
        if (_backBtn != null) _backBtn.onClick.AddListener(OnBackClick);

        EventCenter.Instance.AddListener<object>(EventCenter.ON_NEW_CHAT_MESSAGE, OnNewChatMessage);
        EventCenter.Instance.AddListener<object>(EventCenter.ON_CHAT_HISTORY, OnChatHistory);
    }

    public void StartChat()
    {
        if (_titleText != null) _titleText.text = $"与{FriendAccount}的聊天";
        LuaManager.Instance.CallFunction("Controller.ChatController", "Init");
        LuaManager.Instance.CallFunction("Controller.ChatController", "RequestHistory", MyAccount, FriendAccount);
    }

    public override void OnHide()
    {
        if (_sendBtn != null) _sendBtn.onClick.RemoveListener(OnSendClick);
        if (_backBtn != null) _backBtn.onClick.RemoveListener(OnBackClick);

        EventCenter.Instance.RemoveListener<object>(EventCenter.ON_NEW_CHAT_MESSAGE, OnNewChatMessage);
        EventCenter.Instance.RemoveListener<object>(EventCenter.ON_CHAT_HISTORY, OnChatHistory);

        LuaManager.Instance.CallFunction("Controller.ChatController", "UnregisterEvents");
    }

    private void OnSendClick()
    {
        string text = _inputField?.text ?? "";
        if (string.IsNullOrEmpty(text)) return;

        _inputField.text = "";
        AddMessageToUI(MyAccount, text);   // 发送方自己看到消息
        LuaManager.Instance.CallFunction("Controller.ChatController", "SendMessage", MyAccount, FriendAccount, text);
    }

    private void OnBackClick()
    {
        UIManager.Instance.ClosePanel<ChatPanel>();
    }

    private void OnNewChatMessage(object obj)
    {
        var msg = obj as S2C_ChatMsg;
        if (msg == null || _msgContent == null) return;

        AddMessageToUI(msg.From, msg.Text);
    }

    private void OnChatHistory(object obj)
    {
        var rsp = obj as Game.Proto.S2C_ChatHistory;
        if (rsp == null || _msgContent == null) return;

        foreach (Transform child in _msgContent)
            Destroy(child.gameObject);

        foreach (var msg in rsp.Messages)
            AddMessageToUI(msg.From, msg.Text);
    }

    private void AddMessageToUI(string sender, string text)
    {
        if (_msgContent == null) return;


        GameObject item = Instantiate(_ChatItemPrefab, _msgContent);
        Transform t = item.transform;
        Text senderTxt = t.Find("SenderText")?.GetComponent<Text>();
        Text contentTxt = t.Find("ContentText")?.GetComponent<Text>();

        if (senderTxt != null) senderTxt.text = $"{sender}:";
        if (contentTxt != null) contentTxt.text = text;
    }
}
