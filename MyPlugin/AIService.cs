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
