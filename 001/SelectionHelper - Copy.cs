using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Projects.Utils
{
    public static class SelectionHelper
    {
        public static T PickElement<T>(UIDocument uidoc, string message) where T : Element
        {
            try
            {
                Reference pickedRef = uidoc.Selection.PickObject(ObjectType.Element, message);
                Element elem = uidoc.Document.GetElement(pickedRef.ElementId);
                return elem as T;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }
    }
}