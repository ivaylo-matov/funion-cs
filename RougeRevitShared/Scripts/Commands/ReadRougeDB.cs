using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System.IO;
#if RevitForge||RevitForgeDebug
using DesignAutomationFramework;
#endif


namespace RougeRevit
{
#if RevitForge||RevitForgeDebug
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ReadRougeDB : IExternalDBApplication
    {
        public ExternalDBApplicationResult OnStartup(ControlledApplication application)
        {
            LogTrace("Add in Startup");
            DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
        }

        private void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            LogTrace("Design Automation Ready event triggered...");
            e.Succeeded = true;
            ReadData(e.DesignAutomationData.RevitDoc);
        }

        public void ReadData(Document doc)
        {
            // Load JSON Output from TESSA Platform
            string inputJson = File.ReadAllText("current-site.json");

            var rgObject = JsonConvert.DeserializeObject<RougeElement>(inputJson);
            rgObject.Init();

            var rgData = new RougeData();

            rgData.GetAngleToNorth(rgObject, doc);

            rgData.CreateElements(rgObject, doc);
        }

        public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
        {
            return ExternalDBApplicationResult.Succeeded;
        }

        /// <summary>
        /// This will appear on the Design Automation output
        /// </summary>
        public static void LogTrace(string format, params object[] args) { System.Console.WriteLine(format, args); }
    }
#endif
}
