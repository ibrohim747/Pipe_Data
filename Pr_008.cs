using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using MEP_Data.Path_Finder;
using Projects.Utils;
using System.Collections.Generic;
using System.Linq;

namespace Projects
{
    [Transaction(TransactionMode.Manual)]
    public class Pr_008 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // ── 1. ГРАНИЦА ВХОДА ────────────────────────────────────────────────────
            List<Pipe> rawPipes = SelectionHelper.PickElements<Pipe>(uidoc, "Выберите ровно 2 трубы");

            // ── 2. Валидация ────────────────────────────────────────────────────────
            if (rawPipes == null)
                return Result.Cancelled;

            if (rawPipes.Count != 2)
            {
                TaskDialog.Show("Ошибка", "Нужно выбрать ровно 2 трубы.");
                return Result.Failed;
            }

            List<PipeData> selected = rawPipes.Select(PipeUtils.ToPipeData).ToList();

            // ── 3. Логика на decimal ────────────────────────────────────────────────
            myPipe_numeric pipe1 = selected[0].Numeric;
            myPipe_numeric pipe2 = selected[1].Numeric;
            myPipe_categoric cat1 = selected[0].Categoric;
            myPipe_categoric cat2 = selected[1].Categoric;

            (Coordinate from, Coordinate to) = PipeUtils.GetClosestConnectors(pipe1, pipe2);

            decimal distMm = PipeUtils.DistanceMm(from, to);

            double gx1 = (double)from.X, gy1 = (double)from.Y, gz1 = (double)from.Z;
            double gx2 = (double)to.X, gy2 = (double)to.Y, gz2 = (double)to.Z;

            var manager = new GridManager(gx1, gy1, gz1, gx2, gy2, gz2, step: 100.0);
            Node start = manager.GetNodeFromWorld(gx1, gy1, gz1);
            Node target = manager.GetNodeFromWorld(gx2, gy2, gz2);

            double diameter;

            Node corner = manager.Grid[0, 0, 0];



            // ── 4. ГРАНИЦА ВЫХОДА ───────────────────────────────────────────────────
            using (Transaction tx = new Transaction(doc, "Лесенка X→Y→Z"))
            {
                tx.Start();

                Connector conn1 = PipeUtils.GetConnectorNear(doc, cat1.pipe_Id, from);
                Connector conn2 = PipeUtils.GetConnectorNear(doc, cat2.pipe_Id, to);

                XYZ p1 = conn1.Origin;
                XYZ p2 = conn2.Origin;

                XYZ midXY = new XYZ(p2.X, p1.Y, p1.Z);
                XYZ midZ = new XYZ(p2.X, p2.Y, p1.Z);

                bool hasZ = System.Math.Abs(p2.Z - p1.Z) > 1e-6;

                diameter = PipeUtils.DiameterToDouble(pipe1.Diameter);

                Pipe pipeA, pipeB, pipeC;
                myPipe_numeric numA, numB, numC;
                myPipe_categoric catA, catB, catC;

                if (!hasZ)
                {
                    // ── 2 сегмента ─────────────────────────────────────────────────
                    pipeA = Pipe.Create(doc, cat1.system_type_Id, cat1.type_Id, cat1.level_Id, p1, midXY);
                    pipeA.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameter);

                    pipeB = Pipe.Create(doc, cat1.system_type_Id, cat1.type_Id, cat1.level_Id, midXY, p2);
                    pipeB.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameter);

