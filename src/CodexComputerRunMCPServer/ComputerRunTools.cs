using System.ComponentModel;
using System.Drawing;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Exposes MCP tool endpoints for desktop automation actions, including screen capture,
/// mouse input, keyboard input, and window/query utilities.
/// </summary>
/// <remarks>
/// Methods in this type are discovered through MCP attributes and delegate execution to
/// <see cref="ComputerRunToolRuntime.Service"/>.
/// </remarks>
[McpServerToolType]
public static class ComputerRunTools
{
    /// <summary>
    /// Captures the current virtual desktop as a PNG image.
    /// </summary>
    /// <param name="path">
    /// Optional destination path for the PNG file. If omitted, no file is created.
    /// </param>
    /// <param name="include_image">
    /// <see langword="true"/> to include PNG image data in the MCP tool result; otherwise <see langword="false"/>.
    /// </param>
    /// <returns>
    /// A <see cref="CallToolResult"/> containing the screenshot result payload.
    /// </returns>
    [McpServerTool]
    [Description("Capture the current desktop or a requested screen region as a PNG. Pass path to save it on disk; omit path for an in-memory MCP image result.")]
    public static CallToolResult screenshot(
        [Description("Optional output PNG path. If omitted, no temporary file is created.")] string? path = null,
        [Description("Include PNG image data in the MCP tool result.")] bool include_image = true,
        [Description("Optional left edge of a screen-space capture region. Provide all four region values together.")] int? left = null,
        [Description("Optional top edge of a screen-space capture region. Provide all four region values together.")] int? top = null,
        [Description("Optional width of a screen-space capture region. Must be greater than zero.")] int? width = null,
        [Description("Optional height of a screen-space capture region. Must be greater than zero.")] int? height = null)
        => Invoke(
            "screenshot",
            new { hasPath = !string.IsNullOrWhiteSpace(path), includeImage = include_image, hasRegion = left.HasValue },
            service => service.Screenshot(path, include_image, CreateScreenshotRegion(left, top, width, height)));

    /// <summary>
    /// Moves the mouse cursor to absolute desktop coordinates.
    /// </summary>
    /// <param name="x">Absolute X coordinate.</param>
    /// <param name="y">Absolute Y coordinate.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Move the mouse cursor to absolute desktop coordinates.")]
    public static string move_mouse(
        [Description("Absolute X coordinate.")] int x,
        [Description("Absolute Y coordinate.")] int y,
        [Description("Optional delay after the action, in seconds.")] double? delay = null,
        [Description("Optional native window handle previously returned by find_windows. When supplied, movement is aborted if that window is stale or minimized.")] long? target_handle = null)
        => InvokeControl("move_mouse", new { x, y, delay, target_handle }, service => service.MoveMouse(x, y, delay, target_handle));

    /// <summary>
    /// Performs a mouse click at the current cursor position or at provided coordinates.
    /// </summary>
    /// <param name="x">Optional absolute X coordinate.</param>
    /// <param name="y">Optional absolute Y coordinate.</param>
    /// <param name="button">Mouse button to click: <c>left</c>, <c>right</c>, or <c>middle</c>.</param>
    /// <param name="clicks">Number of click repetitions.</param>
    /// <param name="interval">Delay between repeated clicks, in seconds.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Click at the current cursor position or at absolute desktop coordinates.")]
    public static string click(
        [Description("Optional absolute X coordinate.")] int? x = null,
        [Description("Optional absolute Y coordinate.")] int? y = null,
        [Description("Mouse button: left, right, or middle.")] string button = "left",
        [Description("Number of clicks.")] int clicks = 1,
        [Description("Delay between repeated clicks, in seconds.")] double interval = 0.08,
        [Description("Optional delay after the action, in seconds.")] double? delay = null,
        [Description("Optional native window handle previously returned by find_windows. When supplied, the click is aborted unless that exact window is still foreground.")] long? target_handle = null)
        => InvokeControl("click", new { x, y, button, clicks, interval, delay, target_handle }, service => service.Click(x, y, button, clicks, interval, delay, target_handle));

