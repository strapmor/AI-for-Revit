using Autodesk.Revit.DB;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyPlugin
{
    internal class McpClient
    {
        

        McpClient() 
        {
            
        }

        public async void Foo()
        {
            McpServerConfig cfg = new McpServerConfig();
            cfg.Id = "demo-server";
            

            var client = await McpClientFactory.CreateAsync();

            // Print the list of tools available from the server.
            foreach (var tool in await client.ListToolsAsync())
            {
                Console.WriteLine($"{tool.Name} ({tool.Description})");
            }

            // Execute a tool (this would normally be driven by LLM tool invocations).
            var result = await client.CallToolAsync(
                "echo",
                new Dictionary<string, object?>() { ["message"] = "Hello MCP!" },
                CancellationToken.None);

            // echo always returns one and only one text content object
            Console.WriteLine(result.Content.First(c => c.Type == "text").Text);
        }
    }
}
