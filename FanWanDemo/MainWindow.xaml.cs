using Autodesk.Revit.UI;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FanWanDemo
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        //注册外部事件
        FanWan1 fanWanCommand1 = null;
        ExternalEvent fanWanEvent1 = null;

        FanWan2 fanWanCommand2 = null;
        ExternalEvent fanWanEvent2 = null;

        public MainWindow()
        {
            InitializeComponent();
            //fanWanCommand = new FanWan1();
            //fanWanEvent = ExternalEvent.Create(fanWanCommand);

            fanWanCommand2 = new FanWan2();
            fanWanEvent2 = ExternalEvent.Create(fanWanCommand2);

            List<string> list = new List<string>() { "一点", "两点" };
            cbBlist.ItemsSource = list;

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            fanWanCommand2.offsetValue = Convert.ToDouble(this.textBox.Text);
            fanWanEvent2.Raise();

            //if ((cbBlist.ItemsSource).ToString() == "两点")
            //{
            //    fanWanCommand2.offsetValue = Convert.ToDouble(this.textBox.Text);
            //    fanWanEvent2.Raise();
            //}
            //else
            //{
            //    fanWanCommand1.offsetValue = Convert.ToDouble(this.textBox.Text);
            //    fanWanEvent1.Raise();
            //}
            //this.Close();
        }
        //private void mainWindow_LostFocus(object sender, RoutedEventArgs e)
        //{
        //    fanWanCommand.offsetValue = Convert.ToDouble(this.textBox.Text);
        //    fanWanEvent.Raise();
        //}
    }
}