    /// <summary>
    /// Scrolls the mouse wheel, optionally after moving to specified coordinates.
    /// </summary>
    /// <param name="amount">Wheel notches; positive scrolls up and negative scrolls down.</param>
    /// <param name="x">Optional absolute X coordinate to move to before scrolling.</param>
    /// <param name="y">Optional absolute Y coordinate to move to before scrolling.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Scroll the mouse wheel. Positive amount scrolls up; negative amount scrolls down.")]
    public static string scroll(
        [Description("Wheel notches. Positive scrolls up; negative scrolls down.")] int amount = -3,
        [Description("Optional absolute X coordinate to move to before scrolling.")] int? x = null,
        [Description("Optional absolute Y coordinate to move to before scrolling.")] int? y = null,
        [Description("Optional delay after the action, in seconds.")] double? delay = null,
        [Description("Optional native window handle previously returned by find_windows. When supplied, scrolling is aborted unless that exact window is still foreground.")] long? target_handle = null)
        => InvokeControl("scroll", new { amount, x, y, delay, target_handle }, service => service.Scroll(amount, x, y, delay, target_handle));

    /// <summary>
    /// Presses and releases a single keyboard key.
    /// </summary>
    /// <param name="key">Key name or single character to press.</param>
    /// <param name="duration">Time to hold the key, in seconds.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Press a single keyboard key, for example enter, tab, escape, f5, a, A, ?, or 1.")]
    public static string press_key(
        [Description("Key name or single character.")] string key,
        [Description("How long to hold the key, in seconds.")] double duration = 0.03,
        [Description("Optional delay after the action, in seconds.")] double? delay = null,
        [Description("Optional native window handle previously returned by find_windows. When supplied, input is aborted unless that exact window is still foreground.")] long? target_handle = null)
        => InvokeControl("press_key", new { keyLength = key?.Length ?? 0, duration, delay, target_handle }, service => service.PressKey(key ?? string.Empty, duration, delay, target_handle));

    /// <summary>
    /// Presses a keyboard shortcut chord such as <c>ctrl+l</c> or <c>ctrl+shift+escape</c>.
    /// </summary>
    /// <param name="keys">Shortcut text using <c>+</c>, comma, or space separators.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Press a keyboard shortcut, for example ctrl+l or ctrl+shift+escape.")]
    public static string hotkey(
        [Description("Shortcut text. Use +, comma, or space separators, e.g. ctrl+shift+escape.")] string keys,
        [Description("Optional delay after the action, in seconds.")] double? delay = null,
        [Description("Optional native window handle previously returned by find_windows. When supplied, the shortcut is aborted unless that exact window is still foreground.")] long? target_handle = null)
        => InvokeControl("hotkey", new { keysLength = keys?.Length ?? 0, delay, target_handle }, service => service.Hotkey(keys ?? string.Empty, delay, target_handle));

    /// <summary>
    /// Enters Unicode text into the currently focused application using the platform's preferred text-entry path.
    /// </summary>
    /// <param name="text">Text content to enter.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Enter Unicode text into the focused application. Windows uses direct Unicode input without changing the clipboard.")]
    public static string type_text(
        [Description("Text to enter into the focused application.")] string text,
        [Description("Optional delay after the action, in seconds.")] double? delay = null,
        [Description("Optional native window handle previously returned by find_windows. When supplied, text entry is aborted unless that exact window is still foreground.")] long? target_handle = null)
        => InvokeControl("type_text", new { textLength = text?.Length ?? 0, delay, target_handle }, service => service.TypeText(text ?? string.Empty, delay, target_handle));

    /// <summary>
    /// Gets the current cursor position.
    /// </summary>
    /// <returns>A JSON payload describing the current cursor coordinates.</returns>
    [McpServerTool]
    [Description("Return the current desktop cursor position as JSON.")]
    public static string cursor_position()
        => Invoke("cursor_position", arguments: null, service => service.CursorPosition());

