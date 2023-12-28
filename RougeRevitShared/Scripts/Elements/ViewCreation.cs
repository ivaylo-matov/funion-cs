using Autodesk.Revit.DB;
using View = Autodesk.Revit.DB.View;
using RougeRevit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
#if Revit2020||Revit2021||Revit2022
using Autodesk.Revit.UI;
using W = System.Windows.Forms;
#endif

namespace RougeRevit
{
    public class ViewCreation
    {
        public static void CreateViews(Document doc)
        {

            IEnumerable<ViewFamilyType> viewFamilyTypes = from elem in new
                FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
                                                          let type = elem as ViewFamilyType
                                                          where type.ViewFamily == ViewFamily.FloorPlan
                                                          select type;

            /// need to search for schemes as well as view types.
            IEnumerable<AreaScheme> viewAreaGIASchemes = from elem in new
                FilteredElementCollector(doc).OfClass(typeof(AreaScheme))
                                                         let area = elem as AreaScheme
                                                         where area.Name == "GIA"
                                                         select area;

            /// need to search for schemes as well as view types.
            IEnumerable<AreaScheme> viewAreaGEASchemes = from elem in new
                FilteredElementCollector(doc).OfClass(typeof(AreaScheme))
                                                         let area = elem as AreaScheme
                                                         where area.Name == "Gross Building"
                                                         select area;

            ViewFamilyType viewFamilyType = null;
            foreach (ViewFamilyType familyType in viewFamilyTypes)
            {
                if (familyType.Name == "Floor Plan")
                    viewFamilyType = familyType;
                break;
            }

            AreaScheme viewAreaGIAScheme = null;
            foreach (AreaScheme areatype in viewAreaGIASchemes)
            {
                if (areatype.Name == "GIA")
                    viewAreaGIAScheme = areatype;
                break;
            }

            AreaScheme viewAreaGEAScheme = null;
            foreach (AreaScheme areatype in viewAreaGEASchemes)
            {
                if (areatype.Name == "Gross Building")
                    viewAreaGEAScheme = areatype;
                break;
            }

            //Revit Template Assignments
            //Set Revit Template View for GA Plan
            View viewTemplate = (from viewTem in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                                 where viewTem.IsTemplate == true && viewTem.Name == "GA Plan"
                                 select viewTem).FirstOrDefault();

            //Set Revit Template View for Site Plan
            View viewSiteTemplate = (from viewTem in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                                     where viewTem.IsTemplate == true && viewTem.Name == "GA Site"
                                     select viewTem).FirstOrDefault();

            //Set Revit Template View for GIA Plan
            View viewGIATemplate = (from viewTem in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                                    where viewTem.IsTemplate == true && viewTem.Name == "GIA Plan"
                                    select viewTem).FirstOrDefault();

            //Set Revit Template View for GEA Plan
            View viewGEATemplate = (from viewTem in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                                    where viewTem.IsTemplate == true && viewTem.Name == "GEA Plan"
                                    select viewTem).FirstOrDefault();

            //Set Revit Template View for External Elevations
            View viewEleTemplate = (from viewTem in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                                    where viewTem.IsTemplate == true && viewTem.Name == "GA Elevation"
                                    select viewTem).FirstOrDefault();

            //Set Revit Template View for Site Elevations
            View viewSiteEleTemplate = (from viewTem in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                                        where viewTem.IsTemplate == true && viewTem.Name == "Site Elevation"
                                        select viewTem).FirstOrDefault();

            // Find the Default Plan in the Template and Create Elevation markers on that view, stops revit from throwing an error
            // if the ActiveView is not a Plan View.
            IEnumerable<ViewPlan> all2DViews = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>();
            var defaultPlan = all2DViews.FirstOrDefault(o => o.Name == "Default");
            var threeDPlan = Util.GetDefault3DView(doc);

            // get crop box for plan views and elevations
            CurveLoop cropLoopPlan = CreateCropLoops(doc)[0];
            CurveLoop cropLoopElev1 = CreateCropLoops(doc)[1];
            CurveLoop cropLoopElev2 = CreateCropLoops(doc)[2];

            /// Create GA PLANS
            ViewPlan viewPlan = null;

            for (int p = 0; p < Log.UsedLevels.Count; p++)
            {
                var item = Log.UsedLevels.ElementAt(p);
                var itemKey = item.Key;
                var itemValue = item.Value;

                Level currentLevel = itemValue;
                ElementId levelId = currentLevel.Id;

                // Create GA Plan and assign Modulous View Template
                viewPlan = ViewPlan.Create(doc, viewFamilyType.Id, levelId);

                // Assign name and apply Template to View
                if (viewTemplate != null && Log.ModelType.First() == "Building")
                {
                    viewPlan.Name = "GA Plan Level " + currentLevel.Name;
                    viewPlan.ViewTemplateId = viewTemplate.Id;
                }
                else if (viewTemplate != null && Log.ModelType.First() == "Site")
                {
                    viewPlan.Name = "Site Plan";
                    viewPlan.ViewTemplateId = viewSiteTemplate.Id;
                }

                // apply crop box
                viewPlan.CropBoxActive = true;
                viewPlan.CropBoxVisible = false;
                ViewCropRegionShapeManager cropManager = viewPlan.GetCropRegionShapeManager();
                cropManager.SetCropShape(cropLoopPlan);
                doc.Regenerate();

                // Add View GA Plan to Dictionary
                LogViewsOnSheets(doc, viewPlan as View);
            }

            /// create AREA PLANS
            CreateAreaPlans(doc, viewAreaGEAScheme, Log.AreaGEAPlanBoundary);
            CreateAreaPlans(doc, viewAreaGIAScheme, Log.AreaGIAPlanBoundary);

            #region Scaling Conditions
            // Hide Annotation Depending on Scale TBD - need to look at different strategy for hiding an unhiding room tags.
            //BuiltInCategory rtHide = BuiltInCategory.OST_RoomTags;
            //Action<bool> showHide = (vis) =>
            //{
            //    viewPlan.SetCategoryHidden(new ElementId((int)rtHide), vis);
            //    viewTypPlan.SetCategoryHidden(new ElementId((int)rtHide), vis);
            //};
            //int s = 200;
            //switch (s)
            //{
            //    case 50:
            //        showHide(false);
            //        break;
            //    case 100:
            //        showHide(false);
            //        break;
            //    case 200:
            //        showHide(true);
            //        break;
            //    case 250:
            //        showHide(true);
            //        break;
            //    case 500:
            //        showHide(true);
            //        break;
            //    case 1000:
            //        showHide(true);
            //        break;
            //}
            #endregion

            #region Elevations
            /// Create Elevation Views.
            /// Find an elevation view type
            IEnumerable<ViewFamilyType> viewFamilyTypesElev = from elem in new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
                                                              let type = elem as ViewFamilyType
                                                              where type.ViewFamily == ViewFamily.Elevation
                                                              select type;

            // get the positions of the elevation markers from the cropLoop
            List<XYZ> elevLoc = new List<XYZ>();
            foreach (Curve curve in cropLoopPlan)
            {
                XYZ startPoint = curve.GetEndPoint(0);
                XYZ endPoint = curve.GetEndPoint(1);
                elevLoc.Add((startPoint + endPoint) * 0.5);
            }

            // temp value for currentScale before the elevation is placed of sheet
            int currentScale = 200;

            // Create Elevation Markers for Each View Location from elevLoc array.
            for (int i = 0; i < elevLoc.Count; i++)
            {
                // place elevation markers and rotate with the angle to true north
                ElevationMarker marker = ElevationMarker.CreateElevationMarker(doc, viewFamilyTypesElev.First().Id, elevLoc[i], currentScale);
                ViewSection elevationView = marker.CreateElevation(doc, defaultPlan.Id, i);
                marker.Location.Rotate(Line.CreateBound(elevLoc[i], elevLoc[i].Add(XYZ.BasisZ)), 90 * Util.ToRadians);

                // set crop regions
                elevationView.CropBoxActive = true;

                ViewCropRegionShapeManager cropManager = elevationView.GetCropRegionShapeManager();
                CurveLoop cropLoop = new CurveLoop();
                if (i % 2 == 0) cropLoop = cropLoopElev1;
                else cropLoop = cropLoopElev2;

                // fix level extends
                try
                {
                    SetViewLevelExtents(doc, elevationView);
                }
                catch { }

                Action<string> elevName = (name) =>
                {
                    elevationView.Name = name;
                };

                switch (i)
                {
                    case 0:
                        elevName("North Elevation");
                        break;
                    case 1:
                        elevName("East Elevation");
                        break;
                    case 2:
                        elevName("South Elevation");
                        break;
                    case 3:
                        elevName("West Elevation");
                        break;
                    default:
                        elevName("External Elevation" + i);
                        break;
                }
                if (Log.ModelType.First() == "Site")
                {
                    elevationView.Name = $"Site {elevationView.Name}";
                    elevationView.ViewTemplateId = viewSiteEleTemplate.Id;
                }
                else { elevationView.ViewTemplateId = viewEleTemplate.Id; }

                // to update the level curves crop region needs to be reset
                CurveLoop resetCrop = CurveLoop.CreateViaCopy(cropLoop);
                cropManager.SetCropShape(resetCrop);
                doc.Regenerate();
                elevationView.CropBoxVisible = false;

                LogViewsOnSheets(doc, elevationView as View);
            }

            // Push BuildingName to levels so that the linked model levels can be filtered out on elevation views
            SetLevelsBuildingName();

            #endregion
        }



