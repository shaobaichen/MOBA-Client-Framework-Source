# MOBA Client Framework — Social Lobby Source Showcase

> **Source-code showcase only.** This repository is not a standalone runnable Unity project.
> Unity scenes, prefabs, copyrighted art, bundled AssetBundles, external DLLs,
> native plugins, generated Protobuf bindings and bundled xLua package sources
> are intentionally excluded. Screenshots on the author's GitHub profile show
> the actual Unity client; the code here is a curated source snapshot.

## What this project implements

A Unity 6 client-side framework and social lobby demo, focusing on:

- Account registration, login, lobby/player information UI
- Friend requests, friend list, online-status notifications and private messaging
- TCP message framing, message routing and Protobuf request/response definitions
- C# / xLua controller interaction and event-driven UI updates
- AssetBundle loading, dependencies, reference-counted handles and an experimental update pipeline
- Local/mock test server used to exercise client messaging

**Scope:** This is a client framework and social-lobby demo, **not a finished MOBA combat game**. Matchmaking, hero abilities and full MOBA battles are not implemented in this snapshot.

## Source code layout

```text
Assets/
  Framework/
    GameManager.cs
    UI/             # UIManager, BasePanel, UpdatePanel
    Net/            # TCP, message routing, network bridge, mock server, .proto protocols
    Lua/            # xLua loader and module management
    Resource/       # AssetBundles, version checking, resource handles
    Config/         # Config loading
    Event/          # EventCenter
    Data/           # DataManager
  Game/             # Login, Register, Hall, Friend, Chat, Activity and common UI scripts
  Lua/Controller/   # Lua controllers for login, registration, friends and chat
  Editor/           # AssetBundle build tooling
```

## Main data flow

```text
LoginPanel / HallPanel / ChatPanel
          |  user interaction
          v
Lua Controllers <---> EventCenter / DataManager
          |  API calls
          v
     GameNetwork
          |  Protobuf messages
          v
    MessageRouter
          |  framed bytes
          v
      NetManager  <--TCP-->  MockServer (demo only)
```

## Technology and dependencies

- Unity Editor version in the original project: **6000.3.10f1**
- C# and Lua / xLua
- TCP sockets and Google Protobuf
- Unity UI (UGUI)
- AssetBundle / UnityWebRequest

Third-party xLua, Google.Protobuf, other platform binaries and any proprietary
art are **not** redistributed here. To build an executable, appropriately
licensed dependencies and the original Unity scene/prefab setup are required.

## Known limitations

This is an educational/demo implementation and **not production-ready**:

- The socket receive buffer and heartbeat/reconnection logic need hardening.
- The update pipeline needs real content hashes and robust failure recovery.
- The mock login protocol is not a secure production authentication system;
  test UI code may log password fields and must be fixed before real user use.
- Runtime Lua module/cache invalidation and update verification need additional testing.
- The MockServer lacks production authorization, thread-safety and persistent chat features.

See the source for implementation detail. This public snapshot is provided to
show architecture and development practice; it is not a security-reviewed
service or a complete distributable game.

## Copyright and redistribution

This snapshot intentionally excludes third-party game art and dependencies.
No license is granted here for excluded third-party content. No open-source
license has been applied to the author's source in this snapshot; viewing the
public repository does not by itself grant reuse rights.