    /// <summary>
    /// Lists visible top-level desktop windows.
    /// </summary>
    /// <param name="limit">Maximum number of windows to return.</param>
    /// <returns>A JSON array payload with visible window metadata.</returns>
    [McpServerTool]
    [Description("List visible top-level desktop windows as JSON, including process identity, foreground/minimized state, and screen-space bounds when available.")]
    public static string list_windows([Description("Maximum number of windows to return.")] int limit = 50)
        => Invoke("list_windows", new { limit }, service => service.ListWindows(limit));

    /// <summary>
    /// Finds visible windows using stable metadata instead of screen coordinates.
    /// </summary>
    [McpServerTool]
    [Description("Find visible top-level windows by optional process name, title substring, foreground state, or minimized state.")]
    public static string find_windows(
        [Description("Optional process name, matched case-insensitively, for example Notepad or msedge.")] string? process_name = null,
        [Description("Optional case-insensitive substring of the window title.")] string? title_contains = null,
        [Description("Return only windows reported as the current foreground window.")] bool foreground_only = false,
        [Description("Include minimized windows in the results.")] bool include_minimized = true,
        [Description("Maximum number of matching windows to return.")] int limit = 50)
        => Invoke(
            "find_windows",
            new { hasProcessFilter = !string.IsNullOrWhiteSpace(process_name), hasTitleFilter = !string.IsNullOrWhiteSpace(title_contains), foreground_only, include_minimized, limit },
            service => service.FindWindows(process_name, title_contains, foreground_only, include_minimized, limit));

    /// <summary>
    /// Captures a window's screen-space bounds by native handle.
    /// </summary>
    [McpServerTool]
    [Description("Capture a visible top-level window by native handle returned by list_windows or find_windows. The capture is screen-space and does not reveal occluded content.")]
    public static CallToolResult screenshot_window(
        [Description("Native window handle returned by list_windows or find_windows.")] long handle,
        [Description("Optional output PNG path. If omitted, no file is created.")] string? path = null,
        [Description("Include PNG image data in the MCP tool result.")] bool include_image = true)
        => Invoke("screenshot_window", new { handle, hasPath = !string.IsNullOrWhiteSpace(path), includeImage = include_image }, service => service.ScreenshotWindow(handle, path, include_image));

    /// <summary>
    /// Verifies that a previously selected window still matches its expected identity and state.
    /// </summary>
    [McpServerTool]
    [Description("Verify a window handle before or after an action. Returns JSON with ok, reason, and current window metadata without changing desktop state.")]
    public static string verify_window(
        [Description("Native window handle returned by list_windows or find_windows.")] long handle,
        [Description("Optional expected process name, matched case-insensitively.")] string? process_name = null,
        [Description("Optional expected case-insensitive title substring.")] string? title_contains = null,
        [Description("Require the window to be the foreground window.")] bool require_foreground = false,
        [Description("Allow the window to be minimized.")] bool allow_minimized = true)
        => Invoke(
            "verify_window",
            new { handle, hasProcessFilter = !string.IsNullOrWhiteSpace(process_name), hasTitleFilter = !string.IsNullOrWhiteSpace(title_contains), require_foreground, allow_minimized },
            service => service.VerifyWindow(handle, process_name, title_contains, require_foreground, allow_minimized));

    /// <summary>
    /// Waits for a matching window using a bounded observation-only retry loop.
    /// </summary>
    [McpServerTool]
    [Description("Wait for a visible window matching optional process/title/state filters. The wait is bounded to 30 seconds and never sends input.")]
    public static string wait_for_window(
        [Description("Optional process name, matched case-insensitively.")] string? process_name = null,
        [Description("Optional case-insensitive title substring.")] string? title_contains = null,
        [Description("Return only windows reported as foreground.")] bool foreground_only = false,
        [Description("Include minimized windows in the result.")] bool include_minimized = true,
        [Description("Maximum wait in milliseconds, from 0 to 30000.")] int timeout_ms = 5000,
        [Description("Polling interval in milliseconds, from 25 to 1000.")] int poll_ms = 100)
        => Invoke(
            "wait_for_window",
            new { hasProcessFilter = !string.IsNullOrWhiteSpace(process_name), hasTitleFilter = !string.IsNullOrWhiteSpace(title_contains), foreground_only, include_minimized, timeout_ms, poll_ms },
            service => service.WaitForWindow(process_name, title_contains, foreground_only, include_minimized, timeout_ms, poll_ms));