        /// <summary>
        /// Method to create area plans of given area scheme
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="viewAreaScheme"></param>
        /// <param name="boundaryDictionary"></param>
        public static void CreateAreaPlans(Document doc, AreaScheme viewAreaScheme,
            Dictionary<int, Tuple<CurveArray,Level>> boundaryDictionary)
        {
            ViewPlan viewAreaPlan = null;
            string areaType = "";

            // check if the area is GIA or GEA
            if (viewAreaScheme.Name == "GIA") areaType = "GIA";
            else if (viewAreaScheme.Name == "Gross Building") areaType = "GEA";

            // create dictionary to hold the AreaPlan
            Dictionary<string, ViewPlan> areaPlans = new Dictionary<string, ViewPlan>();

            foreach (KeyValuePair<int, Tuple<CurveArray, Level>> kvp in boundaryDictionary)
            {
                int key = kvp.Key;
                Tuple<CurveArray, Level> value = kvp.Value;
                CurveArray curveArray = value.Item1;
                Level currentLevel = value.Item2;
                double currentElevation = currentLevel.Elevation;
                ElementId levelId;

                // check if area plan on the same elevation is not created already
                if (!areaPlans.ContainsKey(currentElevation.ToString()))
                {
                    // create new Area Plan
                    levelId = currentLevel.Id;
                    viewAreaPlan = ViewPlan.CreateAreaPlan(doc, viewAreaScheme.Id, currentLevel.Id);
                    viewAreaPlan.Name = $"{areaType} Area Plan Level {currentLevel.Name}";

                    // add to areaPlans dictionary
                    areaPlans.Add(currentElevation.ToString(), viewAreaPlan);
                }
                else
                {
                    viewAreaPlan = areaPlans[currentElevation.ToString()];
                    Level level = viewAreaPlan.GenLevel;
                    levelId = level.Id;
                }

                /// point used to floor AREA objects.
                double parameter = 0.5; // Change this value as needed
                XYZ pointOnLine = null;

                // Check for Roof Level, if exists don't create Area Objects
                if (!currentLevel.Name.Contains("Roof"))
                {
                    /// Create Area Boundary Lines for GEA Area Plans
                    SketchPlane areaPlane = SketchPlane.Create(doc, currentLevel.Id);

                    // Retrieve Area Boundary for GEA, retrieve CurvedArray from dictionary.
                    var areaBoundary = curveArray;

                    foreach (Curve curve in areaBoundary)
                    {
                        // change the Z coordinates to match level elevation (fixes area boundary warnings)
                        IList<XYZ> points = curve.Tessellate();
                        if (points != null && points.Count > 0)
                        {
                            for (int i = 0; i < points.Count; i++)
                            {
                                points[i] = new XYZ(points[i].X, points[i].Y, currentElevation);
                            }
                        }
                        Curve newCurve = Line.CreateBound(points[0], points[points.Count - 1]);

                        // build the area boundary curve
                        ModelCurve boundarySegment = doc.Create.NewAreaBoundaryLine(areaPlane, newCurve, viewAreaPlan);
                        Curve areaBoundaryOffset = curve.CreateOffset(0.5, new XYZ(0, 0, 1)) as Curve;
                        pointOnLine = areaBoundaryOffset.Evaluate(parameter, true);
                    }

                    // Convert from XYZ to UV for Area Flooding Point
                    UV uV = new UV(pointOnLine[0], pointOnLine[1]); //points based on Centre of Curve

                    //Create Area Object in Plan
                    Area area = doc.Create.NewArea(viewAreaPlan, uV);
                }
            }
        }

