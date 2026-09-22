# 键鼠映射

在发送端和接收端之间映射键盘、鼠标的 Windows 程序。界面在 `KeyboardAndMouse.App`，命令行发送端和接收端分别是 `KeyboardAndMouse.Sender`、`KeyboardAndMouse.Receiver`。默认端口 `9050`。

## 环境

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 编译

在仓库根目录：

```powershell
dotnet build KeyboardAndMouse.sln -c Release
```

## 打包可分发的程序

下面这条会生成自包含目录，目标机器不必再装 .NET：

```powershell
dotnet publish src\KeyboardAndMouse.App\KeyboardAndMouse.App.csproj -c Release -r win-x64 --self-contained true -o publish\App
```

只编译依赖框架的版本（目标机器需要安装 .NET 8 Desktop Runtime）：

```powershell
dotnet publish src\KeyboardAndMouse.App\KeyboardAndMouse.App.csproj -c Release -r win-x64 --self-contained false -o publish\App
```

命令行两端：

```powershell
dotnet publish src\KeyboardAndMouse.Sender\KeyboardAndMouse.Sender.csproj -c Release -r win-x64 --self-contained true -o publish\Sender
dotnet publish src\KeyboardAndMouse.Receiver\KeyboardAndMouse.Receiver.csproj -c Release -r win-x64 --self-contained true -o publish\Receiver
```

## 部署

把对应的 `publish\` 目录整份拷到目标 Windows 机器。

- 图形界面：运行 `KeyboardAndMouse.exe`
- 接收端：`KeyboardAndMouse.Receiver.exe [端口]`
- 发送端：`KeyboardAndMouse.Sender.exe <接收端IP> [端口]`

发送端热键：`Ctrl+Alt+Q` 开关拦截，`Ctrl+Alt+X` 退出并恢复本机输入。

## 未入库的文件

源码在 `src\`。下面这些是编译结果或本机配置，克隆后不会出现，按上面的命令重新生成即可。

| 路径 | 是什么 | 没有它会怎样 | 怎么补 |
| --- | --- | --- | --- |
| `bin\`、`obj\` | `dotnet build` 的中间输出和 exe | 不能直接双击运行 | `dotnet build` 或 `dotnet publish` |
| `publish\` | 可拷贝到别的电脑的发布目录 | 没有现成的发布包 | 使用上面的 `dotnet publish` |
| `*.exe`、`*.dll`、`*.pdb` | 编译结果 | 同上 | 重新编译 |
| `settings.json` | 本机界面配置 | 程序用默认配置启动 | 在界面里重新设置 |