    /// <summary>
    /// Brings a window returned by <see cref="list_windows"/> to the foreground.
    /// </summary>
    /// <param name="handle">Native window handle returned by list_windows.</param>
    /// <param name="restore">Restore the window first when it is minimized.</param>
    /// <returns>A human-readable activation result.</returns>
    [McpServerTool]
    [Description("Activate a previously enumerated window by native handle. Use a handle from list_windows; optionally restore it when minimized.")]
    public static string activate_window(
        [Description("Native window handle returned by list_windows.")] long handle,
        [Description("Restore the window before focusing it when minimized.")] bool restore = true)
        => InvokeControl("activate_window", new { handle, restore }, service => service.ActivateWindow(handle, restore));

    /// <summary>
    /// Requests a graceful close of one exact top-level window.
    /// </summary>
    [McpServerTool]
    [Description("Request a graceful close for one exact window handle. It never terminates the process; if the app shows a save prompt or rejects the request, the result reports closed=false.")]
    public static string close_window(
        [Description("Native window handle returned by list_windows or find_windows.")] long handle,
        [Description("Maximum time to wait for the window to disappear, from 0 to 5000 milliseconds.")] int timeout_ms = 1000)
        => InvokeControl("close_window", new { handle, timeout_ms }, service => service.CloseWindow(handle, timeout_ms));

    /// <summary>
    /// Lists files and directories with bounded enumeration.
    /// </summary>
    [McpServerTool]
    [Description("List a directory as bounded JSON metadata. It never changes the filesystem, does not follow directory junctions/symlinks during recursive scans, and reports inaccessible children as errors.")]
    public static string list_directory(
        [Description("Directory path. Environment variables are expanded and the result is normalized to an absolute path.")] string path,
        [Description("Recurse into child directories, excluding reparse points.")] bool recursive = false,
        [Description("Maximum entries to return, from 1 to 5000.")] int max_entries = 500)
        => InvokeFileObservation("list_directory", new { hasPath = !string.IsNullOrWhiteSpace(path), recursive, max_entries }, () => FileSystemService.ListDirectory(path, recursive, max_entries));

    /// <summary>
    /// Creates a directory, or returns a dry-run plan by default.
    /// </summary>
    [McpServerTool]
    [Description("Create a directory. dry_run defaults to true and returns the exact normalized path without changing anything.")]
    public static string create_directory(
        [Description("Directory path to create.")] string path,
        [Description("When true, only return the plan; when false, create the directory.")] bool dry_run = true)
        => InvokeFileMutation("create_directory", new { hasPath = !string.IsNullOrWhiteSpace(path), dry_run }, dry_run, () => FileSystemService.CreateDirectory(path, dry_run));

    /// <summary>
    /// Copies a file or directory, or returns a dry-run plan by default.
    /// </summary>
    [McpServerTool]
    [Description("Copy one file or directory to an exact destination. dry_run defaults to true; recursive directory copies skip reparse points.")]
    public static string copy_path(
        [Description("Existing source file or directory.")] string source,
        [Description("Exact destination path, not an implicit parent directory.")] string destination,
        [Description("Allow an existing destination file to be replaced. Existing destination directories are never merged.")] bool overwrite = false,
        [Description("When true, only return the plan; when false, perform the copy.")] bool dry_run = true)
        => InvokeFileMutation("copy_path", new { hasSource = !string.IsNullOrWhiteSpace(source), hasDestination = !string.IsNullOrWhiteSpace(destination), overwrite, dry_run }, dry_run, () => FileSystemService.CopyPath(source, destination, overwrite, dry_run));

