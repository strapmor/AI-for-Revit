using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Controls;

namespace MyPlugin
{
    public partial class MyPluginWindow : Window
    {
        private readonly ExternalCommandData _commandData;
        private string _message;
        private readonly ElementSet _elements;
        private readonly AIService _aiService;

        public MyPluginWindow(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            this._commandData = commandData;
            this._message = message;
            this._elements = elements;
            this._aiService = new AIService();

            InitializeComponent();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string userInput = InputTextBox.Text.Trim();
            InputTextBox.Clear();

            if (string.IsNullOrWhiteSpace(userInput) || userInput == "Введите запрос")
            {
                TaskDialog.Show("Ошибка", "Введите сообщение перед отправкой!");
                return;
            }

            // Отключаем кнопку отправки и показываем индикатор загрузки
            SendButton.IsEnabled = false;
            Separator.Visibility = System.Windows.Visibility.Collapsed;
            LoadingProgressBar.Visibility = System.Windows.Visibility.Visible;

            OutputTextBox.Text = "Ожидание ответа...";
            AIResponse response = await _aiService.SendToChatGPT(userInput);

            // Включаем кнопку отправки и скрываем индикатор загрузки
            SendButton.IsEnabled = true;
            Separator.Visibility = System.Windows.Visibility.Visible;
            LoadingProgressBar.Visibility = System.Windows.Visibility.Collapsed;

            response.Answer = response.Answer.Replace("```csharp", String.Empty).Replace("```", String.Empty).Trim();

            OutputTextBox.Text = response.Answer;

            if (response.ErrorMessage != null)
            {
                MessageBox.Show(response.ErrorMessage, "Ошибка");
                return;
            }
            if (response.Answer == "Ошибка: пустой ответ.")
            {
                MessageBox.Show("Пустой ответ.", "Ошибка");
                return;
            }

            Logger.SaveLog(userInput, response);

            SaveScript(response.Answer);
            ExecuteScript();
            OutputTextBox.ScrollToEnd();
        }

        private void SaveScript(string scriptContent)
        {
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "AICommand.cs");
                File.WriteAllText(path, scriptContent);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Ошибка при сохранении скрипта: {ex.Message}");
            }
        }

        private void ExecuteScript()
        {
            ScriptCompiler compiler = new ScriptCompiler();
            Result result = compiler.Execute(_commandData, ref _message, _elements);

            OutputTextBox.Text += result == Result.Failed ? "\nОшибка при запуске скрипта." : "\nСкрипт успешно выполнен!";
        }

        private void InputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (InputTextBox.Text == "Введите запрос")
            {
                InputTextBox.Text = "";
                InputTextBox.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void InputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                InputTextBox.Text = "Введите запрос";
                InputTextBox.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void LogButton_Click(object sender, RoutedEventArgs e) => Logger.OpenLog();
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => this.DragMove();
    }
}
