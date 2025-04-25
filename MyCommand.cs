using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using System.IO;

namespace MyPlugin
{
    [Transaction(TransactionMode.Manual)]
    public class MyCommand : IExternalCommand /*запуск окна из команды*/
    {
        private static Process _wpfProcess; // Статическая переменная для хранения процесса

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                //MyPluginWindow window = new MyPluginWindow(commandData, ref message, elements);
                //window.Show();

                string currentDllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string currentDir = Path.GetDirectoryName(currentDllPath);

                string appPath = @"..\..\..\..\AI-for-Revit\bin\Debug\net8.0-windows\AI-for-Revit.exe";

                string fullPath = Path.GetFullPath(Path.Combine(currentDir, appPath));



                var processInfo = new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true,  // Запуск через оболочку Windows
                    CreateNoWindow = false   // Показывать окно
                };

                _wpfProcess = Process.Start(processInfo);

                // Подписываемся на событие закрытия Revit (через Idling)
                //commandData.Application.Idling += Application_Idling;

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }
        private void Application_Idling(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            // Проверяем, жив ли процесс
            if (_wpfProcess != null && _wpfProcess.HasExited == false)
            {
                _wpfProcess.Kill(); // Принудительное завершение
                _wpfProcess = null;
            }

            // Отписываемся от события
            var app = (UIApplication)sender;
            app.Idling -= Application_Idling;
        }
    }
}
