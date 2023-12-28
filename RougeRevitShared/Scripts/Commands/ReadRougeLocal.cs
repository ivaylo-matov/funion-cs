using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System.IO;
#if Revit2020||Revit2021||Revit2022
using Autodesk.Revit.UI;
using W = System.Windows.Forms;
#endif

namespace RougeRevit
{
#if Revit2020||Revit2021||Revit2022
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]

    class ReadRougeLocal : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Document and Application documents
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Open file dialog window
            W.OpenFileDialog openDia = new W.OpenFileDialog();
            openDia.Filter = "Json file (*.json)|*.json";
            openDia.Title = "Select a Rouge JSON file";
            if (openDia.ShowDialog() != W.DialogResult.OK) return Result.Cancelled;
            string json = File.ReadAllText(openDia.FileName);

            var rgObject = JsonConvert.DeserializeObject<RougeElement>(json);            
                  
            rgObject.Init();

            var rgData = new RougeData();

            rgData.GetAngleToNorth(rgObject, doc);

            rgData.CreateElements(rgObject, doc);

            return Result.Succeeded;
        }
    }
#endif
}
