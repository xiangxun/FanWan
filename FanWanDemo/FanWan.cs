using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FanWanDemo
{
    [Transaction(TransactionMode.Manual)]
    class FanWan : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            application.CreateRibbonTab("翻弯");//new tab
            RibbonPanel rp = application.CreateRibbonPanel("翻弯", "翻弯UIPanel");
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string classNameFanWanDemo = "FanWanDemo.MainProgram";
            PushButtonData pbd = new PushButtonData("InnerNameRevit", "FanWan", assemblyPath, classNameFanWanDemo);
            PushButton pushButton = rp.AddItem(pbd) as PushButton;
            pushButton.LargeImage = new BitmapImage(new Uri("pack://application:,,,/FanWanDemo;component/pic/圣诞节_棒棒糖.png", UriKind.Absolute));
            pushButton.ToolTip = "HelloRevit";
            return Result.Succeeded;


        }
    }
}
