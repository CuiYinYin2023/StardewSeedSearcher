# StardewSeedSearcher 开发文档

> [!Note] 本文档描述了运行go后端的方法，跟着此文档可以搭建起:
> 原 C# 后端 \
> Go + C# 混合后端


## 1. 项目结构

仓库根目录大致如下：

```text
StardewSeedSearcher/
├─ index.html
├─ script.js
├─ style.css
├─ avatar.png / background.png / logo.png
└─ StardewSeedSearcher/
   ├─ StardewSeedSearcher.csproj
   ├─ ProgramWeb.cs              # 原 C# Web 后端入口
   ├─ SearchWorker.cs            # C# worker，供 Go 混合后端调用
   ├─ main.go                    # Go 混合后端入口
   ├─ go.mod
   ├─ Core/
   ├─ Data/
   ├─ Features/
   └─ internal/
      ├─ server/                 # Go HTTP / WebSocket 服务
      ├─ worker/                 # Go 管理 C# worker 进程
      └─ ws/                     # Go 标准库 WebSocket 薄实现
```

> [!important]
> Windows 对路径大小写不敏感，`Internal` 和 `internal` 指向同一个目录；Go 代码里按惯例使用小写 `internal`

## 2. 编程语言

开发混合后端需要两个工具链：

1. Go
2. .NET SDK

> [!note]
> 一般用户不需要安装 Go；只有开发者需要发布时可以把 Go 编译成 exe

### 2.1 安装 Go

1. 打开 Go 官方下载页：`https://go.dev/dl/`
2. 下载 Windows x64 的 `.msi` 安装包
3. 一路默认安装即可
4. 重新打开 PowerShell，执行：

```powershell
go version
```

能看到类似输出就说明安装好了：

```text
go version go1.23.x windows/amd64
```

本项目 `go.mod` 当前要求：

```text
go 1.23
```

> [!important]
> 如果你的 Go 版本低于 1.23，建议升级

### 2.2 安装 .NET SDK

1. 打开 .NET 下载页：`https://dotnet.microsoft.com/download`
2. 安装 `.NET 9 SDK`
3. 重新打开 PowerShell，执行：

```powershell
dotnet --version
```

能看到 `9.x.x` 版本即可

项目 C# 目标框架是：

```xml
<TargetFramework>net9.0</TargetFramework>
```

所以只装 .NET Runtime 不够，开发时需要 SDK

## 3. 环境检查

在仓库根目录执行：

```powershell
cd E:\Haruko386-UnOffical\CYY\StardewSeedSearcher
go version
dotnet --version
```

然后检查 C# 是否能编译：

```powershell
dotnet build .\StardewSeedSearcher\StardewSeedSearcher.csproj
```

检查 Go 是否能编译：

```powershell
cd .\StardewSeedSearcher
go build ./...
```

> [!tip]
> 目前 Go 代码没有第三方依赖，只使用标准库，所以一般不需要额外 `go get`

## 4. 运行原 C# 后端

原 C# 后端是纯 C# Web 服务，默认端口是 `5000`

从仓库根目录执行：

```powershell
dotnet build .\StardewSeedSearcher\StardewSeedSearcher.csproj
cd .\StardewSeedSearcher\bin\Debug\net9.0
.\StardewSeedSearcher.exe
```

启动后：

```text
HTTP:      http://localhost:5000
WebSocket: ws://localhost:5000/ws
```

前端右上角选择：

```text
C#: http://localhost:5000
```

## 5. 运行 Go + C# 混合后端

混合后端的分工：

- Go：HTTP API、WebSocket、goroutine 并发调度、任务分片、结果聚合
- C#：具体种子推理和计算
- 两者之间：Go 启动多个 C# worker 进程，用 JSON Lines 通过 stdin/stdout 通信

### 5.1 默认运行

> [!important]
> 建议混合后端使用 `5050`，避免和原 C# 后端的 `5000` 冲突

从仓库根目录执行：

```powershell
cd .\StardewSeedSearcher
$env:SEED_GO_ADDR="localhost:5050"
go run .
```

启动后前端右上角选择：

```text
Go＆C#: http://localhost:5050
```

### 5.2 Go 后端会自动启动 C# worker

开发环境下，Go 会尝试寻找并启动 C# 编译结果：

```text
bin\Release\net9.0\StardewSeedSearcher.dll
bin\Debug\net9.0\StardewSeedSearcher.dll
```

如果找不到，会尝试执行：

```powershell
dotnet build
```

所以第一次运行混合后端前，推荐先手动执行一次：

```powershell
dotnet build .\StardewSeedSearcher.csproj
```

如果你当前目录在仓库根目录，则执行：

```powershell
dotnet build .\StardewSeedSearcher\StardewSeedSearcher.csproj
```

### 5.3 混合后端环境变量

可以通过环境变量调整 Go 后端：