    /// <summary>
    /// Moves or renames a file or directory, or returns a dry-run plan by default.
    /// </summary>
    [McpServerTool]
    [Description("Move or rename one file or directory to an exact destination. dry_run defaults to true and a directory cannot be moved into itself.")]
    public static string move_path(
        [Description("Existing source file or directory.")] string source,
        [Description("Exact destination path, not an implicit parent directory.")] string destination,
        [Description("Allow an existing destination file to be replaced. Existing destination directories are never merged.")] bool overwrite = false,
        [Description("When true, only return the plan; when false, perform the move.")] bool dry_run = true)
        => InvokeFileMutation("move_path", new { hasSource = !string.IsNullOrWhiteSpace(source), hasDestination = !string.IsNullOrWhiteSpace(destination), overwrite, dry_run }, dry_run, () => FileSystemService.MovePath(source, destination, overwrite, dry_run));

    /// <summary>
    /// Deletes a file or directory, or returns a dry-run plan by default.
    /// </summary>
    [McpServerTool]
    [Description("Delete one exact file or directory. dry_run defaults to true; actual deletion is permanent, and recursive must be explicitly true for non-empty directories.")]
    public static string delete_path(
        [Description("Existing file or directory to delete.")] string path,
        [Description("Allow deletion of directory contents when dry_run is false.")] bool recursive = false,
        [Description("When true, only return the plan; when false, permanently delete the exact path.")] bool dry_run = true)
        => InvokeFileMutation("delete_path", new { hasPath = !string.IsNullOrWhiteSpace(path), recursive, dry_run }, dry_run, () => FileSystemService.DeletePath(path, recursive, dry_run));

    /// <summary>
    /// Reads local Git status without changing the repository.
    /// </summary>
    [McpServerTool]
    [Description("Return local Git branch and change status for an existing repository. This is observation-only and never stages, commits, or pushes.")]
    public static string git_status(
        [Description("Existing local repository directory.")] string repo_path)
        => InvokeGitObservation("git_status", new { hasRepositoryPath = !string.IsNullOrWhiteSpace(repo_path) }, () => GitService.CreateDefault().Status(repo_path));

    /// <summary>
    /// Initializes a local Git repository, or returns a dry-run plan by default.
    /// </summary>
    [McpServerTool]
    [Description("Create a local Git repository. dry_run defaults to true; it never contacts a remote and does not create files until false is explicitly supplied.")]
    public static string git_init(
        [Description("Repository directory to initialize.")] string repo_path,
        [Description("Create a bare repository instead of a working-tree repository.")] bool bare = false,
        [Description("When true, only return the plan; when false, run git init.")] bool dry_run = true)
        => InvokeGitMutation("git_init", new { hasRepositoryPath = !string.IsNullOrWhiteSpace(repo_path), bare, dry_run }, dry_run, () => GitService.CreateDefault().Init(repo_path, bare, dry_run));

    /// <summary>
    /// Clones a local or remote Git repository, or returns a dry-run plan by default.
    /// </summary>
    [McpServerTool]
    [Description("Clone a Git URL or local source to an exact destination. dry_run defaults to true; no network or filesystem clone occurs until false is explicitly supplied.")]
    public static string git_clone(
        [Description("Git URL or local source path.")] string url,
        [Description("New destination directory, which must not already exist.")] string destination,
        [Description("When true, only return the plan; when false, run git clone.")] bool dry_run = true)
        => InvokeGitMutation("git_clone", new { hasUrl = !string.IsNullOrWhiteSpace(url), hasDestination = !string.IsNullOrWhiteSpace(destination), dry_run }, dry_run, () => GitService.CreateDefault().Clone(url, destination, dry_run));

