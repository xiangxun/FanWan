using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.Attributes;


namespace PipelineAvoidance
{/// <summary>
 /// 这个类会定义两个Connector类型的属性来保存两个相近的，可创建弯头的Connector
 /// </summary>
    public class MyConnector
    {
        private Connector _first;
        private Connector _second;

        public Connector First { get => _first; set => _first = value; }
        public Connector Second { get => _second; set => _second = value; }
        public MyConnector(Connector c1, Connector c2)
        {
            this.First = c1;
            this.Second = c2;
        }
    }
}