        /// <summary>
        /// Creates a drawing sheet for each view
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="viewName"></param>
        /// <param name="planId"></param>
        public static void CreateSheets(Document doc)
        {
            int scale;
            ViewSheet sheet = null;

            // prep the unit legend symbols
            List<FamilySymbol> legends = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .Where(s => s.FamilyName.Equals("Anno_ApartmentLabel"))
                .ToList();

            foreach (KeyValuePair<string, Tuple<View, FamilySymbol, XYZ, int>> kvp in Log.ViewsOnSheets)
            {
                string viewName = kvp.Key;
                Tuple<View, FamilySymbol, XYZ, int> viewData = kvp.Value;
                View view = viewData.Item1;
                FamilySymbol titleblock = viewData.Item2;
                XYZ position = viewData.Item3;

                if (view.ViewType == ViewType.Elevation) scale = Log.ElevationScale;
                else scale = viewData.Item4;

                // create sheet
                sheet = ViewSheet.Create(doc, titleblock.Id);
                sheet.Name = viewName;

                // apply parameters
                sheet.LookupParameter("Drawing Location").Set(SetDrawingLocationParam(doc, view));
                sheet.LookupParameter("Drawing Type").Set("DR"); //placing only drawings on sheets right now
                sheet.LookupParameter("Drawing Role").Set("A"); // assume architecture only
                sheet.LookupParameter("Drawing Status").Set("S0 - Work In Progress"); // assuming cannot be anything else

                // develop system for Sheet Numbering
                string packageCode = string.Empty;
                if (sheet.Name.Contains("Site")) packageCode = "000";
                else if (sheet.Name.Contains("GA")) packageCode = "100";
                else if (sheet.Name.Contains("Sect")) packageCode = "110";
                else if (sheet.Name.Contains("Ele")) packageCode = "120";

                // store the sheet number in the sheetNumbers dictionary
                if (!Log.SheetNumbers.ContainsKey(packageCode))
                {
                    Log.SheetNumbers[packageCode] = 0;
                }
                Log.SheetNumbers[packageCode]++;
                sheet.SheetNumber = $"{packageCode}-{Log.SheetNumbers[packageCode].ToString().PadLeft(3, '0')}";

                Viewport newViewport = null;
                if (Viewport.CanAddViewToSheet(doc, sheet.Id, view.Id))
                {
                    // set the scale
                    view.Scale = scale;
                    newViewport = Viewport.Create(doc, sheet.Id, view.Id, position);
                }

                // place apartment legend
                if (sheet.Name.Contains("GA")) CreateUnitLegend(doc, sheet, view, titleblock, legends);
            }
        }

