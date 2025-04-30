﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Threading;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using System.IO;

namespace AI_for_Revit
{
    public class AIService
    {
        private readonly McpClient mcpClient;
        private static readonly string apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_FREE");
        private static readonly string apiUrl = "https://openrouter.ai/api/v1/chat/completions";
        private static readonly string MODEL = "deepseek/deepseek-chat"; 
        public static List<Dictionary<string, string>>? conversationHistory;

        public AIService()
        {
            mcpClient = McpClient.Instance;
            conversationHistory = new List<Dictionary<string, string>>();
            // Добавляем системный промпт
            conversationHistory.Add(new Dictionary<string, string> 
            {
                { "role", "system" },
                { "content", @"Ты помощник для пользователей Autodesk Revit." }
            });
        }

        public async Task Initialize()
        {
            try
            {
                await mcpClient.InitializeAsync();
                var tools = await mcpClient.GetAvailableToolsAsync();
                Logger.Log($"Доступные MCP инструменты: {string.Join(", ", tools.Select(t => t.Name))}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка при инициализации MCP клиента: {ex.Message}");
                throw;
            }
}

        public async Task<AIResponse> SendToChatGPT(string userPrompt, Func<AIResponse, Task> onPartialResponse)
        {
            try
            {
                if (userPrompt != String.Empty)
                {
                    // Добавляем запрос пользователя в историю
                    conversationHistory.Add(new Dictionary<string, string>
                    {
                        { "role", "user" },
                        { "content", userPrompt }
                    });
                }
                
                var response = new AIResponse();
                var fullResponse = new StringBuilder();
                var mcpBuffer = new StringBuilder();

                await SendToAIStream(conversationHistory, async chunk => 
                {
                    fullResponse.Append(chunk);
                    mcpBuffer.Append(chunk);

                    // Проверяем, есть ли в буфере вызов MCP инструмента
                    //var mcpCall = ExtractMcpCalls(mcpBuffer);
                    //if (mcpCall != null)
                    //{
                    //    // Обрабатываем MCP инструмент
                    //    var mcpResponse = await ProcessMcpCall(mcpCall);
                    //    response.McpResponses.Add(mcpResponse);
                    //    mcpBuffer.Clear();
                    //}

                    // Возвращаем частичный ответ
                    response.Answer = fullResponse.ToString();
                    response.IsPartial = true;
                    await onPartialResponse(response);
                });

                // Добавляем полный ответ в историю
                conversationHistory.Add(new Dictionary<string, string> 
                { 
                    { "role", "assistant" }, 
                    { "content", fullResponse.ToString() } 
                });

                // Возвращаем финальный ответ
                response.Answer = fullResponse.ToString();
                response.IsPartial = false;
                return response;
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка при обработке запроса: {ex.Message}");
                return new AIResponse
                {
                    Answer = "",
                    ErrorMessage = $"Ошибка при обработке запроса: {ex.Message}"
                };
            }
        }

