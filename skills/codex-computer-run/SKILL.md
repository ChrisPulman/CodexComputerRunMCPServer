---
name: codex-computer-run
description: Use this skill when Codex needs to operate or inspect a signed-in Windows desktop through the Codex Computer Run MCP server, including screenshots, visible window discovery, cursor position checks, mouse movement, clicking, scrolling, keyboard shortcuts, single-key presses, or Unicode text paste into focused applications.
---

# Codex Computer Run

## Overview

Use the Codex Computer Run MCP server as an observation-first control layer for a real Windows desktop session. Its tools affect the active machine, so inspect context before acting and verify after actions that can change state.

## Tool Discovery

- Prefer the `mcp__codex_computer_run__` namespace when available.
- If tools are deferred, search for `ComputerRun`, `codex computer run`, or `desktop screenshot mouse keyboard` and choose the namespace that exposes the complete tool set.
- Expect these tools: `screenshot`, `list_windows`, `cursor_position`, `move_mouse`, `click`, `scroll`, `press_key`, `hotkey`, and `type_text`.
- If the MCP tools are unavailable, state that the Computer Run server is not configured in the current session instead of simulating desktop interaction with unrelated shell commands.

## Operating Protocol

1. Observe before acting:
   - Use `list_windows` to identify visible applications and likely targets.
   - Use `screenshot` when visual layout, coordinates, or UI state matters.
   - Use `cursor_position` before relying on the current pointer location.
2. Plan in absolute Windows virtual-screen coordinates:
   - Treat coordinates as desktop coordinates, not browser or app-relative coordinates.
   - Read screenshot metadata for `left`, `top`, `width`, and `height`; multi-monitor layouts can have negative `left` or `top` values.
   - Move or click only when the target application and coordinates are known.
3. Act with the narrowest tool:
   - Use `move_mouse` for hover or to position before a click.
   - Use `click` for buttons, menus, tabs, selections, and context menus.
   - Use `scroll` for pages, lists, combo boxes, and scrollable panes.
   - Use `press_key` for one key such as `enter`, `tab`, `escape`, `f5`, arrows, or a single character.
   - Use `hotkey` for shortcuts such as `ctrl+l`, `ctrl+shift+p`, `alt+tab`, or `ctrl+shift+escape`.
   - Use `type_text` for text entry; it pastes Unicode through the clipboard and is faster and more reliable than repeated key presses.
4. Verify after meaningful actions:
   - Use `screenshot` after navigation, clicks, scrolls, or text entry when the resulting state matters.
   - Use `list_windows` again after task switching or launching apps.

## Screenshots

- Use `screenshot` with `include_image: true` when the model needs to inspect the pixels.
- Use `include_image: false` with a `path` when only dimensions, a saved artifact, or later local inspection is needed.
- Save screenshots to an explicit temporary or workspace path when they may be referenced in the final answer.

## Safety Rules

- Remember that mouse, keyboard, and clipboard actions affect the user's signed-in desktop.
- Do not perform destructive UI actions, submit forms, send messages, make purchases, delete files, or change account/security settings unless the user explicitly asked for that exact outcome.
- Confirm the intended foreground app with `list_windows` or `screenshot` before typing or pressing shortcuts that could affect the wrong application.
- Treat `type_text` as clipboard-changing; use it only when pasting into the focused target is intended.
- Keep delays short but use the optional `delay` parameter after actions that trigger UI transitions.

## Quick Checks

For a non-destructive connectivity test:

1. Call `list_windows` with a small limit and confirm visible window metadata is returned.
2. Call `cursor_position` and confirm JSON coordinates are returned.
3. Call `screenshot` with a saved `path` and `include_image: false`; confirm screenshot metadata includes the saved path and virtual desktop bounds.
4. Optionally move the cursor by a small, reversible amount with `move_mouse`, then call `cursor_position` again to confirm the new coordinates.
