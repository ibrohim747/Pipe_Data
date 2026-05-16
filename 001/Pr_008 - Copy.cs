using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Projects.Utils; // Не забудьте подключить пространство имен ваших утилит

namespace Projects
{
    [Transaction(TransactionMode.Manual)]
    public class Pr_008 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Выбор трубы (используем наш хелпер)
            Pipe sourcePipe = SelectionHelper.PickElement<Pipe>(uidoc, "Выберите исходную трубу");
            if (sourcePipe == null) return Result.Cancelled;

            // 2. Сбор параметров (через статический утилитный класс)
            ElementId pipeTypeId = sourcePipe.GetTypeId();
            ElementId systemTypeId = PipeUtils.GetSystemTypeId(sourcePipe);
            ElementId levelId = PipeUtils.GetLevelId(sourcePipe);
            var connectors = PipeUtils.GetConnectors(sourcePipe);

            if (connectors.Count == 0) return Result.Failed;

            // 3. Создание новой трубы
            XYZ endPoint = connectors[0].Origin;
            XYZ startPoint = new XYZ(0, 5, 0);

            using (Transaction tx = new Transaction(doc, "Создание трубы"))
            {
                tx.Start();
                Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, startPoint, endPoint);
                tx.Commit();
            }

            TaskDialog.Show("Готово", "Труба успешно создана по новым правилам архитектуры кода!");

            return Result.Succeeded;
        }
    }
}