                    pipeC = null;
                }
                else
                {
                    // ── 3 сегмента ─────────────────────────────────────────────────
                    pipeA = Pipe.Create(doc, cat1.system_type_Id, cat1.type_Id, cat1.level_Id, p1, midXY);
                    pipeA.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameter);

                    pipeB = Pipe.Create(doc, cat1.system_type_Id, cat1.type_Id, cat1.level_Id, midXY, midZ);
                    pipeB.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameter);

                    pipeC = Pipe.Create(doc, cat1.system_type_Id, cat1.type_Id, cat1.level_Id, midZ, p2);
                    pipeC.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameter);
                }

                TaskDialog.Show("Ближайшие коннекторы",
                    $"diameter: {diameter}\n" +
                    $"Width: {manager.Width}\n" +
                    $"Height: {manager.Height}\n" +
                    $"Depth: {manager.Depth}\n" +
                    $"Точка 1: {from}\n" +
                    $"Точка 2: {to}\n" +
                    $"Расстояние: {distMm:F2} мм");

                //---------------------- Test -----------------
                //diameter = 0.05249343832021;
                //Pipe pipeX1 = Pipe.Create(doc, cat1.system_type_Id, cat1.type_Id, cat1.level_Id, new XYZ(p1.X, p2.Y, p2.Z), new XYZ(p2.X, p2.Y, p2.Z));
                //pipeX1.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameter);  //diameter = 0.05249343832021

                //Pipe pipeX2 = Pipe.Create(doc, cat1.system_type_Id, cat1.type_Id, cat1.level_Id, new XYZ(p2.X, p1.Y, p2.Z), new XYZ(p2.X, p2.Y, p2.Z));
                //pipeX2.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameter);  //diameter = 0.05249343832021

                //Pipe pipeX3 = Pipe.Create(doc, cat1.system_type_Id, cat1.type_Id, cat1.level_Id, new XYZ(p1.X, p1.Y, p2.Z), new XYZ(p2.X, p1.Y, p2.Z));
                //pipeX3.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameter);  //diameter = 0.05249343832021

                //Pipe pipeX4 = Pipe.Create(doc, cat1.system_type_Id, cat1.type_Id, cat1.level_Id, new XYZ(p1.X, p1.Y, p2.Z), new XYZ(p1.X, p2.Y, p2.Z));
                //pipeX4.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameter);  //diameter = 0.05249343832021

                // ── Слияние на граничных стыках ─────────────────────────────────────
                PipeUtils.TryMergeAsContinuation(doc, doc.GetElement(cat1.pipe_Id) as Pipe, pipeA, from);

                Pipe lastPipe = pipeC ?? pipeB;
                PipeUtils.TryMergeAsContinuation(doc, lastPipe, doc.GetElement(cat2.pipe_Id) as Pipe, to);

                // FIX 2: PickElements(pipeC) только если pipeC не null —
                //         в ветке !hasZ pipeC == null → NullReferenceException
                // + IsValidObject: TryMergeAsContinuation мог удалить трубу из документа,
                //   обращение к удалённому элементу → InvalidObjectException
                (numA, catA) = pipeA != null && pipeA.IsValidObject
                    ? SelectionHelper.PickElements(pipeA)
                    : (default, default);
                (numB, catB) = pipeB != null && pipeB.IsValidObject
                    ? SelectionHelper.PickElements(pipeB)
                    : (default, default);
                (numC, catC) = pipeC != null && pipeC.IsValidObject
                    ? SelectionHelper.PickElements(pipeC)
                    : (default, default);

                // ── Соединяем цепочку отводами ──────────────────────────────────────
                // FIX 3: убран ручной NewElbowFitting перед ConnectWithElbows —
                //         вызов на одних и тех же коннекторах дважды = исключение Revit
                var chain = new List<Pipe>
                {
                    doc.GetElement(cat1.pipe_Id) as Pipe,
                    pipeA,
                    pipeB,
                };
                if (pipeC != null) chain.Add(pipeC);
                chain.Add(doc.GetElement(cat2.pipe_Id) as Pipe);

                // Убираем трубы удалённые TryMergeAsContinuation
                chain.RemoveAll(p => p == null || !p.IsValidObject);

                PipeUtils.ConnectWithElbows(doc, chain);

                tx.Commit();
            }

            return Result.Succeeded;
        }
    }
}