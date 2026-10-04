# Codex Computer Run MCP Server Install Notes

Executable command name after pack/install:
- `codex-computer-run-mcp-server`

Fast local Codex config after publishing:

```toml
[mcp_servers.codex-computer-run]
command = "D:\\Projects\\Github\\chrispulman\\CodexComputerRunMCPServer\\artifacts\\publish\\win-x64\\CodexComputerRunMCPServer.exe"
args = []
```

For Linux or macOS publish output, use the matching runtime folder such as `artifacts/publish/linux-x64/CodexComputerRunMCPServer` or `artifacts/publish/osx-arm64/CodexComputerRunMCPServer`.

Suggested stdio config after NuGet publication:

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

Bundled Codex Skill install:

```powershell
codex-computer-run-mcp-server --install-codex-skill
```

The server installs or refreshes the bundled `codex-computer-run` skill on each startup when the configured `CODEX_HOME` directory or default `%USERPROFILE%\.codex` directory exists. Updating the server and starting it refreshes changed bundled files automatically. Keep customizations in a separate skill. Explicit installation preserves existing files unless you pass `--force`; it can also create the Codex home directory. Diagnostics use standard error, and automatic installation failures do not prevent MCP startup.

Alternative source-run config for development:

```json
{
  "mcpServers": {
    "codex-computer-run": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/CodexComputerRunMCPServer/src/CodexComputerRunMCPServer/CodexComputerRunMCPServer.csproj",
        "--configuration",
        "Release",
        "--no-launch-profile"
      ]
    }
  }
}
```
