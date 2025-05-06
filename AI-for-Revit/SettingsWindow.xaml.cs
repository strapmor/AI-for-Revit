using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AI_for_Revit
{
    /// <summary>
    /// Логика взаимодействия для SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            APIKeyTextBox.Text = Configuration.Instance.GetApiKey();
            ProviderTextBox.Text = Configuration.Instance.GetProvider();
            ModelTextBox.Text = Configuration.Instance.GetModel();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => this.DragMove();

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
