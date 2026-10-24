using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FriendPanel : BasePanel
{
    private Button _backBtn;
    private InputField _addFriendInput;
    private Button _addBtn;
    private Text _tipText;


    public override void OnCreate()
    {
        _backBtn = Find<Button>("BackBtn");
        _addFriendInput = Find<InputField>("AddFriendInput");
        _addBtn = Find<Button>("AddBtn");
        _tipText = Find<Text>("TipText");
    }

    public override void OnShow()
    {
        if (_backBtn != null) _backBtn.onClick.AddListener(OnBackClick);
        if (_addBtn != null) _addBtn.onClick.AddListener(OnAddFriendClick);
        LuaManager.Instance.CallFunction("Controller.FriendController", "Init");
    }

    public override void OnHide()
    {
        if (_backBtn != null) _backBtn.onClick.RemoveListener(OnBackClick);
        if (_addBtn != null) _addBtn.onClick.RemoveListener(OnAddFriendClick);
    }

    private void OnBackClick()
    {
        UIManager.Instance.ClosePanel<FriendPanel>();
    }

    private void OnAddFriendClick()
    {
        string account = _addFriendInput?.text ?? "";
        if (string.IsNullOrEmpty(account)) return;

        ShowTip("添加中...");
        LuaManager.Instance.CallFunction("Controller.FriendController", "SendFriendRequest", account);
    }

    public void ShowTip(string msg)
    {
        if (_tipText != null)
            _tipText.text = msg;
        else
            EventCenter.Instance.FireString("OnShowTip", msg);
    }
}
