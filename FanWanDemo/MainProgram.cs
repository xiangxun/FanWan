using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FanWanDemo
{
    [Transaction(TransactionMode.Manual)]
    public class MainProgram : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            //mainWindow.ShowDialog();
            return Result.Succeeded;
        }
    }
}
