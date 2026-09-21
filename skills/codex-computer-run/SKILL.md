---
name: codex-computer-run
description: Use this skill when Codex needs to operate or inspect a signed-in desktop through the Codex Computer Run MCP server, including screenshots, metadata-based window targeting, bounded window waits, pre/post window verification, safe graceful window closing, cursor position checks, mouse movement, clicking, scrolling, keyboard shortcuts, single-key presses, or Unicode text entry into focused applications.
---

# Codex Computer Run

## Overview

Use the Codex Computer Run MCP server as a context-aware control layer for a real desktop session. Its tools affect the active machine, so identify the intended target before acting and verify after actions that can change meaningful state.

## Interaction Policy

Use the least restrictive policy that still matches the user's request:

- **Normal mode**: identify the target application before interacting with it; verify meaningful state changes, ambiguous transitions, and failures.
- **Developer mode**: when the user has explicitly asked to debug or inspect a named application, treat reversible UI operations as low-risk after the target has been identified once. This includes switching tabs, opening DevTools, reloading a page, focusing a window, closing an explicitly identified empty tab, changing layout, and dismissing a modal.
- Do not ask for an additional confirmation for each low-risk operation merely because it is a click, key press, reload, or tab close. Check the target window and the current UI state once, then continue with the requested sequence.
- Keep explicit confirmation for actions that can transmit, destroy, purchase, authenticate, or alter user data or account/security state. Examples include sending messages, joining calls, submitting forms, deleting files or records, changing account settings, and making purchases.
- Never infer that a tab is empty when the screenshot or window state shows user-entered text, an unsent message, an upload, an active call, recording, streaming, media playback, or another pending operation.

The policy changes how much repetitive confirmation and screenshot checking is needed; it does not authorize acting on an unidentified foreground application or bypass operating-system permissions.

## Tool Discovery

- Prefer the `mcp__codex_computer_run__` namespace when available.
- If tools are deferred, search for `ComputerRun`, `codex computer run`, or `desktop screenshot mouse keyboard` and choose the namespace that exposes the complete tool set.
- Expect these tools: `screenshot`, `list_windows`, `find_windows`, `screenshot_window`, `verify_window`, `wait_for_window`, `activate_window`, `close_window`, `cursor_position`, `move_mouse`, `click`, `scroll`, `press_key`, `hotkey`, and `type_text`.
- If the MCP tools are unavailable, state that the Computer Run server is not configured in the current session instead of simulating desktop interaction with unrelated shell commands.

## Platform Notes

- Windows is the native implementation and uses Win32 desktop APIs.
- Linux and macOS use best-effort command adapters. If a tool reports a missing dependency such as `xdotool`, `gnome-screenshot`, `cliclick`, or `osascript`, explain the dependency instead of retrying unrelated commands.
- The server must run in the signed-in graphical session for the desktop it controls.

## Operating Protocol

1. Observe before acting:
   - Use `list_windows` or `find_windows` to identify visible applications and likely targets.
   - Use `verify_window` before input when the handle may be stale, and `wait_for_window` after launching or recovering a named application.
    - Use `activate_window` with a handle returned by `list_windows` or `find_windows` when the intended target is not already foreground; wait for its success result before continuing.
   - Use `screenshot_window` when a target handle and bounds are known; use full `screenshot` when the wider desktop context matters.
   - Use `cursor_position` before relying on the current pointer location.
2. Plan in absolute desktop coordinates:
   - Treat coordinates as desktop coordinates, not browser or app-relative coordinates.
   - Read screenshot metadata for `platform`, `left`, `top`, `width`, and `height`; multi-monitor layouts can have negative `left` or `top` values.
   - Move or click only when the target application and coordinates are known.
3. Act with the narrowest tool:
   - Use `move_mouse` for hover or to position before a click.
   - Use `click` for buttons, menus, tabs, selections, and context menus.
   - Use `scroll` for pages, lists, combo boxes, and scrollable panes.
   - Use `press_key` for one key such as `enter`, `tab`, `escape`, `f5`, arrows, or a single character.
    - Use `hotkey` for shortcuts such as `ctrl+l`, `ctrl+shift+p`, `alt+tab`, or `ctrl+shift+escape`. When a window handle is known, pass it as `target_handle`.
    - Use `type_text` for text entry and pass `target_handle` whenever focus matters. Windows injects Unicode directly without changing the clipboard; Linux/macOS may use their native clipboard or text-entry fallback.
    - Use `close_window` for an explicitly identified window instead of a global `alt+f4`; it requests a graceful close and reports if a save prompt keeps the window alive.
4. Verify after meaningful actions:
   - In normal mode, use `screenshot` after navigation, clicks, scrolls, or text entry when the resulting state matters.
   - In developer mode, do not capture after every low-risk click when the target and action sequence are already known. Capture after navigation, a meaningful UI transition, a failed action, an unexpected focus change, or before/after a potentially data-bearing action.
   - Use `list_windows` or `verify_window` again after task switching or launching apps, or whenever the foreground target becomes ambiguous.

## Screenshots

- Use `screenshot` with `include_image: true` when the model needs to inspect the pixels.
- Use `include_image: false` with a `path` when only dimensions, a saved artifact, or later local inspection is needed.
- Save screenshots to an explicit temporary or workspace path when they may be referenced in the final answer.

## Safety Rules

- Remember that mouse, keyboard, and clipboard actions affect the user's signed-in desktop.
- Do not perform data-bearing or destructive UI actions, submit forms, send messages, make purchases, delete files, or change account/security settings unless the user explicitly asked for that exact outcome.
- Treat reversible interface maintenance and debugging operations as separate from destructive data operations. Closing a confirmed empty tab, opening DevTools, reloading an identified page, dismissing a modal, or switching tabs is not automatically a destructive action.
- Confirm the intended foreground app with `list_windows` or `screenshot` before typing or pressing shortcuts that could affect the wrong application.
- When a native window handle is available, pass it as `target_handle` to `press_key`, `hotkey`, or `type_text`; the server aborts rather than routing input to a different foreground app.
- Do not use a global `alt+f4` as a substitute for `close_window` when the intended window can be identified by handle.
- On Windows, `type_text` does not change the clipboard. On Linux/macOS, check the platform fallback before using it when preserving clipboard contents matters.
- Keep delays short but use the optional `delay` parameter after actions that trigger UI transitions.
- The server may retry observation-only window enumeration once, but never automatically retries clicks, key presses, hotkeys, scrolling, or text entry.

## Quick Checks

For a non-destructive connectivity test:

1. Call `list_windows` with a small limit and confirm visible window metadata is returned.
2. Call `cursor_position` and confirm JSON coordinates are returned.
3. Call `screenshot` with a saved `path` and `include_image: false`; confirm screenshot metadata includes the saved path and virtual desktop bounds.
4. Optionally move the cursor by a small, reversible amount with `move_mouse`, then call `cursor_position` again to confirm the new coordinates.
