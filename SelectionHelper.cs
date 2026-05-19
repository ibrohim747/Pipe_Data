using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Projects.Utils
{
    public static class SelectionHelper
    {
        /// <summary>
        /// Выбирает элементы типа T через интерактивный пикер Revit.
        /// Возвращает null если пользователь отменил выбор (Escape).
        /// </summary>
        public static List<T> PickElements<T>(UIDocument uidoc, string message)
            where T : Element
        {
            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(
                    ObjectType.Element, new TypeSelectionFilter<T>(), message);

                return refs
                    .Select(r => uidoc.Document.GetElement(r.ElementId))
                    .Cast<T>()
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Конвертирует уже имеющийся Pipe в пару (myPipe_numeric, myPipe_categoric).
        ///
        /// Используется для pipeA, pipeB, pipeC — труб, созданных внутри транзакции,
        /// чтобы дальше работать с ними так же как с выбранными трубами.
        ///
        /// Пример:
        ///   var (num, cat) = SelectionHelper.PickElements(pipeA);
        ///   Coordinate start = num.connector_0;
        /// </summary>
        public static (myPipe_numeric Numeric, myPipe_categoric Categoric) PickElements(Pipe pipe)
        {
            PipeData data = PipeUtils.ToPipeData(pipe);
            return (data.Numeric, data.Categoric);
        }
    }

    /// <summary>Фильтр выбора: разрешает только элементы типа T.</summary>
    public class TypeSelectionFilter<T> : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is T;
        public bool AllowReference(Reference r, XYZ pos) => false;
    }
}