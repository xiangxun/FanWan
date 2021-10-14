using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Plumbing;

namespace FanWanDemo
{

    public class FanWan1 : IExternalEventHandler
    {
        public double offsetValue { get; set; }

        public void Execute(UIApplication app)
        {
            //UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            UIDocument uiDoc = app.ActiveUIDocument;
            Document doc = uiDoc.Document;


            //交互选择第一个点
            Reference reference = uiDoc.Selection.PickObject(ObjectType.PointOnElement, new Pipefilter());
            XYZ point1 = reference.GlobalPoint;


            //交互选择第二个点
            Reference reference2 = uiDoc.Selection.PickObject(ObjectType.PointOnElement, new Pipefilter());
            XYZ point2 = reference2.GlobalPoint;

            //获取管道
            Pipe pipe = doc.GetElement(reference) as Pipe;

            //获取管道定位线
            LocationCurve locationcurve = pipe.Location as LocationCurve;

            //获取管道定位起点终点
            XYZ start = locationcurve.Curve.GetEndPoint(0);
            XYZ end = locationcurve.Curve.GetEndPoint(1);

            //上面的点是管道表面的点，需要投影到管道中心定位线上
            XYZ propoint1 = locationcurve.Curve.Project(point1).XYZPoint;
            XYZ propoint2 = locationcurve.Curve.Project(point2).XYZPoint;


            //创建一个列表来存储所有的线
            double offset = offsetValue / 304.8;
            List<Line> lines = creatlines(start, end, propoint1, propoint2, offset);

            //创建一个管道列表来存储生成的所有管道
            List<Pipe> pipes = new List<Pipe>();

            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("创建管道");


                //通过遍历生成的所有定位线来创建管道
                foreach (Line line in lines)
                {
                    Pipe newpipe = doc.GetElement(ElementTransformUtils.CopyElement(doc, pipe.Id, new XYZ(0, 0, 0)).ElementAt(0)) as Pipe;
                    LocationCurve newpipelocationcurve = newpipe.Location as LocationCurve;
                    newpipelocationcurve.Curve = line;
                    pipes.Add(newpipe);
                }

                doc.Delete(pipe.Id);

                transaction.Commit();

            }

            //获取所有管道上所有的连接器
            List<Connector> connectors = new List<Connector>();
            foreach (Pipe pipe1 in pipes)
            {
                ConnectorSet connects = pipe1.ConnectorManager.Connectors;
                foreach (Connector connector in connects)
                {
                    connectors.Add(connector);
                }
            }


            //遍历连接器列表，找到同一位置处不同管道上的连接器，进而创建弯头
            for (int i = connectors.Count - 1; i > 0; i--)
            {
                Connector con = connectors[i];
                Connector co = nearconnector(con, connectors);

                if (co != null)
                {
                    using (Transaction transaction2 = new Transaction(doc))
                    {
                        transaction2.Start("创建弯头");
                        doc.Create.NewElbowFitting(con, co);
                        transaction2.Commit();
                    }
                }
            }

            
        }


        /// <summary>
        /// 该方法传入翻弯时产生的所有管道节点，返回翻弯生成的所有管道定位线
        /// </summary>
        /// <param name="管道起点start"></param>
        /// <param name="管道终点end"></param>
        /// <param name="交互选择的第一点propoint1"></param>
        /// <param name="交互选择的第二点propoint2"></param>
        /// <param name="偏移值（翻弯高度）offsetvalue"></param>
        /// <returns></returns>
        private List<Line> creatlines(XYZ start, XYZ end, XYZ propoint1, XYZ propoint2, double offsetvalue)
        {
            List<Line> lines = new List<Line>();


            //if (start.DistanceTo(propoint2) == start.DistanceTo(propoint1) + propoint1.DistanceTo(propoint2))
            //{
            //    Line thispipecurve = Line.CreateBound(propoint1, start);
            //    lines.Add(thispipecurve);
            //    Line newpipecurve = Line.CreateBound(propoint2, end);
            //    lines.Add(newpipecurve);
            //}
            //else
            //{
            //    Line thispipecurve = Line.CreateBound(propoint1, end);
            //    lines.Add(thispipecurve);
            //    Line newpipecurve = Line.CreateBound(propoint2, start);
            //    lines.Add(newpipecurve);
            //}


            Line thispipecurve = Line.CreateBound(propoint1, Near(start, end, propoint1, propoint2));
            lines.Add(thispipecurve);
            Line newpipecurve = Line.CreateBound(propoint2, Near(start, end, propoint2, propoint1));
            lines.Add(newpipecurve);


            XYZ propoint1offset = propoint1 + new XYZ(0, 0, offsetvalue);
            XYZ propoint2offset = propoint2 + new XYZ(0, 0, offsetvalue);
            //XYZ propoint1offset = propoint1 + new XYZ(0, offsetvalue * Math.Tan(Math.PI/4) / 304.8, offsetvalue/304.8);
            //XYZ propoint2offset = propoint2 + new XYZ(0, -offsetvalue * Math.Tan(Math.PI / 4) / 304.8, offsetvalue/304.8);

            Line offsetline = Line.CreateBound(propoint1offset, propoint2offset);
            lines.Add(offsetline);

            Line line1 = Line.CreateBound(propoint1, propoint1offset);
            lines.Add(line1);

            Line line2 = Line.CreateBound(propoint2, propoint2offset);
            lines.Add(line2);

            return lines;

        }


        //判断两个交互选择得到的点离管道的两端分别那个最近
        public XYZ Near(XYZ startpoint, XYZ endpoint, XYZ pickedpoint1, XYZ pickedpoint2)
        {
            if (startpoint.DistanceTo(pickedpoint2) == startpoint.DistanceTo(pickedpoint1) + pickedpoint1.DistanceTo(pickedpoint2))
            {
                return startpoint;
            }
            return endpoint;
        }


        //创建一个过滤器，使选点的时候只在管道上选择
        public class Pipefilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if (elem is Pipe)
                {
                    return true;
                }
                return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }


        //遍历弯头，得到相同位置处另一个管道的连接器
        public Connector nearconnector(Connector con, List<Connector> connectors)
        {
            foreach (Connector connector in connectors)
            {
                if (con.Origin.IsAlmostEqualTo(connector.Origin) && con.Owner.Id != connector.Owner.Id)
                {
                    return connector;
                }
            }
            return null;
        }



        public string GetName()
        {
            return "Create";
        }
    }
}
