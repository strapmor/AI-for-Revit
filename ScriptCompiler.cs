using System;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Reflection;
using System.IO;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyPlugin
{
    internal class ScriptCompiler : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Место сборки проекта
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;

            // Путь к скрипту
            string scriptPath = Path.GetDirectoryName(assemblyLocation) + @"\AICommand.cs";

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show("Файл скрипта не найден.", "Ошибка!");
                return Result.Failed;
            }

            // Создаём провайдер для компиляции C# кода
            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();

            // Добавляем ссылки на библиотеки
            parameters.ReferencedAssemblies.Add("RevitAPI.dll");
            parameters.ReferencedAssemblies.Add("RevitAPIUI.dll");
            parameters.ReferencedAssemblies.Add("System.Core.dll");

            // Указываем, что нужно сгенерировать сборку в памяти
            parameters.GenerateInMemory = true;

            // Компилируем скрипт
            CompilerResults results = provider.CompileAssemblyFromFile(parameters, scriptPath);


            // Проверяем, есть ли ошибки компиляции
            if (results.Errors.HasErrors)
            {
                string errors = "";
                foreach (CompilerError error in results.Errors)
                {
                    errors += $"Ошибка компиляции: {error.ErrorText}\n";
                }

                MessageBox.Show(errors, "Ошибки компиляции!");
                return Result.Failed;
            }
            else
            {
                try
                {
                    // Получаем скомпилированную сборку
                    Assembly assembly = results.CompiledAssembly;

                    // Получаем тип из сборки
                    Type type = assembly.GetType("AICommand");

                    // Создаем экземпляр этого типа
                    object instance = Activator.CreateInstance(type);

                    // Добавляем метод Execute
                    MethodInfo method = type.GetMethod("Execute");

                    // Вызываем метод Execute
                    Result result = (Result)method.Invoke(instance, new object[] { commandData, message, elements });
                    if(result == Result.Failed)
                    {
                        MessageBox.Show("Ошибка выполнения сгенерированного скрипта", "Ошибка");
                        return Result.Failed;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка!");
                    throw ex;
                    //return Result.Failed;
                }
            }

            return Result.Succeeded;
        }
    }
}
