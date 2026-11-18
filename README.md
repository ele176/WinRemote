# WinRemote

用 **iPhone / Safari 远程控制 Windows** 的开源 MVP：Windows 端运行一个可见的 Agent，手机打开网页控制台就能看到桌面画面、操作鼠标键盘，手机无需安装 App。

- **局域网模式**：`run.bat`，手机和电脑在同一个 Wi-Fi
- **公网模式**：`run-public.bat`，走 Cloudflare Tunnel，手机用蜂窝网络也能连

> 当前状态：MVP / 实验版，适合学习、验证产品思路、个人授权设备使用，还不是 TeamViewer / AnyDesk 的成熟替代品。

![公网访问流程](docs/assets/public-access-flow.svg)

## 快速开始（公网模式，推荐）

```powershell
git clone https://github.com/ele176/WinRemote.git
cd WinRemote
.\run-public.bat
```

脚本会自动编译 Agent、找空闲端口、按需下载 `cloudflared.exe`、拉起 HTTP/2 隧道。出现这两行才算连上：

```text
https://xxxx-xxxx-xxxx.trycloudflare.com
Registered tunnel connection
```

1. 手机 Safari 打开上面那条 `https://*.trycloudflare.com`。地址每次重启都会变，不要用旧地址，也不要用 `127.0.0.1` / `localhost`。
2. 在 `WinRemote Agent` 窗口里找到 `Pair code: 12345678`，在手机页面输入这 8 位配对码。

## 局域网模式

```powershell
.\run.bat
```

手机 Safari 打开终端打印的地址，例如 `http://192.168.1.23:17373`。

## 手势

![控制按钮说明](docs/assets/control-guide.svg)

| 操作 | 效果 |
| --- | --- |
| 单指触摸 | 移动鼠标 |
| 按住拖动 | 左键拖拽 |
| `L` / `R` / `2x` | 左键 / 右键 / 双击 |
| `↑` / `↓` | 滚动 |
| `⌨` | 文本输入 |

## 功能

MJPEG 屏幕流 · 鼠标与键盘控制 · 8 位配对码 · token 保护控制接口 · Cloudflare HTTPS 隧道 · 登录后自启脚本

## 安全边界

只用于控制你自己的设备，或你被明确授权管理的设备。不支持、也不会加入：隐藏运行、未授权访问、窃取凭据、绕过 Windows 锁屏 / 登录界面 / UAC 安全桌面、规避杀毒软件。

## 常见问题

| 问题 | 解决 |
| --- | --- |
| 手机打不开公网地址 | 等终端出现 `Registered tunnel connection` 再打开 |
| 页面不是 WinRemote | 端口被占用，重跑 `run-public.bat`（会自动换空闲端口） |
| 重启后旧地址失效 | 正常，临时域名会变 |
| 屏幕显示占位图 | Agent 必须在已登录、未锁屏的桌面会话里运行 |
| UAC 弹窗点不了 | 正常，UAC 安全桌面不受普通 Agent 控制 |

更多见 [Troubleshooting](docs/TROUBLESHOOTING.md)。

## 构建与开发

无需 NuGet，使用 Windows 自带的 .NET Framework 编译器：

```powershell
.\build.bat
.\scripts\smoke-test.ps1
```

```text
src/          Windows Agent（C#）
public/       手机端控制页面
scripts/      PowerShell 辅助脚本
docs/         文档与配图
```

`bin/`、`tools/`、`.runtime*/` 和 `cloudflared.exe` 不进仓库，后者按需下载。

## 文档

[Quick Start](docs/QUICKSTART.md) · [Public Access](docs/PUBLIC_ACCESS.md) · [Architecture](docs/ARCHITECTURE.md) · [Security Model](docs/SECURITY_MODEL.md) · [Troubleshooting](docs/TROUBLESHOOTING.md) · [Roadmap](docs/ROADMAP.md)

MIT License，见 [LICENSE](LICENSE)。