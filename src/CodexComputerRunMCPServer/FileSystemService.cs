using System.Text;
using System.Text.Json;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides bounded, path-based filesystem operations for general desktop workflows.
/// Mutating operations default to a dry run so an ambiguous request produces a plan
/// instead of changing user data.
/// </summary>
internal static class FileSystemService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private const int MaxTextBytes = 5_000_000;

    public static string ReadTextFile(string path, int maxBytes)
    {
        ValidateTextByteLimit(maxBytes);
        var target = ResolveExistingFile(path);
        using var stream = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        var requestedBytes = (int)Math.Min(stream.Length, maxBytes);
        var buffer = new byte[requestedBytes];
        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var read = stream.Read(buffer, bytesRead, buffer.Length - bytesRead);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        var content = DecodeUtf8Prefix(buffer.AsSpan(0, bytesRead));
        return JsonSerializer.Serialize(new
        {
            operation = "read_text_file",
            path = target,
            encoding = "utf-8",
            bytesRead,
            truncated = stream.Length > bytesRead,
            content,
        }, JsonOptions);
    }

    public static string WriteTextFile(string path, string content, bool overwrite, bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(content);
        var target = ResolvePath(path);
        if (Directory.Exists(target))
        {
            throw new IOException($"A directory already exists at the requested file path: {target}");
        }

        var parent = Path.GetDirectoryName(target);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
        {
            throw new DirectoryNotFoundException($"Parent directory not found: {parent ?? target}");
        }

        var existing = File.Exists(target);
        if (existing && !overwrite)
        {
            throw new IOException($"Destination file already exists and overwrite=false: {target}");
        }

        var bytes = Utf8NoBom.GetBytes(content);
        ValidateTextByteLimit(bytes.Length);
        if (!dryRun)
        {
            var temporaryPath = Path.Combine(parent, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    options: FileOptions.SequentialScan | FileOptions.WriteThrough))
                {
                    stream.Write(bytes);
                    stream.Flush(flushToDisk: true);
                }

                if (existing)
                {
                    File.Replace(temporaryPath, target, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temporaryPath, target);
                }
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        return JsonSerializer.Serialize(new
        {
            operation = "write_text_file",
            path = target,
            encoding = "utf-8",
            bytes = bytes.Length,
            overwrite,
            dryRun,
            changed = !dryRun,
        }, JsonOptions);
    }

    public static string ListDirectory(string path, bool recursive, int maxEntries)
    {
        ValidateEntryLimit(maxEntries);
        var root = ResolvePath(path);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Directory not found: {root}");
        }

        var entries = new List<FileSystemEntry>(Math.Min(maxEntries, 512));
        var errors = new List<FileSystemOperationError>();
        var pending = new Queue<string>();
        pending.Enqueue(root);

        while (pending.Count > 0 && entries.Count < maxEntries)
        {
            var current = pending.Dequeue();
            IEnumerable<FileSystemInfo> children;
            try
            {
                children = new DirectoryInfo(current)
                    .EnumerateFileSystemInfos()
                    .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception exception) when (IsExpectedFilesystemAccessException(exception))
            {
                errors.Add(new FileSystemOperationError(current, exception.GetType().Name, exception.Message));
                continue;
            }

            foreach (var child in children)
            {
                if (entries.Count >= maxEntries)
                {
                    break;
                }

                var isDirectory = child.Attributes.HasFlag(FileAttributes.Directory);
                var isReparsePoint = child.Attributes.HasFlag(FileAttributes.ReparsePoint);
                entries.Add(CreateEntry(child, isDirectory, isReparsePoint));

                if (recursive && isDirectory && !isReparsePoint)
                {
                    pending.Enqueue(child.FullName);
                }
            }
        }

        return JsonSerializer.Serialize(new
        {
            path = root,
            recursive,
            truncated = entries.Count >= maxEntries && pending.Count > 0,
            entries,
            errors,
        }, JsonOptions);
    }

    public static string CreateDirectory(string path, bool dryRun)
    {
        var target = ResolvePath(path);
        var exists = Directory.Exists(target);
        if (File.Exists(target))
        {
            throw new IOException($"A file already exists at the requested directory path: {target}");
        }

        if (!dryRun && !exists)
        {
            Directory.CreateDirectory(target);
        }

        return JsonSerializer.Serialize(new
        {
            operation = "create_directory",
            path = target,
            dryRun,
            changed = !dryRun && !exists,
            alreadyExists = exists,
        }, JsonOptions);
    }

    public static string CopyPath(string source, string destination, bool overwrite, bool dryRun)
    {
        var sourcePath = ResolveExistingPath(source);
        var destinationPath = ResolvePath(destination);
        var sourceIsDirectory = Directory.Exists(sourcePath);
        ValidateDestination(sourcePath, destinationPath, sourceIsDirectory, overwrite);

        if (!dryRun)
        {
            if (sourceIsDirectory)
            {
                CopyDirectory(sourcePath, destinationPath, overwrite);
            }
            else
            {
                EnsureParentDirectory(destinationPath);
                File.Copy(sourcePath, destinationPath, overwrite);
            }
        }

        return JsonSerializer.Serialize(new
        {
            operation = "copy",
            source = sourcePath,
            destination = destinationPath,
            kind = sourceIsDirectory ? "directory" : "file",
            overwrite,
            dryRun,
            changed = !dryRun,
        }, JsonOptions);
    }

    public static string MovePath(string source, string destination, bool overwrite, bool dryRun)
    {
        var sourcePath = ResolveExistingPath(source);
        var destinationPath = ResolvePath(destination);
        var sourceIsDirectory = Directory.Exists(sourcePath);
        ValidateDestination(sourcePath, destinationPath, sourceIsDirectory, overwrite);

        if (!dryRun)
        {
            EnsureParentDirectory(destinationPath);
            if (sourceIsDirectory)
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                File.Move(sourcePath, destinationPath, overwrite);
            }
        }

        return JsonSerializer.Serialize(new
        {
            operation = "move",
            source = sourcePath,
            destination = destinationPath,
            kind = sourceIsDirectory ? "directory" : "file",
            overwrite,
            dryRun,
            changed = !dryRun,
        }, JsonOptions);
    }

    public static string DeletePath(string path, bool recursive, bool dryRun)
    {
        var target = ResolveExistingPath(path);
        var isDirectory = Directory.Exists(target);
        if (isDirectory && IsFilesystemRoot(target))
        {
            throw new IOException($"Refusing to delete a filesystem root: {target}");
        }

        if (!dryRun)
        {
            if (isDirectory)
            {
                Directory.Delete(target, recursive);
            }
            else
            {
                File.Delete(target);
            }
        }

        return JsonSerializer.Serialize(new
        {
            operation = "delete",
            path = target,
            kind = isDirectory ? "directory" : "file",
            recursive,
            dryRun,
            changed = !dryRun,
            warning = "Actual deletion is permanent; use dry_run=true first and pass false only after confirming the exact path.",
        }, JsonOptions);
    }

    private static FileSystemEntry CreateEntry(FileSystemInfo entry, bool isDirectory, bool isReparsePoint)
    {
        long? size = null;
        if (!isDirectory)
        {
            try
            {
                size = ((FileInfo)entry).Length;
            }
            catch (Exception exception) when (IsExpectedFilesystemAccessException(exception))
            {
                size = null;
            }
        }

        DateTimeOffset? modifiedUtc = null;
        try
        {
            modifiedUtc = entry.LastWriteTimeUtc;
        }
        catch (Exception exception) when (IsExpectedFilesystemAccessException(exception))
        {
            modifiedUtc = null;
        }

        return new FileSystemEntry(
            entry.Name,
            entry.FullName,
            isDirectory ? "directory" : "file",
            size,
            modifiedUtc,
            isReparsePoint,
            entry.Attributes.ToString());
    }

    private static void ValidateDestination(string source, string destination, bool sourceIsDirectory, bool overwrite)
    {
        if (string.Equals(source, destination, GetPathComparison()))
        {
            throw new IOException("Source and destination must be different paths.");
        }

        if (sourceIsDirectory && IsPathInside(destination, source))
        {
            throw new IOException("A directory cannot be copied or moved into itself.");
        }

        if (Directory.Exists(destination))
        {
            if (sourceIsDirectory)
            {
                throw new IOException($"Destination directory already exists: {destination}");
            }

            throw new IOException($"Destination path is an existing directory: {destination}");
        }

        if (sourceIsDirectory && File.Exists(destination))
        {
            throw new IOException($"A directory cannot replace an existing destination file: {destination}");
        }

        if (File.Exists(destination) && !overwrite)
        {
            throw new IOException($"Destination file already exists and overwrite=false: {destination}");
        }
    }

    private static void CopyDirectory(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);
        foreach (var child in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            if (child.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            var target = Path.Combine(destination, child.Name);
            if (child.Attributes.HasFlag(FileAttributes.Directory))
            {
                CopyDirectory(child.FullName, target, overwrite);
            }
            else
            {
                File.Copy(child.FullName, target, overwrite);
            }
        }
    }

    private static string ResolveExistingPath(string path)
    {
        var resolved = ResolvePath(path);
        if (!File.Exists(resolved) && !Directory.Exists(resolved))
        {
            throw new FileNotFoundException($"Path not found: {resolved}", resolved);
        }

        return resolved;
    }

    private static string ResolveExistingFile(string path)
    {
        var resolved = ResolveExistingPath(path);
        if (Directory.Exists(resolved))
        {
            throw new IOException($"A directory cannot be read as a text file: {resolved}");
        }

        return resolved;
    }

    private static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A non-empty path is required.", nameof(path));
        }

        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
    }

    private static void EnsureParentDirectory(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }
    }

    private static bool IsPathInside(string candidate, string parent)
    {
        var normalizedParent = EnsureTrailingSeparator(parent);
        return candidate.StartsWith(normalizedParent, GetPathComparison());
    }

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static StringComparison GetPathComparison()
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool IsFilesystemRoot(string path)
    {
        var root = Path.GetPathRoot(path);
        return root is not null && string.Equals(path, root.TrimEnd(Path.DirectorySeparatorChar), GetPathComparison())
            || root is not null && string.Equals(path, root, GetPathComparison());
    }

    private static void ValidateEntryLimit(int maxEntries)
    {
        if (maxEntries is < 1 or > 5_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "maxEntries must be between 1 and 5000.");
        }
    }

    private static void ValidateTextByteLimit(int maxBytes)
    {
        if (maxBytes is < 1 or > MaxTextBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), $"maxBytes must be between 1 and {MaxTextBytes}.");
        }
    }

    private static string DecodeUtf8Prefix(ReadOnlySpan<byte> bytes)
    {
        var decoder = Utf8NoBom.GetDecoder();
        var chars = new char[Utf8NoBom.GetMaxCharCount(bytes.Length)];
        decoder.Convert(bytes, chars, flush: false, out _, out var charsUsed, out _);
        var content = new string(chars, 0, charsUsed);
        return content.Length > 0 && content[0] == '\uFEFF' ? content[1..] : content;
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // The original destination remains intact if cleanup cannot complete.
        }
        catch (UnauthorizedAccessException)
        {
            // The original destination remains intact if cleanup cannot complete.
        }
    }

    private static bool IsExpectedFilesystemAccessException(Exception exception)
        => exception is IOException or UnauthorizedAccessException or ArgumentException;
}

internal sealed record FileSystemEntry(
    string Name,
    string Path,
    string Kind,
    long? Size,
    DateTimeOffset? ModifiedUtc,
    bool IsReparsePoint,
    string Attributes);

internal sealed record FileSystemOperationError(string Path, string ErrorType, string Message);