    /// <summary>
    /// Creates a local Git branch, or returns a dry-run plan by default.
    /// </summary>
    [McpServerTool]
    [Description("Create a local Git branch after validating its ref name. dry_run defaults to true; checkout is optional and no remote is touched.")]
    public static string git_create_branch(
        [Description("Existing local repository directory.")] string repo_path,
        [Description("Branch name to validate and create.")] string branch,
        [Description("Switch to the new branch after creating it when dry_run is false.")] bool checkout = false,
        [Description("When true, only return the plan; when false, create the branch.")] bool dry_run = true)
        => InvokeGitMutation("git_create_branch", new { hasRepositoryPath = !string.IsNullOrWhiteSpace(repo_path), branchLength = branch?.Length ?? 0, checkout, dry_run }, dry_run, () => GitService.CreateDefault().CreateBranch(repo_path, branch ?? string.Empty, checkout, dry_run));

    /// <summary>
    /// Commits local Git changes, or returns a dry-run plan by default.
    /// </summary>
    [McpServerTool]
    [Description("Create a local Git commit. dry_run defaults to true; stage_all defaults to false so unstaged user files are not silently added. This tool never pushes.")]
    public static string git_commit(
        [Description("Existing local repository directory.")] string repo_path,
        [Description("Non-empty commit message.")] string message,
        [Description("Stage all tracked and untracked changes before committing when dry_run is false.")] bool stage_all = false,
        [Description("When true, only return the current status plan; when false, stage optionally and commit locally.")] bool dry_run = true)
        => InvokeGitMutation("git_commit", new { hasRepositoryPath = !string.IsNullOrWhiteSpace(repo_path), messageLength = message?.Length ?? 0, stage_all, dry_run }, dry_run, () => GitService.CreateDefault().Commit(repo_path, message ?? string.Empty, stage_all, dry_run));

    /// <summary>
    /// Lists processes by optional name without changing process state.
    /// </summary>
    [McpServerTool]
    [Description("List running processes with bounded metadata. An optional process name may include or omit .exe; the query never launches or terminates anything.")]
    public static string list_processes(
        [Description("Optional process name, for example msedge or notepad.exe.")] string? process_name = null,
        [Description("Maximum number of processes to return, from 1 to 1000.")] int limit = 100)
        => InvokeProcessObservation("list_processes", new { hasProcessFilter = !string.IsNullOrWhiteSpace(process_name), limit }, () => ProcessService.ListProcesses(process_name, limit));

    /// <summary>
    /// Waits for a process by name or PID using a bounded observation-only loop.
    /// </summary>
    [McpServerTool]
    [Description("Wait for a running process by name or PID. The wait is bounded to 30 seconds and never sends input or changes process state.")]
    public static string wait_for_process(
        [Description("Optional process name, with or without .exe.")] string? process_name = null,
        [Description("Optional process ID. Provide process_name or process_id.")] int? process_id = null,
        [Description("Maximum wait in milliseconds, from 0 to 30000.")] int timeout_ms = 5000,
        [Description("Polling interval in milliseconds, from 25 to 1000.")] int poll_ms = 100)
        => InvokeProcessObservation("wait_for_process", new { hasProcessFilter = !string.IsNullOrWhiteSpace(process_name), process_id, timeout_ms, poll_ms }, () => ProcessService.WaitForProcess(process_name, process_id, timeout_ms, poll_ms));

    /// <summary>
    /// Starts an executable with an argument list, or returns a dry-run plan by default.
    /// </summary>
    [McpServerTool]
    [Description("Launch an executable with arguments passed without a shell. dry_run defaults to true; when false it starts the application and returns a best-effort PID. For GUI apps that reuse a process, use wait_for_window for the final window identity.")]
    public static string launch_application(
        [Description("Executable name or absolute path.")] string executable,
        [Description("Arguments passed as separate values, never shell-parsed.")] IReadOnlyList<string>? arguments = null,
        [Description("Optional existing working directory.")] string? working_directory = null,
        [Description("When true, only return the plan; when false, start the application.")] bool dry_run = true)
        => InvokeProcessMutation("launch_application", new { executableLength = executable?.Length ?? 0, argumentCount = arguments?.Count ?? 0, hasWorkingDirectory = !string.IsNullOrWhiteSpace(working_directory), dry_run }, dry_run, () => ProcessService.Launch(executable ?? string.Empty, arguments ?? [], working_directory, dry_run));

