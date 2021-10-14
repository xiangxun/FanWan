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
{
    [Autodesk.Revit.Attributes.Transaction(TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(RegenerationOption.Manual)]
    [Autodesk.Revit.Attributes.Journaling(JournalingMode.UsingCommandData)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            //窗体
            //MainWindow mw = new MainWindow();
            //mw.ShowDialog();
            double heigth = 500;
            int angle = 60;
            //获取要处理的风管和创建风管所需的所有点
            List<Line> lines = FirstStep(uidoc, angle, heigth, out Pipe pi);
            Pipe pipe = pi;

            //收集新生成的风管，共后面生成弯头
            List<Pipe> ducts = new List<Pipe>();

            //try-catch 框架
            try
            {
                using (Transaction ts = new Transaction(uidoc.Document, "break"))
                {
                    ts.Start();
                    ducts = FinalStep(uidoc, lines, pipe);
                    List<Connector> connectors = GetConnectors(ducts);
                    List<MyConnector> conn = GetUsefulConnectors(connectors);
                    CreateElbow(uidoc, conn);
                    //TaskDialog.Show("number", conn.Count.ToString());
                    uidoc.Document.Delete(pipe.Id);
                    ts.Commit();
                }
                return Result.Succeeded;
            }

            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                return Result.Failed;
            }

        }
        /// <summary>
        /// 创建弯头
        /// </summary>
        /// <param name="uidoc"></param>
        /// <param name="myConnectors"></param>
        public static void CreateElbow(UIDocument uidoc, List<MyConnector> myConnectors)
        {
            foreach (MyConnector mc in myConnectors)
            {
                uidoc.Document.Create.NewElbowFitting(mc.First, mc.Second);
            }
        }

        /// <summary>
        ///过滤可以创建弯头的connector 
        /// </summary>
        /// <param name="connectors"></param>
        /// <returns></returns>
        public static List<MyConnector> GetUsefulConnectors(List<Connector> connectors)
        {
            List<MyConnector> myConnectors = new List<MyConnector>();
            for (int i = 0; i < connectors.Count; i++)
            {
                for (int j = 0; j < connectors.Count; j++)
                {
                    if (connectors[i].Owner.Id != connectors[j].Owner.Id && connectors[i].Origin.IsAlmostEqualTo(connectors[j].Origin))
                    {
                        MyConnector con = new MyConnector(connectors[i], connectors[j]);
                        // connectors.Remove(connectors[i]);
                        connectors.Remove(connectors[j]);
                        myConnectors.Add(con);
                    }
                }
            }
            return myConnectors;
        }
        /// <summary>
        /// 获取新创建的五个风管两端的十个Connector
        /// </summary>
        /// <param name="ducts"></param>
        /// <returns></returns>
        public static List<Connector> GetConnectors(List<Pipe> pipes)
        {
            List<Connector> connectors = new List<Connector>();
            foreach (Pipe pi in pipes)
            {
                ConnectorSet connectorSet = pi.ConnectorManager.Connectors;
                foreach (Connector cn in connectorSet)
                {
                    connectors.Add(cn);
                }
            }
            return connectors;
        }
        /// <summary>
        /// 复制原来的风管，把Locationcurve用新创建的线来代替
        /// </summary>
        /// <param name="uidoc"></param>
        /// <param name="lines"></param>
        /// <param name="duct"></param>
        public static List<Pipe> FinalStep(UIDocument uidoc, List<Line> lines, Pipe pipe)
        {
            List<Pipe> pipes = new List<Pipe>();
            foreach (Line ll in lines)
            {
                ElementId id = ElementTransformUtils.CopyElement(uidoc.Document, pipe.Id, new XYZ(1, 0, 0)).First();
                Pipe tempPipe = uidoc.Document.GetElement(id) as Pipe;
                (tempPipe.Location as LocationCurve).Curve = ll;
                pipes.Add(tempPipe);
            }
            return pipes;
        }

        /// <summary>
        /// 按用户的输入生成五条line
        /// </summary>
        /// <param name="uidoc"></param>
        /// <param name="angle"></param>
        /// <param name="heigth"></param>
        /// <returns></returns>
        public static List<Line> FirstStep(UIDocument uidoc, int angle, double heigth, out Pipe p)
        {
            Selection selection = uidoc.Selection;
            Document doc = uidoc.Document;
            Reference ref1 = selection.PickObject(ObjectType.PointOnElement, new PipeFilter(), "请选择第一个点");
            XYZ pt1 = ref1.GlobalPoint;
            Reference ref2 = selection.PickObject(ObjectType.PointOnElement, new PipeFilter(), "请选择第二个点");
            XYZ pt2 = ref2.GlobalPoint;
            Pipe pipe = doc.GetElement(ref1) as Pipe;
            p = pipe;
            LocationCurve lc = pipe.Location as LocationCurve;

            XYZ A = lc.Curve.GetEndPoint(0);
            XYZ F = lc.Curve.GetEndPoint(1);

            XYZ B = lc.Curve.Project(pt1).XYZPoint;
            XYZ E = lc.Curve.Project(pt2).XYZPoint;

            XYZ C = GetBentPoint((lc.Curve as Line).Direction, B, angle, heigth / 304.8);
            XYZ D = GetBentPoint(-(lc.Curve as Line).Direction, E, angle, heigth / 304.8);

            //XYZ C = GetBentPoint((lc.Curve as Line).Direction, B, angle, heigth / 304.8);
            //XYZ D = GetBentPoint(-(lc.Curve as Line).Direction, E, angle, heigth / 304.8);

            //判断离端点较近的点
            XYZ middle1 = GetNearPoint(A, B, E);
            XYZ middle2 = GetNearPoint(F, B, E);


            Line l1 = Line.CreateBound(A, middle1);
            Line l2 = Line.CreateBound(middle1, C);
            Line l3 = Line.CreateBound(C, D);
            Line l4 = Line.CreateBound(D, middle2);
            Line l5 = Line.CreateBound(middle2, F);
            List<Line> lines = new List<Line>();
            lines.Add(l1);
            lines.Add(l2);
            lines.Add(l3);
            lines.Add(l4);
            lines.Add(l5);

            return lines;
        }

        /// <summary>
        /// GetNearPoint
        /// </summary>

        /// <returns></returns>
        public static XYZ GetNearPoint(XYZ origin, XYZ pt1, XYZ pt2)
        {
            if (origin.DistanceTo(pt1) > origin.DistanceTo(pt2))
            {
                return pt2;
            }
            else
                return pt1;
        }
        /// <summary>
        /// 按照角度计算点的坐标
        /// </summary>
        /// <param name="direction">单位方向向量</param>
        /// <param name="point"></param>
        /// <param name="angle"></param>
        /// <param name="heigth"></param>
        /// <returns></returns>
        public static XYZ GetBentPoint(XYZ direction, XYZ point, int angle, double heigth)
        {
            //生成以所选管线方向为向量方向的平移变换
            //Transform transform = Transform.CreateTranslation(dir);
            double length = 1 / Math.Tan(angle * Math.PI/180) * heigth;
            XYZ origin = Transform.CreateTranslation(direction).Origin;
            XYZ result = new XYZ(point.X + origin.X * length, point.Y + origin.Y * length, point.Z + heigth);
            return result;

            //XYZ res = Transform.CreateTranslation(dir).OfPoint(point);
            //TaskDialog.Show("显示信息", $"p点坐标({point.X.ToString("0.00")},{point.Y.ToString("0.00")},{point.Z.ToString("0.00")})");
            //TaskDialog.Show("显示信息", $"s点坐标({res.X.ToString("0.00")},{res.Y.ToString("0.00")},{res.Z.ToString("0.00")})");
            //TaskDialog.Show("显示信息", $" origin点坐标({ origin.X.ToString("0.00")},{ origin.Y.ToString("0.00")},{ origin.Z.ToString("0.00")})");
            //double resX = point.X + origin.X * length;
            //double resY = point.Y + origin.Y * length;
            //double resX = res.X-origin.X + origin.X*length;
            //double resY = res.Y-origin.Y + origin.Y*length;
            //TaskDialog.Show("显示信息", $" X Y length ({resX.ToString("0.000")},{resY.ToString("0.000")},{length.ToString("0.000")}),{origin.X.ToString("0.00")}");
            //TaskDialog.Show("显示信息", $"r点坐标({result.X.ToString("0.00")},{result.Y.ToString("0.00")},{result.Z.ToString("0.00")})");

        }
    }
    /// <summary>
    /// 过滤管道
    /// </summary>
    public class PipeFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return (elem is Pipe);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}

