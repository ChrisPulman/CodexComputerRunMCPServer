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
        "CP.CodexComputerRun.Mcp.Server@0.*",
        "--yes"
      ]
    }
  }
}
```

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
