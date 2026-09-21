# Codex Computer Run MCP Server

<!-- mcp-name: io.github.chrispulman/codex-computer-run-mcp-server -->

Codex Computer Run MCP Server gives Codex and other MCP-capable agents direct control over a signed-in desktop session.
It exposes focused tools for screenshots, mouse movement, clicks, scrolling, keyboard shortcuts, Unicode text entry, cursor position, and metadata-based window targeting, plus a bundled Codex Skill for safe desktop-use workflows.

It is implemented in C# on `net10.0` using `ModelContextProtocol` `1.3.0`.
The current package and MCP manifest version is `1.1.0`.
The package targets plain `net10.0` so it can be distributed as a .NET tool. Windows uses native Win32 APIs; Linux and macOS use best-effort command-backed adapters.

## Quick Install

Click to install in your preferred environment:

[![VS Code - Install Codex Computer Run MCP](https://img.shields.io/badge/VS_Code-Install_Codex_Computer_Run_MCP-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)](https://vscode.dev/redirect/mcp/install?name=codex-computer-run-mcp-server&config=%7B%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22CP.CodexComputerRun.Mcp.Server%401.%2A%22%2C%22--yes%22%5D%7D)
[![VS Code Insiders - Install Codex Computer Run MCP](https://img.shields.io/badge/VS_Code_Insiders-Install_Codex_Computer_Run_MCP-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](https://insiders.vscode.dev/redirect/mcp/install?name=codex-computer-run-mcp-server&config=%7B%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22CP.CodexComputerRun.Mcp.Server%401.%2A%22%2C%22--yes%22%5D%7D&quality=insiders)
[![Visual Studio - Install Codex Computer Run MCP](https://img.shields.io/badge/Visual_Studio-Install_Codex_Computer_Run_MCP-5C2D91?style=flat-square&logo=visualstudio&logoColor=white)](https://vs-open.link/mcp-install?%7B%22name%22%3A%22CP.CodexComputerRun.Mcp.Server%22%2C%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22CP.CodexComputerRun.Mcp.Server%401.%2A%22%2C%22--yes%22%5D%7D)

Note:
- These install links are prepared for the intended NuGet package identity `CP.CodexComputerRun.Mcp.Server`.
- If the latest package has not been published yet, use the manual source-build or published-executable configuration below.
- Run the server from the signed-in desktop session you want to control. Windows desktop automation must be launched from Windows, not WSL.
- Linux support expects `xdotool` for pointer and keyboard actions, `xrandr` as a display-geometry fallback, `wmctrl` or `xdotool` for window discovery, one of `gnome-screenshot`, `grim`, or ImageMagick `import` for screenshots, and one of `wl-copy`, `xclip`, `xsel`, or `xdotool` for text entry.
- macOS support uses `screencapture`, `pbcopy`, and `osascript`; pointer actions require `cliclick`. Screen Recording and Accessibility permissions may be required by macOS.

## What Codex Computer Run Helps With

Codex Computer Run gives an agent a minimal, fast desktop-control layer for:

- **Observe** the full desktop via PNG screenshots.
- **Point** the cursor at absolute virtual-screen coordinates.
- **Click** left, right, or middle mouse buttons where supported, including repeated clicks. The built-in macOS adapter supports left and right clicks.
- **Scroll** the wheel at the current cursor position or supplied coordinates.
- **Press** single keys and keyboard shortcuts such as `ctrl+l` or `ctrl+shift+escape`.
- **Enter** Unicode text through the platform's preferred text-entry path.
- **Inspect** cursor position and visible top-level windows.

The server is designed for Codex computer-use workflows where the MCP client controls the active desktop.

## Platform Support

Windows remains the primary implementation. Linux and macOS support keeps the same MCP tool surface but depends on external desktop commands that must be available inside the active graphical session.

| Area | Current behavior |
|------|------------------|
| Version | `1.1.0` |
| Target framework | `net10.0` |
| Windows | Native Win32 implementation with virtual-screen capture, direct Unicode `SendInput`, cursor position, and visible top-level window enumeration |
| Linux | Command-backed adapter using `xdotool` for pointer and keyboard input, `xrandr` for display-geometry fallback, `wmctrl` or `xdotool` for windows, screenshot command fallbacks, and text-entry command fallbacks |
| macOS | Command-backed adapter using `screencapture`, `pbcopy`, `osascript`, and `cliclick`; macOS middle-click automation is not supported by the built-in adapter |
| Unsupported OS | Deterministic unsupported-platform errors instead of silent no-ops |
| Session requirement | Signed-in interactive desktop session |
| Transport | MCP stdio |

Do not run this server from WSL to control a Windows desktop. Building from WSL through Windows `dotnet.exe` can work, but the MCP server itself must be launched by a Windows MCP client or Windows PowerShell session.

## Codex Protocol

When this server is active, agents should follow this operating protocol:

1. Call `screenshot` first when visual context matters.
2. Use `cursor_position` before relative manual reasoning about the current pointer location.
3. Use `list_windows` to identify visible applications before focusing or interacting with them.
4. Use `move_mouse`, `click`, `scroll`, `press_key`, `hotkey`, and `type_text` only when the intended foreground application is known.
5. Prefer `type_text` for text entry because Windows uses direct Unicode input without changing the clipboard; Linux/macOS use their available native text-entry fallback.
6. Keep screenshots small in conversation by setting `include_image` to `false` when only dimensions, platform metadata, or a saved path are needed.

## Codex Skill

The repository and NuGet package include a Codex Skill at `skills/codex-computer-run`. The skill teaches Codex the observation-first workflow, safety rules, and exact MCP tool names for this server.

When the packaged server starts, it tries to install the skill into the current Codex installation if `CODEX_HOME` is set or `%USERPROFILE%\.codex` already exists. Existing skill files are not overwritten during automatic install.

Manual install from a globally installed tool:

```powershell
dotnet tool install --global CP.CodexComputerRun.Mcp.Server --version 1.*
codex-computer-run-mcp-server --install-codex-skill
```

Manual install from source:

```powershell
dotnet run --project .\src\CodexComputerRunMCPServer\CodexComputerRunMCPServer.csproj -- --install-codex-skill
```

Set `CODEX_HOME` first if Codex uses a non-default location:

```powershell
$env:CODEX_HOME = "C:\Users\you\.codex"
codex-computer-run-mcp-server --install-codex-skill
```

To refresh an existing installed copy with the packaged skill files, add `--force`.

Use the skill in Codex by asking for it explicitly, for example:

```text
Use $codex-computer-run to list visible windows, take a screenshot, and confirm the active desktop state.
```

## Available MCP Tools

### `screenshot`

Captures the current desktop as PNG.

**Parameters:**
- `path` *(optional)* - output PNG path. If omitted, the image is returned in memory and no temporary file is created.
- `include_image` *(default: `true`)* - include PNG image data in the MCP tool result.
- `left`, `top`, `width`, `height` *(optional)* - capture only a screen-space region. Provide all four values together; `width` and `height` must be greater than zero.

**Response:** The first content block is JSON metadata with `message`, `path`, `mimeType`, `platform`, `left`, `top`, `width`, and `height`. When `include_image` is `true`, a PNG image block is also returned.

When a region is supplied, the metadata bounds describe that region instead of the full virtual desktop. Region coordinates use the same virtual-desktop screen space as window bounds returned by `list_windows`.

**When to use:** Use before interacting with the desktop, after UI changes, or when the agent needs visual confirmation.

---

### `move_mouse`

Moves the cursor to absolute desktop coordinates.

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
- `button` *(default: `left`)* - `left`, `right`, or `middle`; `middle` is not supported by the built-in macOS adapter.
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

Enters Unicode text into the focused application. On Windows it emits Unicode keyboard events directly and does not modify the clipboard; other platforms use their available native fallback.

**Parameters:**
- `text` - text to enter.
- `delay` *(optional)* - seconds to wait after the action.

**When to use:** Use for text fields, editors, terminals, and any non-trivial text entry.

---

### `cursor_position`

Returns the current desktop cursor position as JSON.

**When to use:** Use before or after mouse actions when the agent needs exact coordinates.

---

### `list_windows`

Lists visible top-level desktop windows as JSON.

**Parameters:**
- `limit` *(default: `50`)* - maximum number of windows to return.

**When to use:** Use to identify visible applications and window titles before interacting with the desktop.

Each window entry also includes `isForeground`, `isMinimized`, and `bounds` when the platform can provide them. `bounds` contains `left`, `top`, `width`, and `height` in virtual-desktop screen coordinates. Use these fields to confirm the intended process and target window before relying on coordinates; a title match alone is not sufficient when multiple windows are open.

---

### `find_windows`

Finds visible top-level windows by optional process name, title substring, foreground state, and minimized state. Matching is case-insensitive for process names and title text.

**Parameters:**
- `process_name` *(optional)* - process name such as `Notepad` or `msedge`.
- `title_contains` *(optional)* - case-insensitive substring of the window title.
- `foreground_only` *(optional)* - return only windows reported as foreground.
- `include_minimized` *(optional)* - include minimized windows; defaults to `true`.
- `limit` *(optional)* - maximum number of matches; defaults to 50.

**When to use:** Prefer this when several windows are open and a process/title predicate is more reliable than choosing by screen coordinates. Re-check the returned handle immediately before a data-bearing action because window handles can become stale.

---

### `screenshot_window`

Captures the screen-space bounds of a visible window selected by native handle. It uses the bounds returned by `list_windows` or `find_windows`; it does not reveal pixels hidden behind another window.

**Parameters:**
- `handle` - native window handle returned by `list_windows` or `find_windows`.
- `path` *(optional)* - output PNG path.
- `include_image` *(optional)* - include PNG bytes in the MCP result; defaults to `true`.

**When to use:** Use after targeting a window when the agent needs a focused visual observation or wants to avoid capturing the entire multi-monitor desktop.

---

### `verify_window`

Checks whether a native window handle still identifies the expected process, title, foreground state, and minimized state. It is observation-only and returns JSON with `ok`, `reason`, and current metadata.

**Parameters:**
- `handle` - native window handle returned by `list_windows` or `find_windows`.
- `process_name` *(optional)* - expected process name.
- `title_contains` *(optional)* - expected title substring.
- `require_foreground` *(optional)* - require current foreground focus.
- `allow_minimized` *(optional)* - allow a minimized match; defaults to `true`.

**When to use:** Call immediately before an input-changing action when a handle may have become stale, and after a meaningful transition when you need a machine-readable postcondition.

---

### `wait_for_window`

Waits for a visible window matching optional process, title, foreground, and minimized-state filters. It never sends input and is bounded to 30 seconds; a single failed window enumeration is retried because the query is idempotent.

**Parameters:**
- `process_name`, `title_contains`, `foreground_only`, `include_minimized` - same targeting filters as `find_windows`.
- `timeout_ms` *(optional)* - maximum wait from 0 to 30000 milliseconds; defaults to 5000.
- `poll_ms` *(optional)* - polling interval from 25 to 1000 milliseconds; defaults to 100.

**When to use:** Use after launching an identified application or waiting for a known window to return after a crash. Do not use a retry loop around clicks, keystrokes, or text entry because repeating those could duplicate a user action.

---

### `activate_window`

Brings a previously enumerated window to the foreground by its native handle.

**Parameters:**
- `handle` - native window handle returned by `list_windows`.
- `restore` *(default: `true`)* - restore the window first when it is minimized.

**When to use:** Call `list_windows` first, verify the process and title, then activate the exact handle before sending input. Windows uses the native window handle and waits until the OS reports that handle as foreground; Linux uses `wmctrl` or `xdotool`; the current macOS adapter reports a clear unsupported error because its window listing does not expose stable native handles.

---

### `close_window`

Requests a graceful close for one exact top-level window handle. The operation posts the platform's normal close request and then checks whether that handle disappeared. It never terminates the owning process. If the application presents a save dialog or rejects the request, the result contains `closed:false` and explains that the window is still present.

**Parameters:**
- `handle` - native window handle returned by `list_windows` or `find_windows`.
- `timeout_ms` *(optional)* - bounded wait from 0 to 5000 milliseconds; defaults to 1000.

**When to use:** Use only after identifying the exact window and confirming that closing it is intended. Prefer this over sending `alt+f4`, because a stale foreground can route a global shortcut to another application.

---

### Targeted keyboard input

`press_key`, `hotkey`, and `type_text` accept an optional `target_handle`. When supplied, the server re-enumerates that exact window immediately before injecting input and sends nothing if it is missing, minimized, or no longer foreground. This turns a focus race into a safe, actionable error. The recommended sequence is `find_windows` → `activate_window` → input with `target_handle`.

## Performance And Integration Notes

- Screenshot capture avoids temporary files when `path` is omitted.
- `include_image:false` avoids PNG encoding unless a `path` is supplied.
- Windows mouse and keyboard actions use batched `SendInput` calls instead of legacy per-event APIs.
- Windows `hotkey` presses all keys down and releases them in reverse order in one batch.
- Windows text entry emits direct Unicode input and leaves the clipboard unchanged.
- Windows activation uses a bounded foreground-stabilization check; activation is reported as failed when the requested handle does not actually become foreground.
- Keyboard input can be bound to an exact `target_handle`; a failed foreground check aborts before `SendInput`.
- `close_window` uses a graceful, handle-directed close request and verifies the postcondition instead of sending a global shortcut or terminating a process.
- Window enumeration uses at most one retry for observation-only queries; input-changing operations are never retried automatically.
- Windows visible window enumeration caches process names by PID during each call.
- Startup enables per-monitor DPI awareness on Windows for correct coordinate and screenshot behavior on mixed-DPI displays.
- Linux and macOS adapters fail with actionable dependency messages when required desktop commands are missing.
- Release publishing enables single-file and ReadyToRun output for faster Codex startup.

## Solution Layout

```text
CodexComputerRunMCPServer.slnx          # Root solution wrapper for CI and local pack commands

src/
|-- CodexComputerRunMCPServer/          # MCP host, tools, service layer, and platform adapters
|-- CodexComputerRunMCPServer.Tests/    # TUnit unit and MCP integration tests
`-- CodexComputerRunMCPServer.slnx      # Source solution file

.mcp/
|-- server.json                         # MCP registry/package metadata
`-- install.md                          # Manual MCP install snippets

skills/
`-- codex-computer-run/                 # Codex Skill bundled into the NuGet package
```

## Configuration

### Lifecycle Safeguards

The server allows multiple Codex sessions to start their own MCP server process so tool discovery remains available in each session. Input-changing tools still coordinate desktop control by taking an exclusive, renewable lease under the platform local application-data folder:

```text
CodexComputerRunMCPServer\control.lock
```

On Windows this is normally `%LOCALAPPDATA%\CodexComputerRunMCPServer\control.lock`. On Linux and macOS it follows .NET's local application-data location for the signed-in user, falling back to the temp directory if no local application-data path is available.

The control lease is acquired by `move_mouse`, `click`, `scroll`, `press_key`, `hotkey`, and `type_text`. If another Codex session currently owns the lease, the tool call fails with a busy message instead of allowing simultaneous mouse or keyboard input. Observation tools (`screenshot`, `cursor_position`, and `list_windows`) remain available from every session.

After the latest control action, the owning process keeps the lease briefly so follow-up clicks or keystrokes from the same session are not interleaved with another session. The lease is also released immediately when the owning MCP process exits.

Idle shutdown is disabled by default so long-lived Codex sessions can call the MCP tools later without finding a closed stdio transport. If you explicitly enable idle shutdown, every tool call updates activity state and active calls are never stopped mid-invocation.

Optional environment overrides:

| Variable | Default | Detail |
|----------|---------|--------|
| `CODEX_COMPUTER_RUN_CONTROL_LOCK` | `true` | Set `false` to disable cross-session desktop-control coordination. |
| `CODEX_COMPUTER_RUN_CONTROL_LEASE_SECONDS` | `60` | Seconds the owning session keeps desktop control after the latest input-changing action. Set `0` to release immediately after each action. |
| `CODEX_COMPUTER_RUN_IDLE_SHUTDOWN` | `false` | Set `true` to enable idle shutdown. |
| `CODEX_COMPUTER_RUN_IDLE_TIMEOUT_SECONDS` | `300` | Seconds without tool activity before shutdown when idle shutdown is enabled. Values `0` or lower disable idle shutdown. |
| `CODEX_COMPUTER_RUN_IDLE_CHECK_INTERVAL_SECONDS` | `10` | Seconds between idle checks. |

### JSONL Action Audit

Auditing is opt-in because desktop actions can involve private applications. Set `CODEX_COMPUTER_RUN_AUDIT=true` to append one JSON object per tool call. The default file is `%LOCALAPPDATA%\CodexComputerRunMCPServer\actions.jsonl` on Windows, or the current platform's local application-data directory. Override it with `CODEX_COMPUTER_RUN_AUDIT_PATH`.

Records include the UTC timestamp, tool name, safe argument metadata, duration, process ID, success state, and a summarized error when a call fails. Text content, screenshot bytes, and full filter values are intentionally omitted; `type_text` records only the character count. Audit write failures are ignored so a diagnostic log cannot break a desktop action or corrupt MCP stdout.

### Fast Codex Desktop Configuration

After publishing, Codex can launch the optimized executable directly. Use the runtime identifier that matches the OS running the signed-in desktop session.

Windows:

```toml
[mcp_servers.codex-computer-run]
command = "PathTo\\CodexComputerRunMCPServer\\artifacts\\publish\\win-x64\\CodexComputerRunMCPServer.exe"
args = []
```

Linux or macOS:

```toml
[mcp_servers.codex-computer-run]
command = "/path/to/CodexComputerRunMCPServer/artifacts/publish/linux-x64/CodexComputerRunMCPServer"
args = []
```

The checked-in `.codex/config.toml` uses the Windows fast published-executable path for this workspace.

### Manual MCP Client Configuration

Published executable:

Windows:

```json
{
  "mcpServers": {
    "codex-computer-run": {
      "command": "PathTo\\CodexComputerRunMCPServer\\artifacts\\publish\\win-x64\\CodexComputerRunMCPServer.exe",
      "args": []
    }
  }
}
```

Linux or macOS:

```json
{
  "mcpServers": {
    "codex-computer-run": {
      "command": "/path/to/CodexComputerRunMCPServer/artifacts/publish/linux-x64/CodexComputerRunMCPServer",
      "args": []
    }
  }
}
```

NuGet package through `dnx`:

```json
{
  "mcpServers": {
    "codex-computer-run": {
      "command": "dnx",
      "args": [
        "CP.CodexComputerRun.Mcp.Server@1.*",
        "--yes"
      ]
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
        "PathTo\\CodexComputerRunMCPServer\\src\\CodexComputerRunMCPServer\\CodexComputerRunMCPServer.csproj",
        "--configuration",
        "Release",
        "--no-launch-profile"
      ]
    }
  }
}
```

Use forward slashes in the project path on Linux and macOS.

### Are `mcp-config.development.windows.json` And `mcp-config.windows.json` Required?

No. They are optional convenience snippets for MCP clients that import JSON config files manually.

Required or primary MCP/Codex files are:
- `.mcp/server.json` for MCP package metadata.
- `.mcp/install.md` for install notes.
- `skills/codex-computer-run` for the bundled Codex Skill.
- `.codex/config.toml` for this local Codex workspace.
- `.mcp.json` only if your client reads repository-local MCP JSON configuration.

## Build

Windows PowerShell:

```powershell
dotnet restore .\CodexComputerRunMCPServer.slnx
dotnet build .\CodexComputerRunMCPServer.slnx --configuration Release
```

Linux or macOS:

```bash
dotnet restore ./CodexComputerRunMCPServer.slnx
dotnet build ./CodexComputerRunMCPServer.slnx --configuration Release
```

If a running MCP server locks the default `bin\Release` output, build to a verification output path:

```powershell
dotnet build .\CodexComputerRunMCPServer.slnx --configuration Release --no-restore /p:OutputPath=D:\Projects\Github\chrispulman\CodexComputerRunMCPServer\artifacts\verify\bin\
```

## Test

Windows PowerShell:

```powershell
dotnet test --project .\src\CodexComputerRunMCPServer.Tests\CodexComputerRunMCPServer.Tests.csproj --configuration Release
```

Linux or macOS:

```bash
dotnet test --project ./src/CodexComputerRunMCPServer.Tests/CodexComputerRunMCPServer.Tests.csproj --configuration Release
```

Coverage with TUnit/Microsoft Testing Platform:

```powershell
dotnet test --project .\src\CodexComputerRunMCPServer.Tests\CodexComputerRunMCPServer.Tests.csproj --configuration Release -- --coverage --coverage-output coverage.cobertura.xml --coverage-output-format cobertura --results-directory .\artifacts\test-results
```

Current verification:
- 61 TUnit tests passed.
- Coverage: 77.65% line coverage, 48.37% branch coverage for testable code.
- Repository and package verification confirm `skills/codex-computer-run/SKILL.md` and `skills/codex-computer-run/agents/openai.yaml` are bundled.
- Native Win32 P/Invoke shims are excluded from coverage and verified through the service boundary plus live MCP tool discovery.

## Publish

The helper script name is historical; it now accepts Windows, Linux, and macOS runtime identifiers.

```powershell
.\scripts\publish-windows.ps1 -Runtime win-x64
.\scripts\publish-windows.ps1 -Runtime linux-x64
.\scripts\publish-windows.ps1 -Runtime osx-arm64
```

Direct command:

```powershell
dotnet publish .\src\CodexComputerRunMCPServer\CodexComputerRunMCPServer.csproj --configuration Release --runtime win-x64 --self-contained false --output .\artifacts\publish\win-x64
```

## MCP Verification

The TUnit suite verifies MCP metadata, the bundled Codex Skill, platform adapters, lifecycle behavior, and the static tool facade. The published `win-x64` executable was also validated with an MCP stdio `initialize` and `tools/list` handshake. The server reports all 15 tools:

```text
activate_window, close_window, scroll, hotkey, type_text, screenshot, list_windows, find_windows, screenshot_window, verify_window, wait_for_window, click, move_mouse, press_key, cursor_position
```

Live Linux and macOS desktop behavior depends on the active graphical session, installed command dependencies, and OS-level permissions.

## Example Prompts For Your AI Assistant

Once configured, you can ask things like:

- "Call `screenshot` and describe the active window."
- "Call `find_windows` for `msedge` with a title containing `Discord`, activate the returned handle, then call `screenshot_window`."
- "Wait for the identified Notepad window, verify its handle and foreground state, then enter the requested text."
- "List visible windows and tell me which browser tabs or apps are available."
- "Move the mouse to `x=400`, `y=300`, click, then take another screenshot."
- "Press `ctrl+l`, type `https://example.com`, then press `enter`."
- "Enter this text into the focused editor using `type_text`."
- "Scroll down 5 notches and confirm what changed on screen."
- "Get the cursor position before clicking."

## Safety Notes

This server controls the active desktop. Mouse, keyboard, and clipboard actions affect the currently focused application. Use it only in a trusted desktop session and pair destructive UI actions with screenshots or window checks first.

### Interaction policy

The bundled Codex Skill distinguishes reversible interface maintenance from data-bearing or destructive actions. When a user explicitly asks to debug a named application, developer mode can continue through low-risk operations such as switching tabs, opening DevTools, reloading an identified page, dismissing a modal, or closing a confirmed empty tab after the target window has been identified. It should not require a separate confirmation for every click in a known sequence.

Developer mode does not remove target-window checks or authorize sending messages, submitting forms, joining calls, deleting data, making purchases, or changing account and security settings. It also must not assume that a tab is empty when it contains unsent text, an upload, an active call, recording, streaming, media playback, or another pending operation.
