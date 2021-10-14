using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FanWan
{
    [Transaction(TransactionMode.Manual)]
    public class Class1 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            //预设集合，后续用于关键删除
            List<ElementId> elementsToDel = new List<ElementId>();
            //选择风管
            Reference rfDuct = uidoc.Selection.PickObject(ObjectType.Element, "选择风管");
            //获取风管及其定位线
            Duct duct = doc.GetElement(rfDuct) as Duct;
            Line ductLine = (duct.Location as LocationCurve).Curve as Line;
            //收集所有管道
            FilteredElementCollector pipeCol = new FilteredElementCollector(doc);
            pipeCol.OfClass(typeof(Pipe));
            //获取与风管相交的管线
            ElementIntersectsElementFilter filter = new ElementIntersectsElementFilter(duct);
            IList<Element> intersectPipes = pipeCol.WherePasses(filter).ToElements();
            //新建并启动事务
            Transaction transaction = new Transaction(doc, "管线翻弯避让");
            transaction.Start();
            foreach (Element element in intersectPipes)
            {
                Pipe pipe = element as Pipe;
                List<Pipe> newPipes = new List<Pipe>();
                //获取管道的尺寸
                double d = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                //获取管道的系统类型
                ElementId sTypeId = pipe.MEPSystem.GetTypeId();
                PipingSystemType systype = doc.GetElement(sTypeId) as PipingSystemType;
                //获取管道类型
                PipeType pType = doc.GetElement(pipe.GetTypeId()) as PipeType;
                ElementId pTypeId = pType.Id;
                //获取管道标高
                ElementId levelid = pipe.ReferenceLevel.Id;
                //获得管道定位线
                Line pipeLine = (pipe.Location as LocationCurve).Curve as Line;
                XYZ pStart = pipeLine.GetEndPoint(0);
                XYZ pEnd = pipeLine.GetEndPoint(1);
                //获得管道起终点的连接件
                Connector conStart = ConnectorAtPoint(pipe, pStart);
                Connector conEnd = ConnectorAtPoint(pipe, pEnd);
                //获取与管道起终点相连的关键的连接件（可能为null)
                Connector fittingConStart = GetConToConctor(conStart);
                Connector fittingConEnd = GetConToConctor(conEnd);
                //线取消原管道两端的连接，以便新管道与之连接
                if (fittingConStart != null)
                {
                    conStart.DisconnectFrom(fittingConStart);
                }
                if (fittingConEnd != null)
                {
                    conEnd.DisconnectFrom(fittingConEnd);
                }
                //将原管道存入待删除集合
                elementsToDel.Add(pipe.Id);
                //关键步骤一：获得风管和管线交点
                XYZ intersectPoint = GetIntersectPoint(pipeLine, ductLine);
                if (intersectPoint == null)
                    continue;
                //关键步骤二：获得翻弯后6个管道控制点，一次存入集合 
                int angle = 60;
                List<XYZ> points = GetPipePoints(pipeLine, duct, d, intersectPoint, angle);

                //关键步骤三：依次生成5个新管道
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Pipe newPipe = Pipe.Create(doc, sTypeId, pTypeId, levelid, points[i], points[i + 1]);
                    //设置新管道尺寸
                    newPipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(d);
                    //把新管道添加到集合中
                    newPipes.Add(newPipe);
                }
                //关键步骤四：连接新生成的管道
                CreateConnector(doc, points, newPipes);
                //关键步骤五：恢复原两端连接
                //获得新管道线在原两端点出的Connector
                Connector newConStart = ConnectorAtPoint(newPipes.First(), pStart);
                Connector newConEnd = ConnectorAtPoint(newPipes.Last(), pEnd);
                //恢复与原关键的连接
                if (fittingConStart != null)
                    newConStart.ConnectTo(fittingConStart);
                if (fittingConEnd != null)
                    newConEnd.ConnectTo(fittingConEnd);
            }
            //删除原管道
            doc.Delete(elementsToDel);
            //提交事务
            transaction.Commit();
            return Result.Succeeded;
        }

        public Connector ConnectorAtPoint(Element e, XYZ point)
        {
            ConnectorSet connectorSet = null;
            //风管连接件集合
            if (e is Duct)
                connectorSet = (e as Duct).ConnectorManager.Connectors;
            //管线连接件集合
            if (e is Pipe)
                connectorSet = (e as Pipe).ConnectorManager.Connectors;
            //桥架的连接件集合
            if (e is CableTray)
                connectorSet = (e as CableTray).ConnectorManager.Connectors;
            //管件等可载入族的连接件集合
            if (e is FamilyInstance)
            {
                FamilyInstance fi = e as FamilyInstance;
                connectorSet = fi.MEPModel.ConnectorManager.Connectors;
            }
            //遍历连接件集合
            foreach (Connector connector in connectorSet)
            {
                //如果连接件的中心和目标点相距很小时视为目标连接件
                if (connector.Origin.DistanceTo(point) < 1 / 304.8)
                    //返回该连接件
                    return connector;
            }
            //如果没有匹配到，则返回null
            return null;
        }
        //获取与管线相连的管件连接件
        public Connector GetConToConctor(Connector connector)
        {
            foreach (Connector con in connector.AllRefs)
            {
                //仅选择管件
                if (con.Owner is FamilyInstance)
                {
                    return con;
                }
            }
            return null;
        }
        /// <summary>
        /// 关键步骤一：获取风管与管道的交点
        /// </summary>
        /// <param name="pLine">管道定位线</param>
        /// <param name="dLine">风管定位线</param>
        /// <returns></returns>
        private XYZ GetIntersectPoint(Line pLine, Line dLine)
        {
            XYZ intersectPoint = null;
            //获取风管和管线的定位线的端点
            XYZ ductStart = dLine.GetEndPoint(0);
            XYZ ductEnd = dLine.GetEndPoint(1);
            XYZ pipeStart = pLine.GetEndPoint(0);
            //把风管定位线投影到与管线定位线统一平面上
            ductStart = new XYZ(ductStart.X, ductStart.Y, pipeStart.Z);
            ductEnd = new XYZ(ductEnd.X, ductEnd.Y, pipeStart.Z);
            dLine = Line.CreateBound(ductStart, ductEnd);
            //找到交点
            IntersectionResultArray intersectionResultArray = new IntersectionResultArray();
            SetComparisonResult result = dLine.Intersect(pLine, out intersectionResultArray);
            if (result != SetComparisonResult.Disjoint)
            {
                intersectPoint = intersectionResultArray.get_Item(0).XYZPoint;
            }
            //返回交点
            return intersectPoint;
        }
        //关键步骤2：获得翻弯后管道控制点

        /// <summary>
        /// 计算点的坐标
        /// </summary>
        /// <param name="line"></param>
        /// <param name="duct"></param>
        /// <param name="d"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        private List<XYZ> GetPipePoints(Line line, Duct duct, double d, XYZ point,int angle)
        {
            //记录控制点集
            List<XYZ> pipePoints = new List<XYZ>();
            //收集风管的宽度和高度，案例只考虑矩形风管，为考虑保温层
            double dh, dw;
            dw = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).AsDouble();
            dh = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).AsDouble();
            //得到管线的方向
            XYZ pipeDir = line.Direction;

            //得到管线端点在水平方向上移动的距离
            double horDou = dw / 2 + d + 100 / 304.8;
            //得到管线端点在竖直方向上移动的距离
            double verDou = dh / 2 + d + 100 / 304.8;

            double length = 1 / Math.Tan(angle * Math.PI / 180) * verDou;
            XYZ origin = Transform.CreateTranslation(pipeDir).Origin;

            //得到新平行管线的定位线的定位点
            XYZ p1 = line.GetEndPoint(0);
            XYZ p2 = point.Add(-pipeDir * horDou);
            //XYZ p3 = GetBentPoint(pipeDir,p2,60, verDou);
            XYZ p3 = new XYZ(p2.X + origin.X * length, p2.Y + origin.Y * length, p2.Z + verDou);
            XYZ p6 = line.GetEndPoint(1);
            XYZ p5 = point.Add(pipeDir * horDou);
            //XYZ p4 = GetBentPoint(-pipeDir, p5, 60, verDou);
            XYZ p4 = new XYZ(p5.X - origin.X * length, p5.Y - origin.Y * length, p5.Z + verDou);
            //添加到集合中
            pipePoints.Add(p1);
            pipePoints.Add(p2);
            pipePoints.Add(p3);
            pipePoints.Add(p4);
            pipePoints.Add(p5);
            pipePoints.Add(p6);
            return pipePoints;
        }
        //连接管线
        private void CreateConnector(Document doc, List<XYZ> points, List<Pipe> pipes)
        {
            for (int i = 0; i < pipes.Count - 1; i++)
            {
                //获得匹配的连接件
                Connector con1 = ConnectorAtPoint(pipes[i], points[i + 1]);
                Connector con2 = ConnectorAtPoint(pipes[i + 1], points[i + 1]);

                //创建弯头
                doc.Create.NewElbowFitting(con1, con2);
            }
        }

        ///// <summary>
        ///// 按照角度计算点的坐标
        ///// </summary>
        ///// <param name="direction">单位方向向量</param>
        ///// <param name="point"></param>
        ///// <param name="angle"></param>
        ///// <param name="heigth"></param>
        ///// <returns></returns>
        //public static XYZ GetBentPoint(XYZ direction, XYZ point, int angle, double heigth)
        //{
        //    //生成以所选管线方向为向量方向的平移变换
        //    //Transform transform = Transform.CreateTranslation(dir);
        //    double length = 1 / Math.Tan(angle * Math.PI / 180) * heigth;
        //    XYZ origin = Transform.CreateTranslation(direction).Origin;
        //    XYZ result = new XYZ(point.X + origin.X * length, point.Y + origin.Y * length, point.Z + heigth);
        //    return result;
        //}


    }
}