        /// <summary>
        /// Get the Drawing Location parameter from View placed on Sheet
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="viewId"></param>
        /// <returns></returns>
        private static string SetDrawingLocationParam(Document doc, View view)
        {
            if (view.GenLevel == null)  return "ZZ";
            else if (view.Name.Contains("RF")) return "RF";
            else return view.GenLevel.Name;
        }

        /// <summary>
        /// Places unit legend elements on sheets
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="sheet"></param>  
        public static void CreateUnitLegend(Document doc, ViewSheet sheet, View view, FamilySymbol titleblock, List<FamilySymbol> legends)
        {
            // set location for the legend based on titleblock orientation
            XYZ location = new XYZ(0.380.MetersToFeet(), 0.175.MetersToFeet(), 0);

            // check if the titleblock has a key map
             if (titleblock.LookupParameter("Key Map").AsInteger() != 1) location = new XYZ(0.380.MetersToFeet(), 0.232.MetersToFeet(), 0);

            // get the level elevation of the view placed on the sheet 
            string elev = view.GenLevel.Elevation.FeetToMeters().ToString();

            if (Log.ApartmentTypes.ContainsKey(elev))
            {
                List<string> usedTypes = Log.ApartmentTypes[elev];
                usedTypes.Sort();
                for (int i = 0; i < usedTypes.Count(); i++)
                {
                    // get the correct legend family type
                    FamilySymbol legendSymbol = null;
                    XYZ newLocation = new XYZ(location.X, location.Y - (i * 0.005).MetersToFeet(), location.Z);

                    foreach (var legend in legends)
                    {
                        if (legend.Name.Equals(usedTypes[i])) legendSymbol = legend;
                    }
                    if (legendSymbol != null) doc.Create.NewFamilyInstance(newLocation, legendSymbol, sheet);
                }
            }
        }

