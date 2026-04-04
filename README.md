# panoptes

**Monorepo** — Unity 2D 前端 + Go 后端 + 共享 Protocol 定义。

---

## 为什么不用 Submodule

Submodule 在小团队短周期项目里几乎只带来麻烦：

```
git clone 之后要手动 git submodule update --init
切分支时 submodule 不跟着切
某人忘记 push submodule 先 push 主仓库 → 别人 pull 到悬空引用
三个人协作，每次都要记得 cd 到正确的子仓库操作
```

Gamejam 期间联调最频繁，出任何 git 问题都是在浪费时间。

---

## 仓库结构

```
panoptes/
├── .gitignore
├── README.md
├── Makefile                    # 顶层命令，统一入口
│
├── server/                     # Go 后端
│   ├── main.go
│   ├── go.mod
│   ├── go.sum
│   └── gen/                    # buf 生成的 Go 代码（已提交）
│
├── client/                     # Unity 前端
│   ├── Assets/
│   │   └── Generated/
│   │       └── Protocol/       # buf 生成的 C# 代码（已提交）
│   ├── Packages/
│   └── ProjectSettings/
│
└── protocol/                   # Proto 定义（单一数据源）
    ├── buf.yaml
    ├── buf.gen.yaml
    ├── common.proto
    ├── auth.proto
    ├── lobby.proto
    ├── game_state.proto
    ├── domestic.proto
    ├── combat.proto
    └── minister.proto
```

`protocol/` 独立在顶层而不是放在 `server/` 里，原因是它属于双方——改 proto 之后在根目录跑一条命令，生成的代码分别进入 `server/gen/` 和 `client/Assets/Generated/Protocol/`。

---

## 快速开始

### 环境依赖

| 工具 | 版本 |
|------|------|
| Unity | 2022.3 LTS+ |
| Go | 1.22+ |
| [buf](https://buf.build/docs/installation) | 最新版（仅改 proto 时需要） |

### 后端

```bash
make server
# 等价于: cd server && go run main.go
# 默认监听 :8080，PORT 环境变量可覆盖
```

### 前端

用 Unity Hub 打开 `client/` 目录作为现有项目。

### 重新生成协议代码

```bash
# 修改 protocol/*.proto 之后
make gen
# server/gen/ 和 client/Assets/Generated/Protocol/ 同时更新
git add .
git commit -m "proto: 新增 MsgXxx 消息"
```

### 检查代码

```bash
make lint
# go vet + buf lint
```

---

## 分支策略

Gamejam 15 天，三条分支够了：

```
main        始终保持可运行状态，demo 从这里出
dev         日常开发，功能完成后合并到 main
fix/xxx     紧急修复，直接从 main 拉，修完合并回 main 和 dev
```

三人协作规则：

- 不直接 push main
- dev 上自由提交，不需要 PR
- 每天结束前把 dev 合并一次到 main（确保 main 可运行）
- 冲突在 dev 上解决，不带到 main

---

## 潜在冲突点

Monorepo 唯一需要注意的是 Unity 的 meta 文件冲突：

```
client/Assets/Scripts/Network/NetworkManager.cs.meta
```

两个人同时新增文件，meta 冲突解决起来很烦。预防措施：

1. 约定分工目录不重叠（后端同学不动 `client/Assets/Scripts/`，客户端同学不动 `server/`）
2. `Assets/` 下的目录提前建好（即使是空目录也 commit 进去）
3. 遇到 meta 冲突，选择保留任意一个版本都行——meta 本质上只是 GUID，重新生成也没问题

---

## 总结

```
仓库结构：Monorepo，一个仓库三个顶层目录
分支策略：main + dev，每日合并
协议生成：make gen，一条命令双端同步
冲突预防：目录分工提前约定好
```