    /// <summary>
    /// Opens an HTTP(S) URL through the operating system's default browser, or returns a dry-run plan.
    /// </summary>
    [McpServerTool]
    [Description("Open an absolute http or https URL using the operating system's default browser. dry_run defaults to true and never opens a tab until false is explicitly supplied; any returned PID is best-effort because browsers may reuse an existing process.")]
    public static string open_url(
        [Description("Absolute http or https URL.")] string url,
        [Description("When true, only return the normalized URL plan; when false, open it in the default browser.")] bool dry_run = true)
        => InvokeProcessMutation("open_url", new { urlLength = url?.Length ?? 0, dry_run }, dry_run, () => ProcessService.OpenUrl(url ?? string.Empty, dry_run));

    private static TResult Invoke<TResult>(string tool, object? arguments, Func<IComputerRunService, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var invocation = ComputerRunToolRuntime.BeginToolInvocation();
        return ComputerRunAuditLogger.Execute(tool, arguments, () => action(ComputerRunToolRuntime.Service));
    }

    private static TResult InvokeControl<TResult>(string tool, object? arguments, Func<IComputerRunService, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var invocation = ComputerRunToolRuntime.BeginToolInvocation();
        using var control = ComputerRunToolRuntime.BeginDesktopControlInvocation();
        return ComputerRunAuditLogger.Execute(tool, arguments, () => action(ComputerRunToolRuntime.Service));
    }

    private static TResult InvokeFileObservation<TResult>(string tool, object? arguments, Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var invocation = ComputerRunToolRuntime.BeginToolInvocation();
        return ComputerRunAuditLogger.Execute(tool, arguments, action);
    }

    private static TResult InvokeFileMutation<TResult>(string tool, object? arguments, bool dryRun, Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var invocation = ComputerRunToolRuntime.BeginToolInvocation();
        using var control = dryRun ? null : ComputerRunToolRuntime.BeginDesktopControlInvocation();
        return ComputerRunAuditLogger.Execute(tool, arguments, action);
    }

    private static TResult InvokeGitObservation<TResult>(string tool, object? arguments, Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var invocation = ComputerRunToolRuntime.BeginToolInvocation();
        return ComputerRunAuditLogger.Execute(tool, arguments, action);
    }

    private static TResult InvokeGitMutation<TResult>(string tool, object? arguments, bool dryRun, Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var invocation = ComputerRunToolRuntime.BeginToolInvocation();
        using var control = dryRun ? null : ComputerRunToolRuntime.BeginDesktopControlInvocation();
        return ComputerRunAuditLogger.Execute(tool, arguments, action);
    }

    private static TResult InvokeProcessObservation<TResult>(string tool, object? arguments, Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var invocation = ComputerRunToolRuntime.BeginToolInvocation();
        return ComputerRunAuditLogger.Execute(tool, arguments, action);
    }

    private static TResult InvokeProcessMutation<TResult>(string tool, object? arguments, bool dryRun, Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var invocation = ComputerRunToolRuntime.BeginToolInvocation();
        using var control = dryRun ? null : ComputerRunToolRuntime.BeginDesktopControlInvocation();
        return ComputerRunAuditLogger.Execute(tool, arguments, action);
    }

    private static Rectangle? CreateScreenshotRegion(int? left, int? top, int? width, int? height)
    {
        var values = new[] { left, top, width, height };
        if (values.Any(value => value.HasValue) && values.Any(value => !value.HasValue))
        {
            throw new ArgumentException("left, top, width, and height must be supplied together.");
        }

        return left.HasValue
            ? new Rectangle(left.Value, top!.Value, width!.Value, height!.Value)
            : null;
    }
}