        private static void SetLevelsBuildingName()
        {
            foreach (var kvp in Log.UsedLevels)
            {
                Level level = kvp.Value as Level;
                level.LookupParameter("BuildingName").Set(Log.BuildingName);
            }
        }

        /// <summary>
        /// Generates a CurveLoops to be applied to plan and elevation views
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        private static List<CurveLoop> CreateCropLoops(Document doc)
        {
            List<CurveLoop> cropLoops = new List<CurveLoop>();

            //  create the points for PLANS
            List<XYZ> cropLoopPoints = new List<XYZ>
            {
                new XYZ(Log.ProjectBoundingBox[0].X, Log.ProjectBoundingBox[1].Y, Log.ProjectBoundingBox[0].Z),
                new XYZ(Log.ProjectBoundingBox[1].X, Log.ProjectBoundingBox[1].Y, Log.ProjectBoundingBox[0].Z),
                new XYZ(Log.ProjectBoundingBox[1].X, Log.ProjectBoundingBox[0].Y, Log.ProjectBoundingBox[0].Z),
                new XYZ(Log.ProjectBoundingBox[0].X, Log.ProjectBoundingBox[0].Y, Log.ProjectBoundingBox[0].Z)
            };

            // create the points for ELEVATIONS 0 & 2
            List<XYZ> cropPointsElev1 = new List<XYZ>()
            {
                cropLoopPoints[0],
                new XYZ(Log.ProjectBoundingBox[0].X, Log.ProjectBoundingBox[1].Y, Log.ProjectBoundingBox[1].Z),
                new XYZ(Log.ProjectBoundingBox[1].X, Log.ProjectBoundingBox[1].Y, Log.ProjectBoundingBox[1].Z),
                cropLoopPoints[1]
            };

            // create the points for elevation 1 & 3
            List<XYZ> cropPointsElev2 = new List<XYZ>()
            {
                cropLoopPoints[1],
                new XYZ(Log.ProjectBoundingBox[1].X, Log.ProjectBoundingBox[1].Y, Log.ProjectBoundingBox[1].Z),
                new XYZ(Log.ProjectBoundingBox[1].X, Log.ProjectBoundingBox[0].Y, Log.ProjectBoundingBox[1].Z),
                cropLoopPoints[2]
            };

            List<List<XYZ>> combinedPoints = new List<List<XYZ>>()
            {
                cropLoopPoints,cropPointsElev1, cropPointsElev2
            };

            // CREATE CROP LOOPS
            foreach (List<XYZ> points in combinedPoints)
            {
                CurveArray cArray = new CurveArray();
                for (int i = 0; i < points.Count(); i++)
                {
                    Line line = Line.CreateBound(points[i], points[(i + 1) % points.Count()]);
                    cArray.Append(line);
                }
                List<Curve> cList = cArray.Cast<Curve>().ToList();
                cropLoops.Add(CurveLoop.Create(cList));
            }

            return cropLoops;
        }

        /// <summary>
        /// Trim level curves in view to keep within the crop region
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="view"></param>
        /// <param name="cropLoop"></param>
        private static void SetViewLevelExtents(Document doc, View view)
        {
            double minX = Log.ProjectBoundingBox[0].X;
            double minY = Log.ProjectBoundingBox[0].Y;
            double maxX = Log.ProjectBoundingBox[1].X;
            double maxY = Log.ProjectBoundingBox[1].Y;

            // get the levels and their curves
            List<Level> levels = new FilteredElementCollector(doc, view.Id).OfClass(typeof(Level)).Cast<Level>().ToList();

            foreach (Level level in levels)
            {
                level.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.Model);
                level.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.Model);

