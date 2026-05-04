    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using ModelContextProtocol.Server;

    namespace CodexComputerRunMCPServer;

    /// <summary>
    /// Provides application startup and host configuration for the MCP server process.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the process.</param>
        /// <returns>
        /// A task that resolves to an exit code:
        /// <c>0</c> when the server exits normally; <c>1</c> when startup is rejected
        /// because the current operating system is not Windows.
        /// </returns>
        /// <remarks>
        /// This server is supported only in a signed-in Windows desktop session.
        /// The method also enables per-monitor DPI awareness before creating and running the host.
        /// </remarks>
        [ExcludeFromCodeCoverage]
        public static async Task<int> Main(string[] args)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("CodexComputerRunMCPServer is Windows-only. Run it from a signed-in Windows desktop session.");
                return 1;
            }

            NativeMethods.TryEnablePerMonitorDpiAwareness();

            using var host = CreateHost(args);
            await host.RunAsync().ConfigureAwait(false);
            return 0;
        }

        /// <summary>
        /// Creates and configures the application host used by the MCP server.
        /// </summary>
        /// <param name="args">Command-line arguments used to initialize the host builder.</param>
        /// <returns>A fully configured <see cref="IHost"/> instance.</returns>
        /// <remarks>
        /// Logging providers are reset and console logging is directed to standard error so that
        /// standard output remains reserved for MCP JSON-RPC stdio transport traffic.
        /// </remarks>
        public static IHost CreateHost(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(options =>
            {
                // MCP stdio reserves stdout for JSON-RPC. Diagnostics must go to stderr.
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();

            return builder.Build();
        }
    }
