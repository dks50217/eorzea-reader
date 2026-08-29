# Blazor Hybrid UI (WPF + BlazorWebView)

A desktop UI whose Razor components run on full .NET, so they can call
`ReadProcessMemory` directly. This is the proven host for an FFXIV reader with a
web-style UI.

## Project file

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>my_app</RootNamespace>
    <AllowUnsafeBlocks>True</AllowUnsafeBlocks>
    <ApplicationManifest>app.manifest</ApplicationManifest>   <!-- requireAdministrator -->
    <Platforms>x64</Platforms>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebView.Wpf" Version="8.0.*" />
    <PackageReference Include="Lumina" Version="6.7.0" />
    <PackageReference Include="Lumina.Excel" Version="7.3.1" />
  </ItemGroup>
</Project>
```

## Files (the whole shell)

- **app.manifest** — `requestedExecutionLevel level="requireAdministrator"` so
  `OpenProcess` succeeds. Users launch the exe (UAC prompt); `dotnet run` may fail
  to elevate.
- **App.xaml** — `<Application StartupUri="MainWindow.xaml">`.
- **App.xaml.cs** — build the DI container, register your reader + Lumina wrapper
  as singletons via *factories* (so they construct on first use, when the game is
  running), then `Resources.Add("services", provider)`.
- **MainWindow.xaml** — a `BlazorWebView` hosting your root component:

  ```xml
  <blazor:BlazorWebView HostPage="wwwroot\index.html" Services="{StaticResource services}">
    <blazor:BlazorWebView.RootComponents>
      <blazor:RootComponent Selector="#app" ComponentType="{x:Type local:Main}" />
    </blazor:BlazorWebView.RootComponents>
  </blazor:BlazorWebView>
  ```
- **MainWindow.xaml.cs** — just `InitializeComponent()`.
- **_Imports.razor** — usings for components + `Microsoft.Extensions.DependencyInjection`.
- **Main.razor** — the UI (below).
- **wwwroot/index.html** — `<div id="app">`, `<base href="/">`, link your css,
  `<script src="_framework/blazor.webview.js">`.
- **wwwroot/css/app.css** — styles.

## DI registration pattern

```csharp
services.AddWpfBlazorWebView();
#if DEBUG
services.AddBlazorWebViewDeveloperTools();
#endif
services.AddSingleton(_ => new InventoryReader(FindGameProcess()));
services.AddSingleton(_ => {
    var p = FindGameProcess();
    var sqpack = Path.Combine(Path.GetDirectoryName(p.MainModule!.FileName)!, "sqpack");
    return new GameItems(sqpack, Language.Japanese);
});
```

Factories, not instances: constructing a reader touches the game process, which
may not exist at app startup. First injection (inside the component) is where it
happens, and any "game not running" exception surfaces where the UI can show it.

## Component: construct off-thread, poll on a timer

```razor
@inject IServiceProvider Sp
@implements IDisposable

@code {
    InventoryReader? reader; GameItems? game;
    List<InvItem> items = new(); string? error; bool ready; Timer? timer;

    protected override async Task OnInitializedAsync() {
        try {
            await Task.Run(() => {                       // heavy Lumina load off the UI thread
                reader = Sp.GetRequiredService<InventoryReader>();
                game   = Sp.GetRequiredService<GameItems>();
            });
            ready = true;
            timer = new Timer(_ => Refresh(), null, 0, 1000);
        } catch (Exception ex) { error = $"初始化失敗：{ex.Message}"; }
    }

    void Refresh() {
        try {
            var snap = reader!.Read();
            if (snap.SequenceEqual(items)) return;       // skip redraw when unchanged
            items = snap; error = null;
            InvokeAsync(StateHasChanged);                // Timer runs off-thread → marshal back
        } catch (Exception ex) { error = $"讀取失敗：{ex.Message}"; InvokeAsync(StateHasChanged); }
    }

    public void Dispose() => timer?.Dispose();
}
```

Key points: do the slow Lumina init inside `Task.Run` so the window paints
immediately; poll with `System.Threading.Timer`; always `InvokeAsync(StateHasChanged)`
because the timer callback is off the render thread; guard every read in try/catch
and surface the message (the game can close mid-session).
