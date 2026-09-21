using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides semantic UI inspection and control. Windows loads Microsoft UI Automation on
/// demand so MCP tool discovery does not require a Windows Desktop assembly on every host.
/// Other platforms report an actionable unsupported-platform error.
/// </summary>
internal static class SemanticService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<WindowsUiAutomation> WindowsRuntime = new(WindowsUiAutomation.Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string FindElements(long windowHandle, string? nameContains, string? role, string? automationId, int maxElements)
    {
        if (maxElements is < 1 or > 1_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maxElements), "maxElements must be between 1 and 1000.");
        }

        var runtime = GetRuntime();
        return runtime.Execute(() =>
        {
            var root = runtime.GetRoot(windowHandle);
            var normalizedName = Normalize(nameContains);
            var normalizedRole = NormalizeRole(role);
            var normalizedAutomationId = Normalize(automationId);
            var matches = new List<SemanticElement>();
            var truncated = false;

            foreach (var element in runtime.EnumerateElements(root, maxElements + 1))
            {
                if (!runtime.Matches(element, normalizedName, normalizedRole, normalizedAutomationId))
                {
                    continue;
                }

                if (matches.Count >= maxElements)
                {
                    truncated = true;
                    break;
                }

                matches.Add(runtime.CreateElement(element));
            }

            return JsonSerializer.Serialize(new
            {
                windowHandle,
                nameContains,
                role = normalizedRole,
                automationId,
                maxElements,
                truncated,
                elements = matches,
            }, JsonOptions);
        });
    }

    public static string InvokeElement(long windowHandle, string elementId, string action)
    {
        if (string.IsNullOrWhiteSpace(elementId))
        {
            throw new ArgumentException("A semantic element id is required.", nameof(elementId));
        }

        var normalizedAction = Normalize(action)?.ToLowerInvariant();
        if (normalizedAction is not ("invoke" or "toggle" or "select" or "focus" or "expand" or "collapse"))
        {
            throw new ArgumentException("action must be invoke, toggle, select, focus, expand, or collapse.", nameof(action));
        }

        var runtime = GetRuntime();
        return runtime.Execute(() =>
        {
            var element = runtime.FindElementById(runtime.GetRoot(windowHandle), elementId)
                ?? throw new InvalidOperationException($"Semantic element was not found or is stale: {elementId}");
            runtime.ApplyAction(element, normalizedAction);

            return JsonSerializer.Serialize(new
            {
                windowHandle,
                elementId,
                action = normalizedAction,
                changed = normalizedAction != "focus",
            }, JsonOptions);
        });
    }

    public static string SetValue(long windowHandle, string elementId, string value)
    {
        if (string.IsNullOrWhiteSpace(elementId))
        {
            throw new ArgumentException("A semantic element id is required.", nameof(elementId));
        }

        var runtime = GetRuntime();
        return runtime.Execute(() =>
        {
            var element = runtime.FindElementById(runtime.GetRoot(windowHandle), elementId)
                ?? throw new InvalidOperationException($"Semantic element was not found or is stale: {elementId}");
            runtime.SetValue(element, value ?? string.Empty);

            return JsonSerializer.Serialize(new
            {
                windowHandle,
                elementId,
                valueLength = value?.Length ?? 0,
                changed = true,
            }, JsonOptions);
        });
    }

    private static WindowsUiAutomation GetRuntime()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Semantic UI Automation currently requires Windows UI Automation in this build; browser semantic inspection is available through the browser tools when a CDP endpoint is configured.");
        }

        return WindowsRuntime.Value;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeRole(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return null;
        }

        const string prefix = "ControlType.";
        return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? normalized[prefix.Length..].ToLowerInvariant()
            : normalized.ToLowerInvariant();
    }

    private sealed class WindowsUiAutomation
    {
        private const string AutomationElementTypeName = "System.Windows.Automation.AutomationElement";
        private const string ControlTypeTypeName = "System.Windows.Automation.ControlType";
        private const string TreeScopeTypeName = "System.Windows.Automation.TreeScope";
        private const string ConditionTypeName = "System.Windows.Automation.Condition";

        private readonly Type _automationElementType;
        private readonly object _treeScopeDescendants;
        private readonly object _trueCondition;
        private readonly object _boundingRectangleProperty;
        private readonly object _notSupported;
        private readonly Dictionary<string, Type> _patternTypes;
        private readonly IReadOnlyList<Assembly> _assemblies;
        private readonly AssemblyLoadContext _loadContext;
        private readonly object _executionLock = new();

        private WindowsUiAutomation(IReadOnlyList<Assembly> assemblies, AssemblyLoadContext loadContext)
        {
            _assemblies = assemblies;
            _loadContext = loadContext;
            _automationElementType = FindType(AutomationElementTypeName)
                ?? throw new PlatformNotSupportedException("UIAutomationClient does not expose AutomationElement.");
            var treeScopeType = FindType(TreeScopeTypeName)
                ?? throw new PlatformNotSupportedException("UIAutomationClient does not expose TreeScope.");
            var conditionType = FindType(ConditionTypeName)
                ?? throw new PlatformNotSupportedException("UIAutomationClient does not expose Condition.");

            _treeScopeDescendants = Enum.Parse(treeScopeType, "Descendants");
            _trueCondition = GetStaticField(conditionType, "TrueCondition");
            _boundingRectangleProperty = GetStaticField(_automationElementType, "BoundingRectangleProperty");
            _notSupported = GetStaticField(_automationElementType, "NotSupported");
            _patternTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                ["invoke"] = GetPatternType("InvokePattern"),
                ["toggle"] = GetPatternType("TogglePattern"),
                ["select"] = GetPatternType("SelectionItemPattern"),
                ["value"] = GetPatternType("ValuePattern"),
                ["expandCollapse"] = GetPatternType("ExpandCollapsePattern"),
            };
        }

        public static WindowsUiAutomation Load()
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Microsoft UI Automation is only available on Windows.");
            }

            var loaded = FindOrLoadAssemblies();
            return new WindowsUiAutomation(loaded.Assemblies, loaded.LoadContext);
        }

        public object GetRoot(long windowHandle)
        {
            var handle = new IntPtr(windowHandle);
            if (windowHandle <= 0 || !NativeMethods.IsWindow(handle))
            {
                throw new ArgumentException($"Window handle {windowHandle} is not a valid open window.", nameof(windowHandle));
            }

            try
            {
                return InvokeStatic(_automationElementType, "FromHandle", handle)
                    ?? throw new InvalidOperationException($"UI Automation returned no root element for window {windowHandle}.");
            }
            catch (Exception exception) when (IsAutomationFailure(exception))
            {
                throw new InvalidOperationException($"UI Automation could not inspect window {windowHandle}.", Unwrap(exception));
            }
        }

        public T Execute<T>(Func<T> action)
        {
            lock (_executionLock)
            {
                if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                {
                    return action();
                }

                T? result = default;
                Exception? failure = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        result = action();
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                })
                {
                    IsBackground = true,
                    Name = "CodexComputerRun.UIAutomation.STA",
                };
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("UI Automation STA execution is only available on Windows.");
                }

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                if (failure is not null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                }

                return result!;
            }
        }

        public IEnumerable<object> EnumerateElements(object root, int maxElements)
        {
            yield return root;
            var descendants = InvokeInstance(root, "FindAll", _treeScopeDescendants, _trueCondition);
            var count = Math.Min(ReadIntProperty(descendants!, "Count"), maxElements - 1);
            for (var index = 0; index < count; index++)
            {
                object? element;
                try
                {
                    element = GetIndexedValue(descendants!, index);
                }
                catch (Exception exception) when (IsAutomationFailure(exception))
                {
                    continue;
                }

                if (element is not null)
                {
                    yield return element;
                }
            }
        }

        public bool Matches(object element, string? nameContains, string? role, string? automationId)
        {
            try
            {
                var current = GetProperty(element, "Current");
                var name = ReadStringProperty(current, "Name") ?? string.Empty;
                var controlType = GetProperty(current, "ControlType");
                var actualRole = NormalizeRole(ReadStringProperty(controlType, "ProgrammaticName"));
                var actualAutomationId = ReadStringProperty(current, "AutomationId");
                return (nameContains is null || name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
                    && (role is null || string.Equals(actualRole, role, StringComparison.OrdinalIgnoreCase))
                    && (automationId is null || string.Equals(actualAutomationId, automationId, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception exception) when (IsAutomationFailure(exception))
            {
                return false;
            }
        }

        public SemanticElement CreateElement(object element)
        {
            var current = GetProperty(element, "Current");
            var controlType = GetProperty(current, "ControlType");
            var supportedPatterns = _patternTypes
                .Where(pattern => SupportsPattern(element, pattern.Value))
                .Select(pattern => pattern.Key)
                .ToArray();

            return new SemanticElement(
                GetElementId(element),
                ReadStringProperty(current, "Name"),
                NormalizeRole(ReadStringProperty(controlType, "ProgrammaticName")),
                ReadStringProperty(current, "AutomationId"),
                ReadStringProperty(current, "ClassName"),
                ReadStringProperty(current, "HelpText"),
                ReadBoolProperty(current, "IsEnabled"),
                ReadBoolProperty(current, "IsOffscreen"),
                ReadBounds(element),
                supportedPatterns);
        }

        public object? FindElementById(object root, string elementId)
        {
            foreach (var element in EnumerateElements(root, 5_000))
            {
                try
                {
                    if (string.Equals(GetElementId(element), elementId.Trim(), StringComparison.Ordinal))
                    {
                        return element;
                    }
                }
                catch (Exception exception) when (IsAutomationFailure(exception))
                {
                    // Continue because the tree is dynamic.
                }
            }

            return null;
        }

        public void ApplyAction(object element, string action)
        {
            switch (action)
            {
                case "focus":
                    InvokeInstance(element, "SetFocus");
                    return;
                case "invoke":
                    InvokePatternMethod(element, "invoke", "Invoke");
                    return;
                case "toggle":
                    InvokePatternMethod(element, "toggle", "Toggle");
                    return;
                case "select":
                    InvokePatternMethod(element, "select", "Select");
                    return;
                case "expand":
                    InvokePatternMethod(element, "expandCollapse", "Expand");
                    return;
                case "collapse":
                    InvokePatternMethod(element, "expandCollapse", "Collapse");
                    return;
            }
        }

        public void SetValue(object element, string value)
        {
            var pattern = GetPattern(element, "value")
                ?? throw new InvalidOperationException("The element does not support ValuePattern.");
            var current = GetProperty(pattern, "Current");
            if (ReadBoolProperty(current, "IsReadOnly"))
            {
                throw new InvalidOperationException("The element reports that its value is read-only.");
            }

            InvokeInstance(pattern, "SetValue", value);
        }

        private void InvokePatternMethod(object element, string patternName, string methodName)
        {
            var pattern = GetPattern(element, patternName)
                ?? throw new InvalidOperationException($"The element does not support the {patternName} control pattern.");
            InvokeInstance(pattern, methodName);
        }

        private object? GetPattern(object element, string patternName)
        {
            var patternIdentifier = GetStaticProperty(_patternTypes[patternName], "Pattern");
            var arguments = new object?[] { patternIdentifier, null };
            var supported = (bool)(InvokeInstance(element, "TryGetCurrentPattern", arguments) ?? false);
            return supported ? arguments[1] : null;
        }

        private bool SupportsPattern(object element, Type patternType)
        {
            try
            {
                var patternIdentifier = GetStaticProperty(patternType, "Pattern");
                var arguments = new object?[] { patternIdentifier, null };
                return (bool)(InvokeInstance(element, "TryGetCurrentPattern", arguments) ?? false);
            }
            catch (Exception exception) when (IsAutomationFailure(exception))
            {
                return false;
            }
        }

        private SemanticBounds ReadBounds(object element)
        {
            try
            {
                var value = InvokeInstance(element, "GetCurrentPropertyValue", _boundingRectangleProperty);
                if (value is null || ReferenceEquals(value, _notSupported))
                {
                    return new SemanticBounds(0, 0, 0, 0);
                }

                return new SemanticBounds(
                    ReadNumericProperty(value, "Left"),
                    ReadNumericProperty(value, "Top"),
                    ReadNumericProperty(value, "Width"),
                    ReadNumericProperty(value, "Height"));
            }
            catch (Exception exception) when (IsAutomationFailure(exception))
            {
                return new SemanticBounds(0, 0, 0, 0);
            }
        }

        private string GetElementId(object element)
        {
            var runtimeId = InvokeInstance(element, "GetRuntimeId") as Array;
            if (runtimeId is null)
            {
                return "uia:";
            }

            var values = runtimeId.Cast<object>().Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            return "uia:" + string.Join('.', values);
        }

        private Type GetPatternType(string name)
            => FindType($"System.Windows.Automation.{name}")
                ?? throw new PlatformNotSupportedException($"UIAutomationClient does not expose {name}.");

        private Type? FindType(string fullName)
            => _assemblies
                .Concat(AppDomain.CurrentDomain.GetAssemblies())
                .Select(assembly => assembly.GetType(fullName, throwOnError: false, ignoreCase: false))
                .FirstOrDefault(type => type is not null);

        private static (IReadOnlyList<Assembly> Assemblies, AssemblyLoadContext LoadContext) FindOrLoadAssemblies()
        {
            var requiredNames = new[]
            {
                "UIAutomationTypes.dll",
                "UIAutomationClient.dll",
                "UIAutomationProvider.dll",
                "WindowsBase.dll",
                "DirectWriteForwarder.dll",
                "PresentationCore.dll",
            };
            var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => requiredNames.Any(name => string.Equals(
                    assembly.GetName().Name + ".dll", name, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (alreadyLoaded.Any(assembly => string.Equals(assembly.GetName().Name, "UIAutomationClient", StringComparison.OrdinalIgnoreCase)))
            {
                return (alreadyLoaded, AssemblyLoadContext.Default);
            }

            var roots = new List<string>();
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var desktopRoot = Path.Combine(programFiles, "dotnet", "shared", "Microsoft.WindowsDesktop.App");
            if (Directory.Exists(desktopRoot))
            {
                roots.AddRange(Directory.GetDirectories(desktopRoot)
                    .OrderByDescending(path => ParseVersion(Path.GetFileName(path))));
            }

            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319", "WPF"));
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework", "v4.0.30319", "WPF"));

            foreach (var root in roots)
            {
                var clientPath = Path.Combine(root, "UIAutomationClient.dll");
                if (!File.Exists(clientPath))
                {
                    continue;
                }

                try
                {
                    var assemblies = requiredNames
                        .Select(name => Path.Combine(root, name))
                        .Where(File.Exists)
                        .Select(path => AssemblyLoadContext.Default.LoadFromAssemblyPath(path))
                        .ToArray();
                    return (assemblies, AssemblyLoadContext.Default);
                }
                catch (FileLoadException)
                {
                    var fallback = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(assembly => requiredNames.Any(name => string.Equals(
                            assembly.GetName().Name + ".dll", name, StringComparison.OrdinalIgnoreCase)))
                        .ToArray();
                    if (fallback.Any(assembly => string.Equals(assembly.GetName().Name, "UIAutomationClient", StringComparison.OrdinalIgnoreCase)))
                    {
                        return (fallback, AssemblyLoadContext.Default);
                    }
                }
            }

            throw new PlatformNotSupportedException("UIAutomationClient.dll was not found. Install the Windows Desktop Runtime or enable Windows UI Automation.");
        }

        private static Version ParseVersion(string? value)
            => Version.TryParse(value, out var version) ? version : new Version(0, 0);

        private static object GetStaticProperty(Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (property is not null)
            {
                return property.GetValue(null)
                    ?? throw new PlatformNotSupportedException($"UI Automation static property {type.FullName}.{name} returned null.");
            }

            return GetStaticField(type, name);
        }

        private static object GetStaticField(Type type, string name)
            => type.GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? throw new PlatformNotSupportedException($"UI Automation type {type.FullName} does not expose static field {name}.");

        private static object? GetProperty(object? instance, string name)
            => instance?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);

        private static string? ReadStringProperty(object? instance, string name)
            => instance is null ? null : Convert.ToString(GetProperty(instance, name), System.Globalization.CultureInfo.InvariantCulture);

        private static bool ReadBoolProperty(object? instance, string name)
            => instance is not null && GetProperty(instance, name) is bool value && value;

        private static int ReadIntProperty(object instance, string name)
            => Convert.ToInt32(GetProperty(instance, name), System.Globalization.CultureInfo.InvariantCulture);

        private static double ReadNumericProperty(object instance, string name)
            => Convert.ToDouble(GetProperty(instance, name), System.Globalization.CultureInfo.InvariantCulture);

        private static object? GetIndexedValue(object instance, int index)
            => instance.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance, [index]);

        private static object? InvokeStatic(Type type, string name, params object?[] arguments)
            => InvokeMethod(type, null, name, arguments);

        private static object? InvokeInstance(object instance, string name, params object?[] arguments)
            => InvokeMethod(instance.GetType(), instance, name, arguments);

        private static object? InvokeMethod(Type type, object? instance, string name, object?[] arguments)
        {
            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(candidate => candidate.Name == name)
                .FirstOrDefault(candidate => candidate.GetParameters().Length == arguments.Length)
                ?? throw new MissingMethodException(type.FullName, name);
            try
            {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static bool IsAutomationFailure(Exception exception)
        {
            var unwrapped = Unwrap(exception);
            return unwrapped is InvalidOperationException
                or TargetInvocationException
                or COMException
                or ExternalException;
        }

        private static Exception Unwrap(Exception exception)
            => exception is TargetInvocationException { InnerException: { } inner }
                ? Unwrap(inner)
                : exception;
    }
}

internal sealed record SemanticElement(
    string Id,
    string? Name,
    string? Role,
    string? AutomationId,
    string? ClassName,
    string? HelpText,
    bool IsEnabled,
    bool IsOffscreen,
    SemanticBounds Bounds,
    IReadOnlyList<string> SupportedPatterns);

internal sealed record SemanticBounds(double Left, double Top, double Width, double Height);
