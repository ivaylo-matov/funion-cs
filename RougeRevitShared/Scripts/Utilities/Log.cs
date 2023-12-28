using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
#if RevitForge || RevitForgeDebug
using DesignAutomationFramework;
#else
using Autodesk.Revit.UI;
#endif

namespace RougeRevit
{
    /// <summary>
    /// Static class for keeping track of the creation of the <see cref="RougeElement"/> that are being imported
    /// </summary>
    static class Log
    {
        /// <summary>List of created <see cref="RougeElement"/></summary>
        public static Dictionary<string, int> Created = new Dictionary<string, int>();

        /// <summary>List of error messages from the creation process</summary>
        public static List<string> ErrorMsgs = new List<string>();

        /// <summary>Dictionary of apartment types by level elevation
        public static Dictionary<string, List<string>> ApartmentTypes = new Dictionary<string, List<string>>();

        /// <summary>Dictionary of available <see cref="ElementType"/>, accessed by "CodeCategory|CodeType"</summary>
        public static Dictionary<string, ElementType> AvailableTypes = new Dictionary<string, ElementType>();

        /// <summary>Dictionary of available <see cref="FamilySymbol"/>, accessed by "CodeCategory|CodeType"</summary>
        public static Dictionary<string, FamilySymbol> AvailableSymbols = new Dictionary<string, FamilySymbol>();

        /// <summary>Dictionary of created <see cref="Level"/>, accessed by elevation</summary>
        public static Dictionary<double, Level> UsedLevels = new Dictionary<double, Level>();

        /// <summary>Dictionary of created <see cref="Level"/>, accessed by elevation</summary>
        public static Dictionary<int, Tuple<CurveArray, Level>> AreaGEAPlanBoundary = new Dictionary<int, Tuple<CurveArray, Level>>();

        /// <summary>Dictionary of created <see cref="Level"/>, accessed by elevation</summary>
        public static Dictionary<int, Tuple<CurveArray, Level>> AreaGIAPlanBoundary = new Dictionary<int, Tuple<CurveArray, Level>>();

        /// <summary>Dictionary of created technical room polygons
        public static List<CurveArray> RoomBoundary = new List<CurveArray>();

        /// <summary>Collection of lists to hold the site data</summary>>
        public static List<double> SiteData = new List<double>();
        public static List<double> ApartmentRotations = new List<double>();
        public static double[][] SiteBoundary;

        /// <summary>Lists to hold all facade and internal walls so they can be joined
        public static List<Wall> FacadeWalls = new List<Wall>();  
        public static List<Wall> InternalWalls = new List<Wall>();

        //ip - temp
        public static Dictionary<string, List<Wall>> CommunalWalls = new Dictionary<string, List<Wall>>();
        public static Dictionary<string, List<ComWallData>> CommunalWallData = new Dictionary<string, List<ComWallData>>();
        public static Dictionary<string, List<ComWallData>> CommunalDoorData = new Dictionary<string, List<ComWallData>>();

        /// <summary>String to hold the building name
        public static string BuildingName = string.Empty;

        /// <summary>Dictionary of views to be placed on sheets <viewName, Tuple<view, titleblock, viewportPosition, scale>> </summary>
        public static Dictionary<string, Tuple<View, FamilySymbol, XYZ, int>> ViewsOnSheets = new Dictionary<string, Tuple<View, FamilySymbol, XYZ, int>>();
        public static int ElevationScale;

        /// <summary>Dictionaries of the Apartments and Buildings that have been loaded already, accessed by their name</summary>
        public static Dictionary<string, ElementId> LoadedApartments = new Dictionary<string, ElementId>();
        public static Dictionary<string, ElementId> LoadedBuildings = new Dictionary<string, ElementId>();

        /// <summary>Dictionary of creates Sheet numbers name</summary>
        public static Dictionary<string, int> SheetNumbers = new Dictionary<string, int>();

        /// <summary>Dictionaries of available apartment and building files that can be loaded in the project</summary>
        public static Dictionary<string, string> ApartmentFiles = new Dictionary<string, string>();
        public static Dictionary<string, string> BuildingFiles = new Dictionary<string, string>();

        /// <summary>Dictionary of <see cref="XYZ"/> Offsets of apartments in order to place them correctly</summary>
        public static Dictionary<string, XYZ> ApartmentOffsets = new Dictionary<string, XYZ>();

        /// <summary><see cref="ElemenetId"/> of the "Code: Category" parameter</summary>
        public static ElementId CodeCategoryParamId;

        /// <summary><see cref="ElemenetId"/> of the "Code: Type" parameter</summary>
        public static ElementId CodeTypeParamId;
        /// <summary>A bounding box of the project</summary>
        public static List<XYZ> ProjectBoundingBox = new List<XYZ>();
        public static List<string> ModelType = new List<string>();

        private static string _lineSeparator = "--------------------------" + Environment.NewLine;
        private static DateTime _startTime;

        /// <summary>
        /// Retrieve the formated log
        /// </summary>
        /// <returns></returns>
        public static string GetLog()
        {
            // Created elements section
            string message = "Created Elements:";
#if DEBUG
            message += $"{Environment.NewLine}Generated in: {(DateTime.Now - _startTime).TotalMilliseconds.ToString("F2")} ms";
#endif

            foreach (var type in Created.Keys)
            {
                var count = Created[type];
                if (count > 0) message += $"{Environment.NewLine}  {type}: {count}";
            }

            message += Environment.NewLine + $"Total of created elements: {Created.Values.Sum()}";

            // Error section
            if (ErrorMsgs.Count > 0)
            {
                message += Environment.NewLine + Environment.NewLine + _lineSeparator;
                message += Environment.NewLine + "Errors:" + Environment.NewLine + "  - " + String.Join(Environment.NewLine + "  - ", ErrorMsgs);
            }

            return message;
        }

#if RevitForge || RevitForgeDebug
#else

        /// <summary>
        /// Show the resulting log in a <see cref="TaskDialog"/> format
        /// </summary>
        public static void ShowLog()
        {
            TaskDialog dialogBox = new TaskDialog("Result Log");
            dialogBox.MainContent = GetLog();
            dialogBox.CommonButtons = TaskDialogCommonButtons.Ok;
            dialogBox.Show();
        }
#endif

        /// <summary>
        /// Clear up all the properties of the static object before running a new command
        /// </summary>
        public static void Clear()
        {
            Created = new Dictionary<string, int>();
            ErrorMsgs = new List<string>();
            AvailableTypes = new Dictionary<string, ElementType>();
            AvailableSymbols = new Dictionary<string, FamilySymbol>();
            UsedLevels = new Dictionary<double, Level>();
            ViewsOnSheets = new Dictionary<string, Tuple<View, FamilySymbol, XYZ, int>>();
            ApartmentFiles = new Dictionary<string, string>();
            LoadedApartments = new Dictionary<string, ElementId>();
            ApartmentOffsets = new Dictionary<string, XYZ>();
            ModelType = new List<string>();
#if DEBUG
            _startTime = DateTime.Now;
#endif
        }

        public static void AddToCreated(RougeElement el)
        {
            if (Created.ContainsKey(el.Type)) Created[el.Type] += 1;
            else Created.Add(el.Type, 1);
        }
    }
}
