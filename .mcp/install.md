# Codex Computer Run MCP Server Install Notes

Executable command name after pack/install:
- `codex-computer-run-mcp-server`

Fast local Codex config after publishing:

```toml
[mcp_servers.codex-computer-run]
command = "D:\\Projects\\Github\\chrispulman\\CodexComputerRunMCPServer\\artifacts\\publish\\win-x64\\CodexComputerRunMCPServer.exe"
args = []
```

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

The server also auto-installs the bundled `codex-computer-run` skill on startup when `CODEX_HOME` is set or `%USERPROFILE%\.codex` already exists. Existing skill files are left untouched unless the installer is run with `--force`.

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
