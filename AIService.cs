using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Newtonsoft.Json;

namespace MyPlugin
{
    public class AIService
    {
        private static readonly string apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_FREE");
        private static readonly string apiUrl = "https://openrouter.ai/api/v1/chat/completions";
        private static readonly string MODEL = "deepseek/deepseek-chat:free"; 

        public async Task<AIResponse> SendToChatGPT(string prompt)
        {
            //TaskDialog.Show("Проверка API-ключа", $"API-ключ: {apiKey ?? "НЕ НАЙДЕН"}");
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var requestBody = new
                {
                    model = MODEL,
                    messages = new[]
                    {
                        new { role = "system", content = "Ты помощник для пользователей Autodesk Revit 2021." +
                                "Тебе необходимо написать полностью рабочий c# скрипт, использующий RevitAPI и RevitAPIUI, выполняющий задачу, о которой попросит тебя пользователь." +
                                "Твой ответ не должен содержать ничего более, кроме исходного кода самого скрипта." +
                                "Класс, реализующий интерфейс IExternalCommand обязательно должен называться \"AICommand\", пространства имён быть не должно." +
                                "Скрипт должен содержать Все импорты Библиотек, которые он использует. Если используется System.Linq, то он обязательно должен импортироваться." },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 5000
                    //max_tokens = 100
                };

                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                string responseString;
                double cost = 0;
                //string errorMessage = null;

                try
                {
                    response = await client.PostAsync(apiUrl, content);
                    responseString = await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException)
                {
                    return new AIResponse
                    {
                        Answer = "",
                        Cost = 0,
                        ErrorMessage = "Ошибка соединения с сервером. Проверьте интернет-соединение."
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new AIResponse
                    {
                        Answer = "",
                        Cost = 0,
                        ErrorMessage = $"Ошибка API: {response.StatusCode} \n{responseString}"
                    };
                }

                dynamic responseObject = JsonConvert.DeserializeObject(responseString);
                
                //string reasoning = responseObject?["choices"]?[0]?["message"]?["reasoning"]?.ToString();
                string answer = responseObject?["choices"]?[0]?["message"]?["content"]?.ToString() ?? "Ошибка: пустой ответ.";
                string error = responseObject?["error"]?["message"]?.ToString();

                int promptTokens = responseObject?.usage?.prompt_tokens ?? 0;
                int completionTokens = responseObject?.usage?.completion_tokens ?? 0;
                cost = (promptTokens / 1000.0 * 0.03) + (completionTokens / 1000.0 * 0.06);
                cost = Math.Round(cost, 4);

                return new AIResponse
                {
                    Answer = answer,
                    Cost = cost,
                    ErrorMessage = error
                };
            }
        }
    }
}