                Curve levelCurve = level.GetCurvesInView(DatumExtentType.Model, view).First();
                XYZ start = levelCurve.GetEndPoint(0);
                XYZ end = levelCurve.GetEndPoint(1);

                Line newLine = levelCurve as Line;

                // set the new curves
                if (start.X == end.X)
                {
                    newLine = Line.CreateBound(new XYZ(start.X, minY, start.Z), new XYZ(end.X, maxY, end.Z));
                }
                else if (start.Y == end.Y)
                {
                    newLine = Line.CreateBound(new XYZ(minX, start.Y, start.Z), new XYZ(maxX, end.Y, end.Z));
                }

                level.SetCurveInView(DatumExtentType.Model, view, newLine);
            }
        }

        /// <summary>
        /// Calculates view scale and titleblock for views to be placed on sheets
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="view"></param>
        /// <returns></returns>
        private static Tuple<int, FamilySymbol, XYZ> GetViewScaleAndTitleBlock(Document doc, View view)
        {
            // Define the dimensions of the landscape frame (in meters)
            double lWidth = 0.330;
            double lHeight = 0.270;
            // Define the dimensions of the portrait frame (in meters)
            double pWidth = 0.210;
            double pHeight = 0.400;
            // Define the available scales (500 as default below)
            int[] scales = { 50, 100, 150, 200, 250, 300, 500, 1000, 1500 };
            // give default scale and define variables to store the best scale and titleblock
            int lScale = 2000;
            int pScale = 2000;

            // get the crop region of the view
            ViewCropRegionShapeManager cropManager = view.GetCropRegionShapeManager();
            CurveLoop cropLoop = cropManager.GetCropShape().First();

            double xHeight = cropLoop.First().Length.FeetToMeters();
            double xWidth = cropLoop.Last().Length.FeetToMeters();

            if (view.ViewType == ViewType.Elevation)
            {
                xWidth = cropLoop.First().Length.FeetToMeters();
                xHeight = cropLoop.Last().Length.FeetToMeters();
            }

            lScale = scales.FirstOrDefault(scale => (xWidth / scale) <= lWidth && (xHeight / scale) <= lHeight);
            pScale = scales.FirstOrDefault(scale => (xWidth / scale) <= pWidth && (xHeight / scale) <= pHeight);

            // determine the largest scale and title block name
            int largestScale = lScale;
            string titleblockName = "A3 Landscape";
            XYZ position = new XYZ(0.69, 0.5, 0);

            if (pScale < lScale)
            {
                largestScale = pScale;
                titleblockName = "A3 Portrait";
                position = new XYZ(0.9, 0.3, 0);
            }

            if (view.ViewType == ViewType.FloorPlan)
            {
                titleblockName += " Plans";
            }

            // get the titleblock
            FamilySymbol titleblock = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .Cast<FamilySymbol>()
                .FirstOrDefault(x => x.Name == titleblockName);

            if (titleblock == null)
            {
                titleblock = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .Cast<FamilySymbol>()
                .FirstOrDefault();
            }

            return new Tuple<int, FamilySymbol, XYZ>(largestScale, titleblock, position);
        }

        /// <summary>
        /// Logs view data for views to be placed on sheets. Single scale value for elevations
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="view"></param>
        private static void LogViewsOnSheets(Document doc, View view)
        {
            // get the optimal scale for the view
            int scale = GetViewScaleAndTitleBlock(doc, view).Item1;
            FamilySymbol titleblock = GetViewScaleAndTitleBlock(doc, view).Item2;
            XYZ position = GetViewScaleAndTitleBlock(doc, view).Item3;

            Log.ViewsOnSheets[view.Name] = new Tuple<View, FamilySymbol, XYZ, int>(view, titleblock, position, scale);

            if (view.ViewType == ViewType.Elevation)
            {
                if (Log.ElevationScale <= scale)
                {
                    Log.ElevationScale = scale;
                }
            }
        }
    }
}
