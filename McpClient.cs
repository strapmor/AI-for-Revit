using Autodesk.Revit.DB;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyPlugin
{
    public class McpServerConfig
    {
        public string Command { get; set; }
        public List<string> Args { get; set; }
    }

    public class McpServers
    {
        public Dictionary<string, McpServerConfig> mcpServers { get; set; }
    }

    public class McpClient
    {
        public IMcpClient Client { get; private set; }
        private static McpClient instance;
        private bool isInitialized;

        private McpClient()
        {
            isInitialized = false;
        }

        public static McpClient Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new McpClient();
                }
                return instance;
            }
        }

        public async Task InitializeAsync()
        {
            if (isInitialized) return;

            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mcp-config.json");
                string mcpConfig = File.ReadAllText(configPath);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var config = JsonSerializer.Deserialize<McpServers>(mcpConfig, options);

                if (config?.mcpServers == null || !config.mcpServers.ContainsKey("revit-mcp"))
                {
                    throw new Exception("Invalid MCP configuration: revit-mcp server not found");
                }

                var serverConfig = config.mcpServers["revit-mcp"];
                var clientTransport = new StdioClientTransport(new StdioClientTransportOptions 
                { 
                    Name = "revit-mcp",
                    Command = serverConfig.Command,
                    Arguments = serverConfig.Args
                });

                Client = await McpClientFactory.CreateAsync(clientTransport);
                isInitialized = true;
                
                Logger.Log("MCP client initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to initialize MCP client: {ex.Message}");
                throw;
            }
        }

        public async Task<IReadOnlyList<Tool>> GetAvailableToolsAsync()
        {
            if (!isInitialized)
                throw new InvalidOperationException("MCP client is not initialized");

            return await Client.ListToolsAsync() as IReadOnlyList<Tool>;
        }

        public async Task<CallToolResponse> ExecuteToolAsync(string toolName, Dictionary<string, object?> parameters)
        {
            if (!isInitialized)
                throw new InvalidOperationException("MCP client is not initialized");

            try
            {
                // Convert Dictionary to IReadOnlyDictionary
                IReadOnlyDictionary<string, object?> readOnlyParams = parameters;

                // Call the tool without JsonSerializerOptions parameter
                return await Client.CallToolAsync(toolName, readOnlyParams, null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error executing tool {toolName}: {ex.Message}");
                throw;
            }
        }
    }
}
