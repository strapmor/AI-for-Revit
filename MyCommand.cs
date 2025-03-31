using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace MyPlugin
{
    [Transaction(TransactionMode.Manual)]
    public class MyCommand : IExternalCommand /*запуск окна из команды*/
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                MyPluginWindow window = new MyPluginWindow(commandData, ref message, elements);
                window.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }
    }
}
