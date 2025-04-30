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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AI_for_Revit
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

    public class ToolProperty
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }

    public class ToolParametersSchema
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "object";

        [JsonProperty("properties")]
        public Dictionary<string, ToolProperty> Properties { get; set; } = new Dictionary<string, ToolProperty>();

        [JsonProperty("required")]
        public List<string> Required { get; set; } = new List<string>();
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
                
                var config = System.Text.Json.JsonSerializer.Deserialize<McpServers>(mcpConfig, options);

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

        public async Task<IList<McpClientTool>> GetAvailableToolsAsync()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("MCP client is not initialized");
            }

            var tools = await Client.ListToolsAsync();
            if (tools == null)
            {
                throw new InvalidOperationException("The ListToolsAsync method returned null");
            }

            return tools ?? throw new InvalidOperationException("Failed to cast tools to IReadOnlyList<Tool>");
        }

        public async Task<CallToolResponse> ExecuteToolAsync(string toolName, Dictionary<string, object?> parameters)
        {
            if (!isInitialized)
                throw new InvalidOperationException("MCP client is not initialized");

            try
            {
                // Convert Dictionary to IReadOnlyDictionary
                IReadOnlyDictionary<string, object?> readOnlyParams = parameters;

                // Create JsonSerializerOptions with default settings
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };
                
                return await Client.CallToolAsync(toolName, readOnlyParams, null, null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error executing tool {toolName}: {ex.Message}");
                throw;
            }
        }
    }
}
