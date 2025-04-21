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
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using Visibility = System.Windows.Visibility;

namespace MyPlugin
{
    public class McpStatusViewModel : INotifyPropertyChanged
    {
        private string tool = string.Empty;
        private bool isInProgress;
        private double progress;
        private string statusText = string.Empty;
        private ResponseStatus status;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Tool
        {
            get => tool;
            set
            {
                tool = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public bool IsInProgress
        {
            get => isInProgress;
            set
            {
                isInProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressVisibility));
            }
        }

        public double Progress
        {
            get => progress;
            set
            {
                progress = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => statusText;
            set
            {
                statusText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public ResponseStatus Status
        {
            get => status;
            set
            {
                status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public Visibility ProgressVisibility => IsInProgress ? Visibility.Visible : Visibility.Collapsed;

        public Brush StatusColor
        {
            get
            {
                return Status switch
                {
                    ResponseStatus.Success => new SolidColorBrush(Colors.Green),
                    ResponseStatus.Warning => new SolidColorBrush(Colors.Orange),
                    ResponseStatus.Error => new SolidColorBrush(Colors.Red),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MyPluginWindow : Window
    {
        public enum MCPConnectionState
        {
            Disconnected,
            Connecting,
            Connected
        }

        private MCPConnectionState _connectionState = MCPConnectionState.Disconnected;
        public MCPConnectionState ConnectionState
        {
            get => _connectionState;
            set
            {
                _connectionState = value;
                UpdateConnectionIndicator();
            }
        }

        private void UpdateConnectionIndicator()
        {
            Dispatcher.Invoke(() =>
            {
                ConnectionIndicator.Fill = _connectionState switch
                {
                    MCPConnectionState.Disconnected => Brushes.Red,
                    MCPConnectionState.Connecting => Brushes.Yellow,
                    MCPConnectionState.Connected => Brushes.Green,
                    _ => Brushes.Gray
                };
            });
        }

        private readonly ExternalCommandData _commandData;
        private string _message;
        private readonly ElementSet _elements;
        private readonly AIService _aiService;
        private readonly ObservableCollection<McpStatusViewModel> _mcpStatuses;

        public MyPluginWindow(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            this._commandData = commandData;
            this._message = message;
            this._elements = elements;
            this._aiService = new AIService();
            this._mcpStatuses = new ObservableCollection<McpStatusViewModel>();
            
            // Инициализация состояния подключения
            _ = InitializeConnectionAsync();

            InitializeComponent();
            McpStatusPanel.ItemsSource = _mcpStatuses;
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
            Separator.Visibility = Visibility.Collapsed;
            LoadingProgressBar.Visibility = Visibility.Visible;
            _mcpStatuses.Clear();
            OutputTextBox.Text = "Ожидание ответа...";

            try
            {
                var response = await _aiService.SendToChatGPT(userInput, async partialResponse => 
                {
                    // Обновляем текст ответа
                    OutputTextBox.Text = partialResponse.Answer
                        .Replace("```csharp", string.Empty)
                        .Replace("```", string.Empty)
                        .Trim();

                    // Обновляем статусы MCP команд
                    foreach (var mcpResponse in partialResponse.McpResponses)
                    {
                        var existingStatus = _mcpStatuses.FirstOrDefault(s => s.Tool == mcpResponse.Tool);
                        if (existingStatus == null)
                        {
                            var statusViewModel = new McpStatusViewModel
                            {
                                Tool = mcpResponse.Tool,
                                Status = mcpResponse.Status,
                                StatusText = mcpResponse.Message,
                                IsInProgress = mcpResponse.Status == ResponseStatus.InProgress,
                                Progress = mcpResponse.Status == ResponseStatus.Success ? 100 : 0
                            };
                            _mcpStatuses.Add(statusViewModel);
                        }
                        else
                        {
                            existingStatus.Status = mcpResponse.Status;
                            existingStatus.StatusText = mcpResponse.Message;
                            existingStatus.IsInProgress = mcpResponse.Status == ResponseStatus.InProgress;
                            existingStatus.Progress = mcpResponse.Status == ResponseStatus.Success ? 100 : 0;
                        }
                    }

                    OutputTextBox.ScrollToEnd();
                    await Task.Delay(50); // Даем время для обновления UI
                });

                // Обработка финального ответа
                OutputTextBox.Text = response.Answer;
                OutputTextBox.ScrollToEnd();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            }
            finally
            {
                // Включаем кнопку отправки и скрываем индикатор загрузки
                SendButton.IsEnabled = true;
                Separator.Visibility = Visibility.Visible;
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void InputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (InputTextBox.Text == "Введите запрос")
            {
                InputTextBox.Text = "";
                InputTextBox.Foreground = Brushes.Black;
            }
        }

        private void InputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                InputTextBox.Text = "Введите запрос";
                InputTextBox.Foreground = Brushes.Gray;
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
        private async Task InitializeConnectionAsync()
        {
            try
            {
                ConnectionState = MCPConnectionState.Connecting;
                await _aiService.Initialize();
                ConnectionState = MCPConnectionState.Connected;
            }
            catch
            {
                ConnectionState = MCPConnectionState.Disconnected;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => this.DragMove();
    }
}