```powershell
$env:SEED_GO_ADDR="localhost:5050"
$env:SEED_GO_WORKERS="7"
$env:SEED_GO_CHUNK="10000"
go run .
```

含义：

```text
SEED_GO_ADDR     Go 混合后端监听地址，默认 localhost:5000
SEED_GO_WORKERS  C# worker 进程数量，默认 CPU 核心数 - 1
SEED_GO_CHUNK    每个 C# worker 一次处理的种子区间大小，默认 10000
```

一般只需要设置 `SEED_GO_ADDR`

## 6. 前端页面

直接双击仓库根目录的：

```text
index.html
```

右上角连接状态旁边有一个小按钮，展开后有两行：

```text
C#:     http://localhost:5000
Go＆C#: http://localhost:5050
```

点击切换到对应后端，并重新连接 WebSocket

也可以直接用 URL 参数指定后端：

```text
index.html?backend=http://localhost:5050
```

## 7. 开发流程

### 7.1 只改前端

修改：

```text
index.html
script.js
style.css
```

然后刷新浏览器即可

如果浏览器缓存比较顽固，可以按：

```text
Ctrl + F5
```

### 7.2 只改 C# 算法

修改：

```text
Core/
Features/
Data/
ProgramWeb.cs
SearchWorker.cs
```

然后重新构建：

```powershell
dotnet build .\StardewSeedSearcher\StardewSeedSearcher.csproj
```

> [!warning]
> 如果正在运行原 C# 后端或 Go 混合后端，可能会锁住 `bin\Debug\net9.0\StardewSeedSearcher.dll`先关闭正在运行的黑色命令行窗口，再重新 build

### 7.3 只改 Go 调度层

修改：

```text
StardewSeedSearcher/main.go
StardewSeedSearcher/internal/server/
StardewSeedSearcher/internal/worker/
StardewSeedSearcher/internal/ws/
```

检查：

```powershell
cd .\StardewSeedSearcher
go build ./...
```

运行：

```powershell
$env:SEED_GO_ADDR="localhost:5050"
go run .
```

## 8. 常见问题

### 8.1 `go` 不是内部或外部命令

说明 Go 没装好，或者 PATH 没刷新

处理：

1. 确认已经安装 Go
2. 关闭当前 PowerShell
3. 重新打开 PowerShell
4. 再执行：

```powershell
go version
```

### 8.2 `dotnet` 不是内部或外部命令

说明 .NET SDK 没装好，或者 PATH 没刷新

处理方式同上，安装 `.NET 9 SDK` 后重新打开 PowerShell

### 8.3 端口被占用

如果看到类似：

```text
Only one usage of each socket address is normally permitted
```

说明端口已经被另一个后端占用

处理：

- 原 C# 后端默认占用 `5000`
- Go 混合后端建议占用 `5050`

运行混合后端时使用：

```powershell
$env:SEED_GO_ADDR="localhost:5050"
go run .
```

### 8.4 `StardewSeedSearcher.dll` 被占用，无法 build

这是因为旧后端还在运行

处理：

1. 关闭正在运行的原 C# 后端窗口
2. 关闭正在运行的 Go 混合后端窗口
3. 再执行：

```powershell
dotnet build .\StardewSeedSearcher\StardewSeedSearcher.csproj
```

如果只是想验证 C# 编译，也可以临时构建 Release：

```powershell
dotnet build .\StardewSeedSearcher\StardewSeedSearcher.csproj -c Release
```

### 8.5 前端一直显示未连接

检查顺序：

1. 后端是否真的启动了
2. 前端右上角选择的地址是否正确
3. C# 后端通常是 `http://localhost:5000`
4. Go 混合后端通常是 `http://localhost:5050`
5. 浏览器按 `F12`，看 Console 有没有报错

可以用 PowerShell 测试后端健康检查：

```powershell
Invoke-RestMethod http://localhost:5000/api/health
Invoke-RestMethod http://localhost:5050/api/health
```

哪个后端没启动，哪个命令就会失败

## 9. 发布免安装版本

开发时需要 Go 和 .NET SDK，但用户不应当安装这些编程语言

```text
脆音音搜种器/
├─ Data/
├─ index.html
├─ script.js
├─ style.css
├─ avatar.png
├─ background.png
├─ logo.png
├─ StardewSeedSearcher.exe        # C# self-contained worker / 原后端
├─ StardewSeedSearcherHybrid.exe  # Go 编译出的混合后端
└─ start（点这个启动）.bat
```

**Go 发布：**

```powershell
cd .\StardewSeedSearcher
go build -o StardewSeedSearcherHybrid.exe .
```

**C# 发布：**

```powershell
dotnet publish .\StardewSeedSearcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

> [!tip]
> 发布脚本需要根据最终目录再单独整理开发阶段仅需要确保 `dotnet build`、`go build ./...` 和 `go run .` 的运行正常