        private async Task<List<object>> GetToolsSchemaAsync()
        {
            var tools = await mcpClient.Client.ListToolsAsync();
            var schemas = new List<object>();

            foreach (var tool in tools)
            {
                    var paramsSchema = System.Text.Json.JsonSerializer.Deserialize<ToolParametersSchema>(
                        tool.ProtocolTool.InputSchema.ToString(),
                        new System.Text.Json.JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                var schema = new
                {
                    type = "function",
                    function = new
                    {
                        name = tool.Name,
                        description = tool.Description,
                        parameters = new
                        {
                            type = paramsSchema.Type,
                            properties = paramsSchema.Properties?.ToDictionary(
                                p => p.Key,
                                p => new
                                {
                                    type = p.Value.Type,
                                    description = p.Value.Description
                                }),
                            required = paramsSchema.Required
                        }
                    }
                };

                schemas.Add(schema);
            }

            return schemas;
        }

        private McpToolCall? ExtractMcpCalls(StringBuilder buffer)
        {
            var text = buffer.ToString();
            var pattern = @"[\w_]+\s*\(.*?\)";
            var match = Regex.Match(text, pattern);
            
            if (match.Success)
            {
                try
                {
                    var toolName = match.Value.Split('(')[0].Trim();
                    var paramsJson = match.Value[(toolName.Length + 1)..^1];
                    var parameters = JsonConvert.DeserializeObject<Dictionary<string, object?>>(paramsJson);
                    
                    return new McpToolCall 
                    { 
                        Tool = toolName,
                        Parameters = parameters
                    };
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        private async Task<McpResponseItem> ProcessMcpCall(McpToolCall mcpCall)
        {
            var response = new McpResponseItem
            {
                Tool = mcpCall.Tool,
                Status = ResponseStatus.InProgress
            };

            try
            {
                var result = await ExecuteMcpTool(mcpCall.Tool, mcpCall.Parameters);
                response.Result = result;

                if (result != null)
                {
                    // Добавляем результат выполнения инструмента в историю
                    var toolResponse = new Dictionary<string, string>
                    {
                        { "role", "tool" },
                        { "name", mcpCall.Tool },
                        { "content", JsonConvert.SerializeObject(result) }
                    };
                    conversationHistory.Add(toolResponse);

                    // Отправляем обновленный conversationHistory в Deepseek
                    await SendToChatGPT("", async partialResponse => { });
                    // Если результат содержит Content, обрабатываем его
                    if (result.Content != null && result.Content.Any())
                    {
                        var statusContent = result.Content.FirstOrDefault(c => c.Type.ToLower() == "status");
                        var messageContent = result.Content.FirstOrDefault(c => c.Type.ToLower() == "text" || c.Type.ToLower() == "error");

                        response.Status = statusContent?.Text?.ToLower() == "error" ? ResponseStatus.Error :
                                       statusContent?.Text?.ToLower() == "warning" ? ResponseStatus.Warning :
                                       ResponseStatus.Success;

                        response.Message = messageContent?.Text;
                    }
                    // Если Content отсутствует, считаем выполнение успешным
                    else
                    {
                        response.Status = ResponseStatus.Success;
                        response.Message = "Команда выполнена успешно";
                    }
                }
                }
                catch (Exception ex)
                {
                    response.Status = ResponseStatus.Error;
                    response.Message = ex.Message;
                    Logger.Log($"Ошибка при выполнении MCP команды: {ex.Message}");
                }

            return response;
        }

        private async Task SendToAIStream(List<Dictionary<string, string>> messages, Func<string, Task> onChunkReceived)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var tools = new List<object>
                    {
                        new 
                        {
                            type = "function",
                            function = new 
                            {
                                name = "color_elements",
                                description = "Color elements in the current view based on a category and parameter value",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        categoryName = new { type = "string", description = "The name of the Revit category to color" },
                                        parameterName = new { type = "string", description = "The name of the parameter to use for grouping and coloring elements" },
                                        useGradient = new { type = "boolean", description = "Whether to use a gradient color scheme" },
                                        customColors = new 
                                        { 
                                            type = "array",
                                            items = new
                                            {
                                                type = "object",
                                                properties = new
                                                {
                                                    r = new { type = "integer", minimum = 0, maximum = 255 },
                                                    g = new { type = "integer", minimum = 0, maximum = 255 },
                                                    b = new { type = "integer", minimum = 0, maximum = 255 }
                                                }
                                            }
                                        }
                                    },
                                    required = new[] { "categoryName", "parameterName" }
                                }
                            }
                        },
                        new 
                        {
                            type = "function",
                            function = new 
                            {
                                name = "create_line_based_element",
                                description = "Create one or more line-based elements in Revit such as walls, beams, or pipes",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        data = new
                                        {
                                            type = "array",
                                            items = new
                                            {
                                                type = "object",
                                                properties = new
                                                {
                                                    name = new { type = "string" },
                                                    typeId = new { type = "number" },
                                                    locationLine = new
                                                    {
                                                        type = "object",
                                                        properties = new
                                                        {
                                                            p0 = new
                                                            {
                                                                type = "object",
                                                                properties = new
                                                                {
                                                                    x = new { type = "number" },
                                                                    y = new { type = "number" },
                                                                    z = new { type = "number" }
                                                                }
                                                            },
                                                            p1 = new
                                                            {
                                                                type = "object",
                                                                properties = new
                                                                {
                                                                    x = new { type = "number" },
                                                                    y = new { type = "number" },
                                                                    z = new { type = "number" }
                                                                }
                                                            }
                                                        }
                                                    },
                                                    thickness = new { type = "number" },
                                                    height = new { type = "number" },
                                                    baseLevel = new { type = "number" },
                                                    baseOffset = new { type = "number" }
                                                },
                                                required = new[] { "name", "locationLine", "thickness", "height", "baseLevel", "baseOffset" }
                                            }
                                        }
                                    },
                                    required = new[] { "data" }
                                }
                            }
                        },
                        new 
                        {
                            type = "function",
                            function = new 
                            {
                                name = "create_point_based_element",
                                description = "Create one or more point-based elements in Revit such as doors, windows, or furniture",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        data = new
                                        {
                                            type = "array",
                                            items = new
                                            {
                                                type = "object",
                                                properties = new
                                                {
                                                    name = new { type = "string" },
                                                    typeId = new { type = "number" },
                                                    locationPoint = new
                                                    {
                                                        type = "object",
                                                        properties = new
                                                        {
                                                            x = new { type = "number" },
                                                            y = new { type = "number" },
                                                            z = new { type = "number" }
                                                        }
                                                    },
                                                    width = new { type = "number" },
                                                    depth = new { type = "number" },
                                                    height = new { type = "number" },
                                                    baseLevel = new { type = "number" },
                                                    baseOffset = new { type = "number" },
                                                    rotation = new { type = "number" }
                                                },
                                                required = new[] { "name", "locationPoint", "width", "height", "baseLevel", "baseOffset" }
                                            }
                                        }
                                    },
                                    required = new[] { "data" }
                                }
                            }
                        },
                        new 
                        {
                            type = "function",
                            function = new 
                            {
                                name = "create_surface_based_element",
                                description = "Create one or more surface-based elements in Revit such as floors, ceilings, or roofs",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        data = new
                                        {
                                            type = "array",
                                            items = new
                                            {
                                                type = "object",
                                                properties = new
                                                {
                                                    name = new { type = "string" },
                                                    typeId = new { type = "number" },
                                                    boundary = new
                                                    {
                                                        type = "object",
                                                        properties = new
                                                        {
                                                            outerLoop = new
                                                            {
                                                                type = "array",
                                                                items = new
                                                                {
                                                                    type = "object",
                                                                    properties = new
                                                                    {
                                                                        p0 = new
                                                                        {
                                                                            type = "object",
                                                                            properties = new
                                                                            {
                                                                                x = new { type = "number" },
                                                                                y = new { type = "number" },
                                                                                z = new { type = "number" }
                                                                            }
                                                                        },
                                                                        p1 = new
                                                                        {
                                                                            type = "object",
                                                                            properties = new
                                                                            {
                                                                                x = new { type = "number" },
                                                                                y = new { type = "number" },
                                                                                z = new { type = "number" }
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    },
                                                    thickness = new { type = "number" },
                                                    baseLevel = new { type = "number" },
                                                    baseOffset = new { type = "number" }
                                                },
                                                required = new[] { "name", "boundary", "thickness", "baseLevel", "baseOffset" }
                                            }
                                        }
                                    },
                                    required = new[] { "data" }
                                }
                            }
                        },
                        new 
                        {
                            type = "function",
                            function = new 
                            {
                                name = "delete_element",
                                description = "Delete one or more elements from the Revit model by their element IDs",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        elementIds = new
                                        {
                                            type = "array",
                                            items = new { type = "string" }
                                        }
                                    },
                                    required = new[] { "elementIds" }
                                }
                            }
                        },
                        new 
                        {
                            type = "function",
                            function = new 
                            {
                                name = "get_current_view_elements",
                                description = "Get elements from the current active view in Revit",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        modelCategoryList = new
                                        {
                                            type = "array",
                                            items = new { type = "string" }
                                        },
                                        annotationCategoryList = new
                                        {
                                            type = "array",
                                            items = new { type = "string" }
                                        },
                                        includeHidden = new { type = "boolean" },
                                        limit = new { type = "number" }
                                    }
                                }
                            }
                        },
                        new 
                        {
                            type = "function",
                            function = new 
                            {
                                name = "get_current_view_info",
                                description = "获取 Revit 当前活动视图的详细信息，包括视图类型、名称、比例等属性",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new{}
                                }
                            }
                        },
                        new 
                        {
                            type = "function",
                            function = new 
                            {
                                name = "get_selected_elements",
                                description = "Get elements currently selected in Revit",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        limit = new { type = "number" }
                                    }
                                }
                            }
                        },
                        new
                        {
                            type = "function",
                            function = new
                            {
                                name = "get_available_family_types",
                                description = "Get available family types in the current Revit project",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        categoryList = new
                                        {
                                            type = "array",
                                            items = new { type = "string" }
                                        },
                                        familyNameFilter = new { type = "string" },
                                        limit = new { type = "number" }
                                    }
                                }
                            }
                        },
                        new 
                        {
                            type = "function",
                            function = new 
                            {
                                name = "send_code_to_revit",
                                description = "Send C# code to Revit for execution",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        code = new { type = "string" },
                                        parameters = new
                                        {
                                            type = "array"
                                        }
                                    },
                                    required = new[] { "code" }
                                }
                            }
                        },
                        new 
                        {
                            type = "function",
                            function = new 
                            {
                                name = "tag_all_walls",
                                description = "Create tags for all walls in the current active view",
                                parameters = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        useLeader = new { type = "boolean" },
                                        tagTypeId = new { type = "string" }
                                    }
                                }
                            }
                        }
                    };

                    Logger.Log("Инициализация HTTP клиента");
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    client.DefaultRequestHeaders.Add("Accept", "text/event-stream");

                    var requestBody = new
                    {
                        model = MODEL,
                        messages = messages,
                        max_tokens = 5000,
                        tools = tools,
                        stream = true
                    };

                    Logger.Log("Формирование тела запроса");
                    var jsonContent = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    Logger.Log("Отправка запроса на API");
                    using (var response = await client.PostAsync(apiUrl, content, CancellationToken.None))
                    {
                        Logger.Log($"Получен ответ: {response.StatusCode}");
                        if (!response.IsSuccessStatusCode)
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            Logger.Log($"Ошибка API: {response.StatusCode}, Content: {errorContent}");
                            return;
                        }

                        Logger.Log("Чтение потока ответа");
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var reader = new StreamReader(stream))
                        {
                            while (!reader.EndOfStream)
                            {
                                var line = await reader.ReadLineAsync();
                                Logger.Log($"Получена строка: {line}");

                                if (string.IsNullOrEmpty(line) || !line.AsSpan().StartsWith("data:".AsSpan()))
                                    continue;

                                var json = line["data:".Length..].Trim();
                                if (json == "[DONE]")
                                {
                                    Logger.Log("Получен маркер завершения [DONE]");
                                    return;
                                }

                                try
                                {
                                    Logger.Log($"Обработка JSON: {json}");
                                    var responseObject = JsonConvert.DeserializeObject<dynamic>(json);
                                var delta = responseObject?.choices?[0]?.delta;
                                
                                // Обработка текстового контента
                                var textContent = delta?.content?.ToString();
                                if (!string.IsNullOrEmpty(textContent))
                                {
                                    Logger.Log($"Получен текстовый chunk: {textContent}");
                                    await onChunkReceived(textContent);
                                }

                                // Обработка вызовов функций
                                var toolCalls = delta?.tool_calls;
                                if (toolCalls != null)
                                {
                                    foreach (var toolCall in toolCalls)
                                    {
                                        var toolName = toolCall.function?.name?.ToString();
                                        var arguments = toolCall.function?.arguments?.ToString();
                                        
                                        if (!string.IsNullOrEmpty(toolName))
                                        {
                                            Logger.Log($"Получен вызов функции: {toolName}");
                                            
                                            var parameters = !string.IsNullOrEmpty(arguments) 
                                                ? JsonConvert.DeserializeObject<Dictionary<string, object>>(arguments)
                                                : new Dictionary<string, object>();

                                            var mcpResponse = await ProcessMcpCall(new McpToolCall
                                            {
                                                Tool = toolName,
                                                Parameters = parameters
                                            });

                                            // Формируем и отправляем результат выполнения функции
                                            var resultText = $"\n[Выполнено: {toolName}]\n";
                                            if (mcpResponse.Status == ResponseStatus.Success && !string.IsNullOrEmpty(mcpResponse.Message))
                                            {
                                                resultText += $"Результат: {mcpResponse.Message}\n";
                                            }
                                            else if (mcpResponse.Status == ResponseStatus.Error)
                                            {
                                                resultText += $"Ошибка: {mcpResponse.Message}\n";
                                            }
                                            
                                            await onChunkReceived(resultText);
                                        }
                                    }
                                }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log($"Ошибка при обработке chunk: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка в SendToAIStream: {ex.Message}");
                throw;
            }
        }

        private async Task<AIResponse> ProcessAIResponse(string aiResponse)
        {
                try
                {
                var response = new AIResponse();
                var resultBuilder = new StringBuilder();
                resultBuilder.AppendLine("Выполняю запрошенные действия:");
                resultBuilder.AppendLine();

                var mcpCalls = ExtractMcpCalls(aiResponse);
                
                if (mcpCalls.Any())
                {
                    foreach (var mcpCall in mcpCalls)
                    {
                        resultBuilder.AppendLine($"Выполняю команду: {mcpCall.Tool}");
                        var mcpResponse = new McpResponseItem 
                        { 
                            Tool = mcpCall.Tool,
                            Status = ResponseStatus.InProgress 
                        };
                        response.McpResponses.Add(mcpResponse);

                        //try 
                        //{
                            var result = await ExecuteMcpTool(mcpCall.Tool, mcpCall.Parameters);
                            mcpResponse.Result = result;

                            if (result?.Content != null)
                            {
                                var statusContent = result.Content.FirstOrDefault(c => c.Type.ToLower() == "status");
                                var messageContent = result.Content.FirstOrDefault(c => c.Type.ToLower() == "text" || c.Type.ToLower() == "error");

                                mcpResponse.Status = statusContent?.Text?.ToLower() == "error" ? ResponseStatus.Error :
                                                   statusContent?.Text?.ToLower() == "warning" ? ResponseStatus.Warning :
                                                   ResponseStatus.Success;

                                mcpResponse.Message = messageContent?.Text;

                                switch (mcpResponse.Status)
                                {
                                    case ResponseStatus.Success:
                                        resultBuilder.AppendLine($"Успешно: {mcpResponse.Message}");
                                        break;
                                    case ResponseStatus.Warning:
                                        resultBuilder.AppendLine($"Предупреждение: {mcpResponse.Message}");
                                        break;
                                    case ResponseStatus.Error:
                                        resultBuilder.AppendLine($"Ошибка: {mcpResponse.Message}");
                                        break;
                                }
                            }
                        //}
                        //catch (Exception ex)
                        //{
                        //    mcpResponse.Status = ResponseStatus.Error;
                        //    mcpResponse.Message = ex.Message;
                        //    resultBuilder.AppendLine($"Ошибка: {ex.Message}");
                        //}
                        
                        resultBuilder.AppendLine();
                    }

                    response.Answer = resultBuilder.ToString().Trim();
                    return response;
                }
                //else if (aiResponse.Contains("```cs") || aiResponse.Contains("```csharp"))
                //{
                //    var codeMatch = Regex.Match(aiResponse, @"```(?:cs|csharp)\s*([\s\S]*?)\s*```");
                //    if (codeMatch.Success)
                //    {
                //        resultBuilder.AppendLine("Выполняю C# код:");
                //        var parameters = new Dictionary<string, object?>
                //        {
                //            ["code"] = codeMatch.Groups[1].Value.Trim()
                //        };

                //        var mcpResponse = new McpResponseItem 
                //        { 
                //            Tool = "mcp_revit_mcp_send_code_to_revit",
                //            Status = ResponseStatus.InProgress 
                //        };
                //        response.McpResponses.Add(mcpResponse);

                //        try
                //        {
                //            var result = await mcpClient.ExecuteToolAsync("mcp_revit_mcp_send_code_to_revit", parameters);
                //            mcpResponse.Result = result;

                //            if (result?.Content != null)
                //            {
                //                var statusContent = result.Content.FirstOrDefault(c => c.Type.ToLower() == "status");
                //                var messageContent = result.Content.FirstOrDefault(c => c.Type.ToLower() == "text" || c.Type.ToLower() == "error");

                //                mcpResponse.Status = statusContent?.Text?.ToLower() == "error" ? ResponseStatus.Error :
                //                                   statusContent?.Text?.ToLower() == "warning" ? ResponseStatus.Warning :
                //                                   ResponseStatus.Success;

                //                mcpResponse.Message = messageContent?.Text;

                //                switch (mcpResponse.Status)
                //                {
                //                    case ResponseStatus.Success:
                //                        resultBuilder.AppendLine($"Успешно: {mcpResponse.Message}");
                //                        break;
                //                    case ResponseStatus.Warning:
                //                        resultBuilder.AppendLine($"Предупреждение: {mcpResponse.Message}");
                //                        break;
                //                    case ResponseStatus.Error:
                //                        resultBuilder.AppendLine($"Ошибка: {mcpResponse.Message}");
                //                        break;
                //                }
                //            }
                //        }
                //        catch (Exception ex)
                //        {
                //            mcpResponse.Status = ResponseStatus.Error;
                //            mcpResponse.Message = ex.Message;
                //            resultBuilder.AppendLine($"Ошибка: {ex.Message}");
                //        }
                //    }

                //    response.Answer = resultBuilder.ToString().Trim();
                //    return response;
                //}
                else
                {
                    response.Answer = aiResponse;
                    return response;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка при выполнении MCP команды: {ex.Message}");
                return new AIResponse
                {
                    Answer = "",
                    ErrorMessage = $"Ошибка при выполнении команды: {ex.Message}"
                };
            }
        }

        private class McpToolCall
        {
            public string? Tool { get; set; }
            public Dictionary<string, object?>? Parameters { get; set; }
        }

        private List<McpToolCall> ExtractMcpCalls(string text)
        {
            var calls = new List<McpToolCall>();
            
            var pattern = @"[\w_]+\s*\((.*?)\)";
            var matches = Regex.Matches(text, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                try
                {
                    var toolName = match.Value.Split('(')[0].Trim();
                    var paramsJson = match.Groups[1].Value;
                    var parameters = JsonConvert.DeserializeObject<Dictionary<string, object?>>(paramsJson);
                    
                    calls.Add(new McpToolCall 
                    { 
                        Tool = toolName,
                        Parameters = parameters
                    });
                }
                catch (Exception ex)
                {
                    Logger.Log($"Ошибка при обработке chunk: {ex.Message}");
                }
            }

            return calls;
        }

        private async Task<CallToolResponse> ExecuteMcpTool(string toolName, Dictionary<string, object?> parameters)
        {
            try
            {

                await mcpClient.InitializeAsync();

                // Создаем копию Dictionary для передачи в метод
                var paramsCopy = new Dictionary<string, object?>(parameters);
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return await mcpClient.Client.CallToolAsync(
                    toolName, 
                    paramsCopy, 
                    null, // progress
                    null, // serializerOptions
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка при выполнении MCP команды: {ex.Message}");
                return new CallToolResponse
                {
                    Content = new List<Content> {new Content {Type = "text", Text = ex.Message } },
                    IsError = true
                };
            }
        }
        public async Task<List<string>> GetAvailableTools()
        {
            try
            {
                var tools = await mcpClient.GetAvailableToolsAsync();
            return tools.Select(t => t.Name).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка при получении списка инструментов: {ex.Message}");
                return new List<string>();
            }
        }
    }
}
