# Codex Computer Run MCP Server

<!-- mcp-name: io.github.chrispulman/codex-computer-run-mcp-server -->

Codex Computer Run MCP Server gives Codex and other MCP-capable agents direct control over a signed-in Windows desktop session.
It exposes focused tools for screenshots, mouse movement, clicks, scrolling, keyboard shortcuts, Unicode paste, cursor position, and visible window discovery.

It is implemented in C# on `net10.0-windows10.0.19041.0` using `ModelContextProtocol` `1.2.0`.

## Quick Install

Click to install in your preferred environment:

[![VS Code - Install Codex Computer Run MCP](https://img.shields.io/badge/VS_Code-Install_Codex_Computer_Run_MCP-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)](https://vscode.dev/redirect/mcp/install?name=codex-computer-run-mcp-server&config=%7B%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22CP.CodexComputerRun.Mcp.Server%400.%2A%22%2C%22--yes%22%5D%7D)
[![VS Code Insiders - Install Codex Computer Run MCP](https://img.shields.io/badge/VS_Code_Insiders-Install_Codex_Computer_Run_MCP-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](https://insiders.vscode.dev/redirect/mcp/install?name=codex-computer-run-mcp-server&config=%7B%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22CP.CodexComputerRun.Mcp.Server%400.%2A%22%2C%22--yes%22%5D%7D&quality=insiders)
[![Visual Studio - Install Codex Computer Run MCP](https://img.shields.io/badge/Visual_Studio-Install_Codex_Computer_Run_MCP-5C2D91?style=flat-square&logo=visualstudio&logoColor=white)](https://vs-open.link/mcp-install?%7B%22name%22%3A%22CP.CodexComputerRun.Mcp.Server%22%2C%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22CP.CodexComputerRun.Mcp.Server%400.%2A%22%2C%22--yes%22%5D%7D)

Note:
- These install links are prepared for the intended NuGet package identity `CP.CodexComputerRun.Mcp.Server`.
- If the latest package has not been published yet, use the manual source-build or published-executable configuration below.
- This server is Windows-only and must run from a signed-in Windows desktop session, not WSL.

## What Codex Computer Run Helps With

Codex Computer Run gives an agent a minimal, fast desktop-control layer for:

- **Observe** the full Windows virtual desktop via PNG screenshots.
- **Point** the cursor at absolute virtual-screen coordinates.
- **Click** left, right, or middle mouse buttons, including repeated clicks.
- **Scroll** the wheel at the current cursor position or supplied coordinates.
- **Press** single keys and keyboard shortcuts such as `ctrl+l` or `ctrl+shift+escape`.
- **Paste** Unicode text through the Windows clipboard using `Ctrl+V`.
- **Inspect** cursor position and visible top-level windows.

The server is designed for Codex computer-use workflows where the MCP client controls the active Windows desktop.

## Windows-Only Design

This server intentionally targets Windows:

| Area | Detail |
|------|--------|
| Target framework | `net10.0-windows10.0.19041.0` |
| Runtime guard | Exits immediately when `OperatingSystem.IsWindows()` is false |
| Desktop APIs | `user32.dll`, `kernel32.dll`, WinForms screen metadata, GDI+ PNG capture, Windows clipboard |
| Session requirement | Signed-in interactive Windows desktop |
| Transport | MCP stdio |

Do not run this server from WSL for desktop automation. Building from WSL through Windows `dotnet.exe` can work, but the MCP server itself must be launched by a Windows MCP client or Windows PowerShell session.

## Codex Protocol

When this server is active, agents should follow this operating protocol:

1. Call `screenshot` first when visual context matters.
2. Use `cursor_position` before relative manual reasoning about the current pointer location.
3. Use `list_windows` to identify visible applications before focusing or interacting with them.
4. Use `move_mouse`, `click`, `scroll`, `press_key`, `hotkey`, and `type_text` only when the intended foreground application is known.
5. Prefer `type_text` for text entry because it uses Unicode clipboard paste and is faster and more reliable than simulated per-character typing.
6. Keep screenshots small in conversation by setting `include_image` to `false` when only dimensions or a saved path are needed.

## Available MCP Tools

### `screenshot`

Captures the Windows virtual desktop as PNG.

**Parameters:**
- `path` *(optional)* - output PNG path. If omitted, the image is returned in memory and no temporary file is created.
- `include_image` *(default: `true`)* - include PNG image data in the MCP tool result.

**When to use:** Use before interacting with the desktop, after UI changes, or when the agent needs visual confirmation.

---

### `move_mouse`

Moves the cursor to absolute Windows virtual-screen coordinates.

**Parameters:**
- `x` - absolute X coordinate.
- `y` - absolute Y coordinate.
- `delay` *(optional)* - seconds to wait after the action.

**When to use:** Use before a click or hover-sensitive action.

---

### `click`

Clicks at the current cursor position or at supplied absolute coordinates.

**Parameters:**
- `x` *(optional)* - absolute X coordinate.
- `y` *(optional)* - absolute Y coordinate.
- `button` *(default: `left`)* - `left`, `right`, or `middle`.
- `clicks` *(default: `1`)* - number of clicks.
- `interval` *(default: `0.08`)* - seconds between repeated clicks.
- `delay` *(optional)* - seconds to wait after the action.

**When to use:** Use for buttons, menus, tabs, context menus, and desktop UI selection.

---

### `scroll`

Scrolls the mouse wheel.

**Parameters:**
- `amount` *(default: `-3`)* - wheel notches. Positive scrolls up, negative scrolls down.
- `x` *(optional)* - absolute X coordinate to move to before scrolling.
- `y` *(optional)* - absolute Y coordinate to move to before scrolling.
- `delay` *(optional)* - seconds to wait after the action.

**When to use:** Use for lists, pages, combo boxes, and scrollable application panes.

---

### `press_key`

Presses one keyboard key.

**Parameters:**
- `key` - key name or single character, for example `enter`, `tab`, `escape`, `f5`, `a`, `A`, `?`, or `1`.
- `duration` *(default: `0.03`)* - seconds to hold the key.
- `delay` *(optional)* - seconds to wait after the action.

**When to use:** Use for navigation keys, function keys, confirm/cancel actions, and single-character shortcuts.

---

### `hotkey`

Presses a keyboard shortcut.

**Parameters:**
- `keys` - shortcut text using `+`, comma, or space separators, for example `ctrl+l`, `ctrl+shift+escape`, or `alt+tab`.
- `delay` *(optional)* - seconds to wait after the action.

**When to use:** Use for application shortcuts, browser address bar focus, task switching, command palettes, and system shortcuts.

---

### `type_text`

Pastes Unicode text into the focused Windows application using the clipboard and `Ctrl+V`.

**Parameters:**
- `text` - text to paste.
- `delay` *(optional)* - seconds to wait after the action.

**When to use:** Use for text fields, editors, terminals, and any non-trivial text entry.

---

### `cursor_position`

Returns the current Windows cursor position as JSON.

**When to use:** Use before or after mouse actions when the agent needs exact coordinates.

---

### `list_windows`

Lists visible top-level Windows desktop windows as JSON.

**Parameters:**
- `limit` *(default: `50`)* - maximum number of windows to return.

**When to use:** Use to identify visible applications and window titles before interacting with the desktop.

## Performance And Integration Notes

- Screenshot capture avoids temporary files when `path` is omitted.
- `include_image:false` avoids PNG encoding unless a `path` is supplied.
- Mouse and keyboard actions use batched `SendInput` calls instead of legacy per-event APIs.
- `hotkey` presses all keys down and releases them in reverse order in one batch.
- Clipboard access retries briefly when another process has the clipboard open.
- Visible window enumeration caches process names by PID during each call.
- Startup enables per-monitor DPI awareness for correct coordinate and screenshot behavior on mixed-DPI displays.
- Release publishing enables single-file and ReadyToRun output for faster Codex startup.

## Solution Layout

```text
src/
|-- CodexComputerRunMCPServer/          # MCP host, tools, service layer, Win32 platform layer
|-- CodexComputerRunMCPServer.Tests/    # TUnit unit and MCP integration tests
`-- CodexComputerRunMCPServer.slnx      # Solution file

.mcp/
|-- server.json                         # MCP registry/package metadata
`-- install.md                          # Manual MCP install snippets
```

## Configuration

### Fast Codex Desktop Configuration

After publishing, Codex can launch the optimized executable directly:

```toml
[mcp_servers.codex-computer-run]
command = "D:\\Projects\\Github\\chrispulman\\CodexComputerRunMCPServer\\artifacts\\publish\\win-x64\\CodexComputerRunMCPServer.exe"
args = []
```

The checked-in `.codex/config.toml` uses this fast published-executable path.

### Manual MCP Client Configuration

Published executable:

```json
{
  "mcpServers": {
    "codex-computer-run": {
      "command": "D:\\Projects\\Github\\chrispulman\\CodexComputerRunMCPServer\\artifacts\\publish\\win-x64\\CodexComputerRunMCPServer.exe",
      "args": []
    }
  }
}
```

Development source run:

```json
{
  "mcpServers": {
    "codex-computer-run": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\Projects\\Github\\chrispulman\\CodexComputerRunMCPServer\\src\\CodexComputerRunMCPServer\\CodexComputerRunMCPServer.csproj",
        "--configuration",
        "Release",
        "--no-launch-profile"
      ]
    }
  }
}
```

### Are `mcp-config.development.windows.json` And `mcp-config.windows.json` Required?

No. They are optional convenience snippets for MCP clients that import JSON config files manually.

Required or primary MCP/Codex files are:
- `.mcp/server.json` for MCP package metadata.
- `.mcp/install.md` for install notes.
- `.codex/config.toml` for this local Codex workspace.
- `.mcp.json` only if your client reads repository-local MCP JSON configuration.

## Build

```powershell
dotnet restore .\CodexComputerRunMCPServer.slnx
dotnet build .\CodexComputerRunMCPServer.slnx --configuration Release
```

If a running MCP server locks the default `bin\Release` output, build to a verification output path:

```powershell
dotnet build .\CodexComputerRunMCPServer.slnx --configuration Release --no-restore /p:OutputPath=D:\Projects\Github\chrispulman\CodexComputerRunMCPServer\artifacts\verify\bin\
```

## Test

```powershell
dotnet test .\src\CodexComputerRunMCPServer.Tests\CodexComputerRunMCPServer.Tests.csproj --configuration Release
```

Coverage with TUnit/Microsoft Testing Platform:

```powershell
dotnet test .\src\CodexComputerRunMCPServer.Tests\CodexComputerRunMCPServer.Tests.csproj --configuration Release -- --coverage --coverage-output .\artifacts\test-results\coverage.cobertura.xml --coverage-output-format cobertura --results-directory .\artifacts\test-results
```

Current verification:
- 23 TUnit tests passed.
- Coverage: 100% line coverage, 98.44% branch coverage for testable code.
- Native Win32 P/Invoke shims are excluded from coverage and verified through the service boundary plus live MCP tool discovery.

## Publish

```powershell
.\scripts\publish-windows.ps1 -Runtime win-x64
```

Direct command:

```powershell
dotnet publish .\src\CodexComputerRunMCPServer\CodexComputerRunMCPServer.csproj --configuration Release --runtime win-x64 --self-contained false --output .\artifacts\publish\win-x64
```

## MCP Verification

The published `win-x64` executable was validated with an MCP stdio `initialize` and `tools/list` handshake. The server reported all 9 tools:

```text
scroll, hotkey, type_text, screenshot, list_windows, click, move_mouse, press_key, cursor_position
```

## Example Prompts For Your AI Assistant

Once configured, you can ask things like:

- "Call `screenshot` and describe the active window."
- "List visible windows and tell me which browser tabs or apps are available."
- "Move the mouse to `x=400`, `y=300`, click, then take another screenshot."
- "Press `ctrl+l`, type `https://example.com`, then press `enter`."
- "Paste this text into the focused editor using `type_text`."
- "Scroll down 5 notches and confirm what changed on screen."
- "Get the cursor position before clicking."

## Safety Notes

This server controls the active Windows desktop. Mouse, keyboard, and clipboard actions affect the currently focused application. Use it only in a trusted desktop session and pair destructive UI actions with screenshots or window checks first.
