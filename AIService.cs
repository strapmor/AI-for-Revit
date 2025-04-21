﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
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
using System.Text.Json;
using System.IO;

namespace MyPlugin
{
    public class AIService
    {
        private readonly McpClient mcpClient;
        private static readonly string apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_FREE");
        private static readonly string apiUrl = "https://openrouter.ai/api/v1/chat/completions";
        private static readonly string MODEL = "deepseek/deepseek-chat:free"; 
        private List<Dictionary<string, string>> conversationHistory;

        public AIService()
        {
            mcpClient = McpClient.Instance;
            conversationHistory = new List<Dictionary<string, string>>();
            // Добавляем системный промпт
            conversationHistory.Add(new Dictionary<string, string> 
            {
                { "role", "system" },
            { "content", @"Ты помощник для пользователей Autodesk Revit. У тебя есть доступ к следующим инструментам MCP с их параметрами:

1. get_current_view_info() - получить информацию о текущем виде
   Параметры: нет

2. get_current_view_elements() - получить элементы текущего вида
   Параметры: 
   - modelCategoryList: string[] - список категорий элементов
   - annotationCategoryList: string[] - список аннотационных категорий
   - includeHidden: bool - включать скрытые элементы
   - limit: int - максимальное количество элементов

3. get_available_family_types() - получить доступные типы семейств
   Параметры:
   - categoryList: string[] - список категорий
   - familyNameFilter: string - фильтр по имени семейства
   - limit: int - максимальное количество типов

4. get_selected_elements() - получить выбранные элементы
   Параметры:
   - limit: int - максимальное количество элементов

5. create_point_based_element() - создать элемент на основе точки
   Параметры:
   - data: object[] - массив объектов с параметрами:
     * name: string - имя элемента
     * typeId: int - ID типа семейства
     * locationPoint: object - координаты точки {x, y, z}
     * width: double - ширина
     * height: double - высота
     * baseLevel: double - уровень основания
     * baseOffset: double - смещение от уровня
     * rotation: double - угол поворота

6. create_line_based_element() - создать элемент на основе линии
   Параметры:
   - data: object[] - массив объектов с параметрами:
     * name: string - имя элемента
     * typeId: int - ID типа семейства
     * locationLine: object - линия {p0: {x, y, z}, p1: {x, y, z}}
     * thickness: double - толщина
     * height: double - высота
     * baseLevel: double - уровень основания
     * baseOffset: double - смещение от уровня

7. create_surface_based_element() - создать элемент на основе поверхности
   Параметры:
   - data: object[] - массив объектов с параметрами:
     * name: string - имя элемента
     * typeId: int - ID типа семейства
     * boundary: object - граница {outerLoop: [{p0: {x, y, z}, p1: {x, y, z}}]}
     * thickness: double - толщина
     * baseLevel: double - уровень основания
     * baseOffset: double - смещение от уровня

8. delete_element() - удалить элемент
   Параметры:
   - elementIds: string[] - массив ID элементов

9. send_code_to_revit() - отправить C# код в Revit
   Параметры:
   - code: string - код на C#
   - parameters: object[] - параметры для кода

10. color_elements() - раскрасить элементы
    Параметры:
    - categoryName: string - имя категории
    - parameterName: string - имя параметра
    - useGradient: bool - использовать градиент
    - customColors: object[] - массив цветов {r, g, b}

11. tag_all_walls() - создать марки для всех стен
    Параметры:
    - useLeader: bool - использовать выноски
    - tagTypeId: string - ID типа марки

Используй эти инструменты для выполнения задач пользователя. Ты можешь:

1. get_current_view_info() - получить информацию о текущем виде
2. get_current_view_elements() - получить элементы текущего вида
3. get_available_family_types() - получить доступные типы семейств
4. get_selected_elements() - получить выбранные элементы
5. create_point_based_element() - создать элемент на основе точки
6. create_line_based_element() - создать элемент на основе линии
7. create_surface_based_element() - создать элемент на основе поверхности
8. delete_element() - удалить элемент
9. send_code_to_revit() - отправить C# код в Revit
10. color_elements() - раскрасить элементы
11. tag_all_walls() - создать марки для всех стен

Используй эти инструменты для выполнения задач пользователя. Ты можешь:
1. Получать информацию о модели через get_* инструменты
2. Создавать новые элементы через create_* инструменты
3. Удалять элементы через delete_element
4. Выполнять C# код через send_code_to_revit
5. Комбинировать несколько инструментов для сложных задач

Когда используешь инструмент, указывай его параметры в формате JSON. Например:
get_current_view_elements({""modelCategoryList"": [""OST_Walls""], ""includeHidden"": false})

Если нужно написать C# код, используй send_code_to_revit и убедись, что код:
1. Содержит все необходимые using директивы
2. Правильно работает с транзакциями
3. Обрабатывает ошибки
4. Возвращает результат через Result.Succeeded/Failed" }
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
                // Добавляем запрос пользователя в историю
                conversationHistory.Add(new Dictionary<string, string> 
                { 
                    { "role", "user" }, 
                    { "content", userPrompt } 
                });

                var response = new AIResponse();
                var fullResponse = new StringBuilder();
                var mcpBuffer = new StringBuilder();

                await SendToAIStream(conversationHistory, async chunk => 
                {
                    fullResponse.Append(chunk);
                    mcpBuffer.Append(chunk);

                    // Проверяем, есть ли в буфере вызов MCP инструмента
                    var mcpCall = ExtractMcpCallFromBuffer(mcpBuffer);
                    if (mcpCall != null)
                    {
                        // Обрабатываем MCP инструмент
                        var mcpResponse = await ProcessMcpCall(mcpCall);
                        response.McpResponses.Add(mcpResponse);
                        mcpBuffer.Clear();
                    }

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

        private McpToolCall? ExtractMcpCallFromBuffer(StringBuilder buffer)
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
                    Logger.Log("Инициализация HTTP клиента");
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    client.DefaultRequestHeaders.Add("Accept", "text/event-stream");

                    var requestBody = new
                    {
                        model = MODEL,
                        messages = messages,
                        max_tokens = 5000,
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
                                    var chunk = responseObject?.choices?[0]?.delta?.content?.ToString();
                                    
                                    if (!string.IsNullOrEmpty(chunk))
                                    {
                                        Logger.Log($"Получен chunk: {chunk}");
                                        await onChunkReceived(chunk);
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

                        try 
                        {
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
                        }
                        catch (Exception ex)
                        {
                            mcpResponse.Status = ResponseStatus.Error;
                            mcpResponse.Message = ex.Message;
                            resultBuilder.AppendLine($"Ошибка: {ex.Message}");
                        }
                        
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
            public string Tool { get; set; }
            public Dictionary<string, object?> Parameters { get; set; }
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
                    Logger.Log($"Ошибка при разборе вызова MCP инструмента: {ex.Message}");
                }
            }

            return calls;
        }

        private async Task<CallToolResponse> ExecuteMcpTool(string toolName, Dictionary<string, object?> parameters)
        {
            try
            {
                // Создаем копию Dictionary для передачи в метод
                var paramsCopy = new Dictionary<string, object?>(parameters);
                
                return await mcpClient.Client.CallToolAsync(
                    toolName, 
                    paramsCopy, 
                    null, // progress
                    null, // serializerOptions
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка при выполнении MCP инструмента {toolName}: {ex.Message}");
                throw;
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
