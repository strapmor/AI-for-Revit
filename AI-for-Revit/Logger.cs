using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Windows;
using System.Linq;
using System.Collections.Generic;

namespace AI_for_Revit
{
    public class McpLogResult
    {
        public string Tool { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public static class Logger
    {
        private static readonly string logsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyPluginLogs");

        static Logger()
        {
            try
            {
                if (!Directory.Exists(logsDirectory))
                {
                    Directory.CreateDirectory(logsDirectory);
                }
                // Дополнительная проверка на запись
                string testFilePath = Path.Combine(logsDirectory, "test.txt");
                File.WriteAllText(testFilePath, "test");
                File.Delete(testFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации логов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void SaveLog(string userInput, AIResponse response)
        {
            try
            {
                string requestId = Guid.NewGuid().ToString();
                string logFilePath = Path.Combine(logsDirectory, $"{DateTime.Now:yyyy-MM-dd}.txt");
                string jsonLogFilePath = Path.Combine(logsDirectory, $"{DateTime.Now:yyyy-MM-dd}.json");

                var mcpResults = new List<McpLogResult>();
                if (response.McpResponses != null)
                {
                    mcpResults = response.McpResponses.Select(r => new McpLogResult
                    {
                        Tool = r.Tool,
                        Status = r.Status.ToString(),
                        Message = r.Message
                    }).ToList();
                }

                var logEntry = new
                {
                    RequestId = requestId,
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    Question = userInput,
                    Answer = response.Answer,
                    Cost = response.Cost,
                    ErrorMessage = response.ErrorMessage,
                    OverallStatus = response.OverallStatus.ToString(),
                    McpResults = mcpResults
                };

                var logBuilder = new StringBuilder()
                    .AppendLine($"Запрос ID: {requestId}")
                    .AppendLine($"Время: {logEntry.Time}")
                    .AppendLine($"Вопрос: {logEntry.Question}")
                    .AppendLine($"Ответ: {logEntry.Answer}")
                    .AppendLine($"Цена: ${logEntry.Cost:F4}")
                    .AppendLine($"Общий статус: {logEntry.OverallStatus}");

                if (mcpResults.Any())
                {
                    logBuilder.AppendLine("Результаты MCP команд:");
                    foreach (var result in mcpResults)
                    {
                        logBuilder.AppendLine($"  - {result.Tool}: {result.Status} - {result.Message}");
                    }
                }

                if (!string.IsNullOrEmpty(logEntry.ErrorMessage))
                {
                    logBuilder.AppendLine($"Ошибка: {logEntry.ErrorMessage}");
                }

                logBuilder.AppendLine(new string('-', 50));

            string jsonLog = Newtonsoft.Json.JsonConvert.SerializeObject(logEntry, Newtonsoft.Json.Formatting.Indented);

                // Записываем в обычный лог
                File.AppendAllText(logFilePath, logBuilder.ToString(), Encoding.UTF8);

                // Записываем в JSON лог
                File.AppendAllText(jsonLogFilePath, jsonLog + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при записи лога: {ex.Message}", "Ошибка логирования",
                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void Log(string message)
        {
            try
            {
                string logFilePath = Path.Combine(logsDirectory, $"{DateTime.Now:yyyy-MM-dd}_debug.log");
                string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(logFilePath, logEntry, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при записи отладочного лога: {ex.Message}", "Ошибка логирования",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void OpenLog()
        {
            string logFilePath = Path.Combine(logsDirectory, $"{DateTime.Now:yyyy-MM-dd}.txt");

            if (!File.Exists(logFilePath))
            {
                MessageBox.Show("Лог-файл за сегодня отсутствует.", "Журнал",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = logFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии лога: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
