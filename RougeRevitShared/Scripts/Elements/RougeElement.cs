//using Amazon.Runtime.Internal.Transform;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json.Linq;
using RougeRevit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Document = Autodesk.Revit.DB.Document;
using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Windows.Forms.VisualStyles;
#if Revit2021
using Autodesk.Revit.UI;
#endif

namespace RougeRevit
{
    /// <summary>
    /// Default / generic class to receive all elements from Rouge and handle creating them on a Revit document.
    /// </summary>
    public class RougeElement
    {
        public RougeElement Parent;

        #region Serialisable properties

        public string Name;
        public double[] Pos;
        public double[] Rot;
        public double[] Size;
        public double[] Anchor;
        public string ModulousId;
        public RougeElement[] Children;
        public string Type;
        public string Uuid;
        public CustomData Data;
        public RougeElement[] templates;

        #endregion

        #region Getters

        //Vector getters flip Y and Z in Pos and Anchor
        public XYZ PosVec => new XYZ(Pos[0], -Pos[2],Pos[1]);
        public XYZ AnchorVec => new XYZ(Anchor[0], (1 - Anchor[2]) ,Anchor[1]);
        public XYZ SizeVec => new XYZ(Size[0], Size[2], Size[1]);
        public Transform Rotation => Transform.CreateRotation(XYZ.BasisX, Rot[0] * Util.ToRadians) *
                Transform.CreateRotation(XYZ.BasisY, Rot[2] * Util.ToRadians) *
                Transform.CreateRotation(XYZ.BasisZ, Rot[1] * Util.ToRadians);

        public string CodeCategory => ModulousId.Split('|')[0];
        public string CodeType => ModulousId.Split('|')[1];

        public Element Instance { get; private set; }

        #endregion

        #region Private Variables and constants

        /// <summary>
        /// Defines the types of element that can be created in Revit
        /// </summary>
        public static readonly string[] Instantiatables = new string[]
        {
            "Levels",
            "Wall",
            "Parapet",
            "Facade",
            "Floor",
            "Ceiling",
            "Door",
            "Window",
            "Beam",
            "Column",
            "Roof",
            "Room",
            "Opening"
        };

        /// <summary>
        /// Defines the types of element that can not be created in Revit
        /// </summary>
        public static readonly string[] NonInstantiables = new string[]
        {
            "Site",
            "Zone",
            "Plot",
            "Building",
            "Core",
            "Corridor",
            "TechnicalSpace",
            "Apartment",
            "Module",
            "Balcony"
        };

        /// <summary>
        /// Defines the types that have only one parent that should be accounted for
        /// </summary>
        private static readonly string[] _singleParents = new string[] 
        { 
            "Core",
            "Corridor",
            "Building",
            "Apartment" 
        };
        #endregion

        /// <summary>
        /// Follow Methods force the Apartment object in the Building to use the Template definitions 
        /// </summary>

        public void Init() {
            UseTemplates(templates);
            RemoveMuscleUnits();
        }

        void UseTemplates(RougeElement[] templates)
        {
            if (Type == "Apartment")
            {
                UseTemplatesOnApartment(templates);
            }
            else
            {
                foreach (RougeElement child in Children)
                {
                    child.UseTemplates(templates);
                }
            }
        }

        void UseTemplatesOnApartment(RougeElement[] templates)
        {
            // find template for apartment
            var template = templates.FirstOrDefault(t => t.Name == this.Name);
            Children = template.Children;
        }

        public void RemoveMuscleUnits()
        {
            Children = Children.Where(c => {
                if (c.Data == null) return true;
                if (c.Data.DtoMuscleUnit == true) return false;
                return true;
            }).ToArray();
            foreach (RougeElement child in Children)
            {
                child.RemoveMuscleUnits();
            }
        }

        /// <summary>
        /// Create the Revit element of this <see cref="RougeElement"/>, based on its Type, in the given <see cref="Document"/>
        /// </summary>
        /// <param name="doc"></param>
        public void CreateElement(Document doc)
        {
            // Check if levels should be created
            if (Data != null && Data.Levels != null)
            {
                CreateLevels(doc);
            }

            if (Type == "Building" && Children.Length > 0)
            {
                Log.BuildingName = Name;
                Log.ModelType.Add("Building");
            }
            else if (Type == "Building" && Children.Length == 0)
            {
                CreateBuildingInstance(doc);
                CreateSiteLevel(doc);
                Log.ModelType.Add("Site");
            }
            // Check for Apartment Type, if exists then stop processing other element creation.
            else if (Type == "Apartment")
            {
                Log.AddToCreated(this);
                string elev = Pos[1].ToString();
                if (!Log.ApartmentTypes.ContainsKey(elev)) Log.ApartmentTypes[elev] = new List<string>();
                if (!Log.ApartmentTypes[elev].Contains(Data.Category)) Log.ApartmentTypes[elev].Add(Data.Category);

                if (CreateApartmentInstance(doc)) return;
            }
            else if (Type == "Facade" || Type == "Parapet") CreateWall(doc);
            else if (Type == "Wall")
            {
                if (Parent.Parent.Type == "TechnicalSpace"
                    || Parent.Type == "Core"
                    || Parent.Type == "Entrance") CollectCommunalWallData(doc);
                else CreateWall(doc);
            }
            else if (Type == "Floor") CreateFloor(doc);
            else if (Type == "Ceiling") CreateCeiling(doc);
            else if (Type == "Roof") CreateRoof(doc);
            else if (Type == "Window") CreateFamilyInstance(doc);
            else if (Type == "Door")
            {
                if (Parent.Parent.Parent.Type == "TechnicalSpace"
                    || Parent.Parent.Type == "Core"
                    || Parent.Parent.Type == "Entrance") CollectCommunalDoorData(doc);
                else CreateFamilyInstance(doc);
            }
            else if (Type == "TechnicalSpace" || Type == "Core" || Type == "Entrance" || Type == "Corridor") CreateRoom(doc);
            else if (Type == "Opening") CreateOpening(doc);
            else if (Type == "World") SetProjectName(doc);
            else if (Instantiatables.Contains(Type))
            {
                string err = $"Support for the '{Type}' category has not been implemented yet";
                if (!Log.ErrorMsgs.Contains(err)) Log.ErrorMsgs.Add(err);
            }
            else Log.AddToCreated(this);

            if (Children.Length > 0)
            {
                foreach (RougeElement child in Children)
                {
                    child.Parent = this;
                    child.CreateElement(doc);
                }
            }
        }

        #region Element creation per category

        /// <summary>
        /// Creates the levels this <see cref="RougeElement"/> requires, if they don't exist yet
        /// </summary>
        /// <param name="doc"></param>
        /// <exception cref="Exception"></exception>
        public void CreateLevels(Document doc)
        {
            var myLevels = Data.Levels.Length;
            
            // Add Levels to Level Dictionary, if they don't exist already
            for (int i = 0; i < myLevels; i++)
            {
                double elevation = 0;

                if (Data.Levels[i].FloorWorldBottom != 0)
                {
                    elevation = Data.Levels[i].FloorWorldBottom.MetersToFeet();
                }
                else if (Data.Levels[i].FFL != 0)
                {
                    elevation = Data.Levels[i].FFL.MetersToFeet();
                }

                // filter out duplicate levels
                if (!Log.UsedLevels.ContainsKey(elevation))
                {
                    Level level = Level.Create(doc, elevation);

                    // Set Computation height as 500mm above Level. Need for Room Calculation due to some internal walls being 305mm above the Level.
                    double levelComp = 0.5;

                    level.get_Parameter(BuiltInParameter.LEVEL_ROOM_COMPUTATION_HEIGHT).Set(levelComp.MetersToFeet());

                    if (level == null)
                    {
                        throw new Exception("Create a new level failed.");
                    }

                    // Change the level name, check for Roof Level
                    if (!Data.Levels[i].IsRoof)
                    {
                        //level.Name = $"Level {levelCode}_{i}";
                        level.Name = Data.Levels[i].levelIndex.ToString().PadLeft(2, '0');
                    }
                    else level.Name = $"{Data.Levels[i].levelIndex.ToString().PadLeft(2, '0')}_RF";

                    CreateAreaPolygons(doc, Data.Levels[i].levelIndex, i, elevation, level);

                    // Add Levels to Dictionary
                    // *** this  is why it is not adding the levels to the dictionary, if there are 2 areas on 1 level, it wont create the level.
                    Log.UsedLevels.Add(elevation, level);
                }
            }
        }

        /// <summary>
        /// Creates single site level at elevation 00. Site model only
        /// </summary>
        /// <param name="doc"></param>
        /// <exception cref="Exception"></exception>
        public void CreateSiteLevel(Document doc)
        {
            string levelName = "00";
            double elevation = 0;
            if (!Log.UsedLevels.ContainsKey(elevation))
            {
                Level level = Level.Create(doc, elevation);
                level.Name = levelName;
                Log.UsedLevels.Add(elevation, level);
            }
        }

        /// store each area polygon in a Dictionary along with the level and it's index (not level code!)
        private void CreateAreaPolygons(Document doc, string l, int i, double elevation, Level level)
        {
            if (this.Data.Levels[i].GEAPolygon != null)
            {
                CurveArray GEAAreaBoundary = Util.CreatePolygon(this, this.Data.Levels[i].GEAPolygon);
                Log.AreaGEAPlanBoundary.Add(i, new Tuple<CurveArray, Level>(GEAAreaBoundary, level));
            }
            if (this.Data.Levels[i].GIAPolygon != null)
            {
                CurveArray GIAAreaBoundary = Util.CreatePolygon(this, this.Data.Levels[i].GIAPolygon);
                Log.AreaGIAPlanBoundary.Add(i, new Tuple<CurveArray, Level>(GIAAreaBoundary, level));
            }
        }

        /// store each communal room polygon in a List
        private void CreateRoomPolygons(Document doc)
        {
            if (this.Data.Polygon != null)
            {
                CurveArray roomBoundary = Util.CreatePolygon(this, this.Data.Polygon);
                Log.RoomBoundary.Add(roomBoundary);
            }
        }

        /// <summary>
        /// Expands the ProjectBoundingBoxPoints to include the endpoints of external walls
        /// </summary>
        /// <param name="baseline"></param>
        private void AddToProjectBoundingBox(Line baseline)
        {
            List<XYZ> points = new List<XYZ>();
            XYZ minXYZ;
            XYZ maxXYZ;
            XYZ offset = new XYZ(10, 10, 10);

            // process the first baseline/wall
            if (Log.ProjectBoundingBox.Count < 2)
            {
                points = new List<XYZ> { 
                    baseline.GetEndPoint(0),
                    baseline.GetEndPoint(1) };

                minXYZ = new XYZ(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z));
                maxXYZ = new XYZ(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z));

                Log.ProjectBoundingBox.Add(minXYZ - offset);
                Log.ProjectBoundingBox.Add(maxXYZ + offset);
            }
            // process the rest of the baselines/walls
            else
            {
                points = new List<XYZ> {
                    Log.ProjectBoundingBox[0] + offset,
                    Log.ProjectBoundingBox[1] - offset,
                    baseline.GetEndPoint(0),
                    baseline.GetEndPoint(1) };

                minXYZ = new XYZ(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z));
                maxXYZ = new XYZ(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z));

                Log.ProjectBoundingBox[0] = minXYZ - offset;
                Log.ProjectBoundingBox[1] = maxXYZ + offset;
            }
        }

        /// <summary>
        /// Searches through the Apartment IDs that have been logged from the JSON file and calls the CreateLinkInstance method to create 
        /// an instance of the link.
        /// </summary>
        public bool CreateApartmentInstance(Document doc)
        {
            if (Log.LoadedApartments.TryGetValue(Name, out ElementId apartmentId))
            {
                CreateLinkInstance(doc, apartmentId);
                return true;
            }
            else if (Log.ApartmentFiles.TryGetValue(Name, out string apartmentFile))
            {
                apartmentId = Util.LoadRevitLink(doc, apartmentFile);
                Log.LoadedApartments[Name] = apartmentId;
                CreateLinkInstance(doc, apartmentId);
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Searches through the Building IDs that have been logged from the JSON file and calls the CreateBuildingLinkInstance 
        /// method to create an instance of the link.
        /// NB : If we assume that each building will be it's own RevitLinkType, the two methods can merge? 
        /// </summary>
        public bool CreateBuildingInstance(Document doc)
        {
            if (Log.LoadedBuildings.TryGetValue(Name, out ElementId buildingId))
            {
                CreateBuildingLinkInstance(doc, buildingId);
                return true;
            }
            else if (Log.BuildingFiles.TryGetValue(Name, out string buildingFile))
            {
                buildingId = Util.LoadRevitLink(doc, buildingFile);
                Log.LoadedBuildings[Name] = buildingId;
                CreateBuildingLinkInstance(doc, buildingId);
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Place Revit Link Instances, Rotate around Origin then move to Correct Placement
        /// </summary>
        public void CreateLinkInstance(Document doc, ElementId linkTypeId)
        {
            // Create revit link instance at origin
            RevitLinkInstance instance = RevitLinkInstance.Create(doc, linkTypeId);
            Location location = instance.Location;

            // rotate the units to the correct orientation
            double angle = (Rot[1] * Util.ToRadians) - Log.SiteData[2];
            Line axis = Line.CreateUnbound(XYZ.Zero, XYZ.BasisZ);
            location.Rotate(axis, angle);

            // move units to correct position
            XYZ position = Util.ApplyParentTransforms(this, PosVec).MetersToFeet();
            position = Util.RotateByAngleToNorth(position);
            location.Move(position);

            // Set Building Name Parameter
            Util.SetBuildingNameParameter(doc, this, instance);
        }

        /// <summary>
        /// Place Revit Link Instances and Rotate around Origin by Angle to True North
        /// </summary>
        public void CreateBuildingLinkInstance(Document doc, ElementId linkTypeId)
        {
            // Create revit link instance at origin
            RevitLinkInstance instance = RevitLinkInstance.Create(doc, linkTypeId);

            // get Angle To True North
            Document linkedDoc = instance.GetLinkDocument();
            ProjectPosition prjPosition = linkedDoc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            double angle = prjPosition.Angle;

            // rotate the units to the correct orientation
            Location location = instance.Location;
            Line axis = Line.CreateUnbound(XYZ.Zero, XYZ.BasisZ);
            location.Rotate(axis, angle);

            // expand ProjectBoundingBox to contain building height
            Level topLevel = new FilteredElementCollector(linkedDoc).OfClass(typeof(Level)).Cast<Level>().Last();
            double offset = 10;
            double topHeight = Math.Max(Log.ProjectBoundingBox[1].Z, topLevel.Elevation + offset);
            Log.ProjectBoundingBox[1] = new XYZ(Log.ProjectBoundingBox[1].X, Log.ProjectBoundingBox[1].Y, topHeight);
        }

        /// <summary>
        /// Create a Revit Wall on the given <see cref="Document"/>. After creating the wall, it attempts to
        /// set its top reference to an existing level.
        /// It relies on the expected properties and types to exist on the target document
        /// </summary>
        /// <param name="doc"></param>
        private void CreateWall(Document doc)
        {
            WallType wallType = GetElementTypeByProperties() as WallType;
            if (wallType == null) return;

            Line baseline = Util.CreateHorizontalCenterLine(this);
            if (baseline == null) return;

            double elevation;
            Level baseLevel;
            double offset;

            // Check for Parent Level
            if (_singleParents.Contains(Parent.Type)) elevation = Log.UsedLevels.FirstOrDefault(x => x.Key == Parent.Data.Level).Key;
            else elevation = Log.UsedLevels.FirstOrDefault(x => x.Key == Parent.Parent.Data.Level).Key;

            // Get level and evaluate offset
            ( baseLevel, offset) = GetLevelAndOffsetFromParent(doc, new XYZ(0, 0, 0));

            if (baseLevel == null)
            {
                double parentElevation = Util.ApplyParentTransforms(this, (Parent.PosVec + Parent.SizeVec.MultiplyVector(Parent.AnchorVec))).Z.MetersToFeet();
                Level parentLevel = GetLevelByElevation(doc, parentElevation);
                if (parentLevel == null) return;
                offset = elevation - parentElevation;
                baseLevel = parentLevel;
            }

            if (baseline.Length > 0.1.MetersToFeet())
            {
                Wall wall = Wall.Create(doc, baseline, wallType.Id, baseLevel.Id, SizeVec.Z.MetersToFeet(), 0, false, false);
                wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(offset);

                // Set Building Name Parameter
                Util.SetBuildingNameParameter(doc, this, wall);

                Instance = wall;

                Log.AddToCreated(this);

                if (Type == "Facade" || Type == "Parapet")
                {
                    AddToProjectBoundingBox(baseline);
                    Log.FacadeWalls.Add(wall);
                }
                else
                {
                    WallUtils.DisallowWallJoinAtEnd(wall, 0);
                    WallUtils.DisallowWallJoinAtEnd(wall, 1);
                    Log.InternalWalls.Add(wall);

                    // check for internal corridor walls
                    if (Children.Length == 1 && Children[0].Type == "Door" && Parent.Type == "Corridor")
                    {
                        wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).Set(0);
                    }
                }    
            }
        }

        /// <summary>
        /// Create a Rectangular Revit Floor on the given <see cref="Document"/>. It relies on the expected properties and types to exist on the target document
        /// </summary>
        /// <param name="doc"></param>
        private void CreateFloor(Document doc)
        {
            FloorType floorType = GetElementTypeByProperties() as FloorType;
            if (floorType == null) return;

            CurveArray profile = Util.CreateHorizontalRectangle(this);
            (Level baseLevel, double offset) = GetLevelAndOffsetFromParent(doc, new XYZ(0, 0, 1));

#if Revit2022
            CurveLoop loop = new CurveLoop();
            foreach (Curve curve in profile)
            {
                loop.Append(curve);
            }
            Floor floor = Floor.Create(doc, new List<CurveLoop> {loop}, floorType.Id, baseLevel.Id, false, null, 0.0);
#else
            Floor floor = doc.Create.NewFloor(profile, floorType, baseLevel, false);
            #endif

            floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(offset);

            //// Set Building Name Parameter
            Util.SetBuildingNameParameter(doc, this, floor);

            Instance = floor;

            Log.AddToCreated(this);
        }

        /// <summary>
        /// Create a Rectangular Rouge Ceiling as a Revit Roof on the given <see cref="Document"/>. 
        /// It relies on the expected properties and types to exist on the target document. Unable of creating a Revit
        /// Ceiling at the current APIs.
        /// </summary>
        /// <param name="doc"></param>
        private void CreateCeiling(Document doc)
        {
            ElementType ceilingType = GetElementTypeByProperties();
            if (ceilingType == null) return;

            CurveArray profile = Util.CreateHorizontalRectangle(this);

            (Level baseLevel, double offset) = GetLevelAndOffsetFromParent(doc, new XYZ(0, 0, 0));
            if (baseLevel == null) return;

            if (ceilingType.GetType() == typeof(RoofType))
            {
                RoofType rt = ceilingType as RoofType;
                ModelCurveArray mc = new ModelCurveArray();
                FootPrintRoof ceiling = doc.Create.NewFootPrintRoof(profile, baseLevel, rt, out mc);
                ceiling.get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM).Set(offset);

                //// Set Building Name Parameter
                Util.SetBuildingNameParameter(doc, this, ceiling);

                Instance = ceiling;
            }
            else if (ceilingType.GetType() == typeof(CeilingType))
            {
                string err = "Tried to create a Ceiling element using a Ceiling family. The current Revit API does not support the creation of ceilings";
                if (!Log.ErrorMsgs.Contains(err)) Log.ErrorMsgs.Add(err);
                Instance = null;
            }

            Log.AddToCreated(this);
        }

        /// <summary>
        /// Create a Rectangular Rouge Roof as a Revit Roof on the given <see cref="Document"/>.
        /// It relies on the expected properties and types to exist on the target document.
        /// </summary>
        /// <param name="doc"></param>
        private void CreateRoof(Document doc)
        {
            RoofType roofType = GetElementTypeByProperties() as RoofType;
            if (roofType == null) return;

            // create Polygon profile from polygon array.
            CurveArray profile = Util.CreatePolygon(this, this.Data.Polygon);

            (Level baseLevel, double offset) = GetLevelAndOffsetFromParent(doc, new XYZ(0, 0, 0));
            if (baseLevel == null) return;

            ModelCurveArray mc = new ModelCurveArray();
            FootPrintRoof roof = doc.Create.NewFootPrintRoof(profile, baseLevel, roofType, out mc);
            roof.get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM).Set(offset);

            //// Set Building Name Parameter
            Util.SetBuildingNameParameter(doc, this, roof);

            Instance = roof;

            Log.AddToCreated(this);
        }

        /// <summary>
        /// Create an instance of a family in Revit on the given <see cref="Document"/>, based on 
        /// the input category and the element's ModulousId. It relies on the expected properties 
        /// and types to exist on the target document.
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="category"></param>
        private void CreateFamilyInstance(Document doc)
        {
            FamilySymbol symbol = GetSymbolByProperties();
            if (symbol == null) return;

            // need to activate symbol otherwise throws an Symbol is not loaded exception
            if (!symbol.IsActive)
            { symbol.Activate(); doc.Regenerate(); }

            XYZ position = Util.GetAbsolutePosition(this);
            position = Util.RotateByAngleToNorth(position);

            var levelId = Parent.Instance.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId();
            var level = doc.GetElement(levelId) as Level;

            Instance = doc.Create.NewFamilyInstance(position, symbol, Parent.Instance, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            //// Set Building Name Parameter
            Util.SetBuildingNameParameter(doc, this, Instance);

            Log.AddToCreated(this);
        }

        /// <summary>
        ///  Creates Room Object based on RGE Room object location
        /// </summary>
        /// <param name="doc"></param>
        private void CreateRoom(Document doc)
        {
            XYZ centre = Util.ApplyParentTransforms(this, Util.EvaluatePivot(this, new XYZ(0.5, 0.5, 0))).MetersToFeet();
            centre = Util.RotateByAngleToNorth(centre);

            // offset from center to avoid walls
            double uncentre = 0.5;
            UV centre2D = new UV(centre.X + uncentre.MetersToFeet(), centre.Y + uncentre.MetersToFeet());

            // get the host level from the Log
            int index = (int)Data.Level;
            Level level = Log.UsedLevels.ElementAt(index).Value;

            if (!level.Name.Contains("Roof"))
            {
                // create a new room
                Room room = doc.Create.NewRoom(level, centre2D);
                if (null == room)
                {
                    throw new Exception("Create a " +
                        "new room failed.");
                }

                // make sure name starts with upper
                string roomName = char.ToUpper(Type[0]) + Type.Substring(1);

                room.Name = roomName;
                try
                {
                    room.LookupParameter("Room Type").Set(roomName);
                    room.LookupParameter("Apartment Type").Set(roomName);
                }
                catch { }

                // log room in ApartmentTypes so that it can be included in plan legend
                string elev = Pos[1].ToString();
                if (!Log.ApartmentTypes.ContainsKey(elev)) Log.ApartmentTypes[elev] = new List<string>();
                if (!Log.ApartmentTypes[elev].Contains(roomName)) Log.ApartmentTypes[elev].Add(roomName);

                // temp method to turn off room bounding of walls inside technical rooms
                // suite team to remove those wall from the aggregator after webinar
                CreateRoomPolygons(doc);
            }
        }

        /// <summary>
        /// Creates an instance of <see cref="Autodesk.Revit.DB.Opening"/> in the project
        /// </summary>
        /// <param name="doc"></param>
        private void CreateOpening(Document doc)
        {
            // Call the Wall hosting object into this method           
            Wall wall = doc.GetElement(this.Parent.Instance.Id) as Wall;

            // Get the offset value for each level
            double offset = 0;
            if (this.Parent.Type != "Facade") offset = - this.PosVec.Z;

            // get the minimum pivot
            XYZ p1 = Util.EvaluatePivot(this, new XYZ(0, 0, 0));
            p1 = Util.ApplyParentTransforms(this, p1);
            p1 = Util.RotateByAngleToNorth(p1);
            XYZ p1offset = new XYZ(p1.X, p1.Y, p1.Z + offset).MetersToFeet();

            // get the maximum pivot
            XYZ p2 = Util.EvaluatePivot(this, new XYZ(1, 1, 1));
            p2 = Util.ApplyParentTransforms(this, p2);
            p2 = Util.RotateByAngleToNorth(p2);
            XYZ p2offset = new XYZ(p2.X, p2.Y, p2.Z + offset).MetersToFeet();

            Opening opening = doc.Create.NewOpening(wall, p1offset, p2offset);
            Log.AddToCreated(this);
        }

        public static void CreateCommunalWalls(Document doc)
        {
            foreach (var kvp in Log.CommunalWallData)
            {
                string spaceId = kvp.Key;
                List<Curve> baseCurves = kvp.Value.Select(el => el.OriginCurve).ToList();
                List<ElementId> wallTypeIds = kvp.Value.Select(el => el.TypeId).ToList();
                List<ElementId> levelIds = kvp.Value.Select(el => el.LevelId).ToList();
                List<double> heights = kvp.Value.Select(el => el.Height).ToList();
                List<double> baseOffsets = kvp.Value.Select(el => el.BaseOffset).ToList();
                List<RougeElement> rgElements = kvp.Value.Select(el => el.RougeElement).ToList();

                // if the shape is complex
                if (baseCurves.Count > 0)
                {
                    // extend the base curves
                    for (int i = baseCurves.Count - 1; i >= 0; i--)
                    {
                        Curve extendedCurve = Util.ExtendCurve(baseCurves[i], 0.4.MetersToFeet());
                        baseCurves.RemoveAt(i);
                        baseCurves.Insert(i, extendedCurve);
                    }

                    // merge the extended curves
                    List<Curve> temp = new List<Curve>(); // create disposable temp list
                    while (baseCurves.Count > 0)
                    {
                        List<Curve> curvesToMerge = new List<Curve> { baseCurves[baseCurves.Count - 1] };
                        baseCurves.RemoveAt(baseCurves.Count - 1);

                        for (int i = baseCurves.Count - 1; i >= 0; i--)
                        {
                            if (Util.AreCurvesOverlapping(curvesToMerge[curvesToMerge.Count - 1], baseCurves[i]))
                            {
                                curvesToMerge.Add(baseCurves[i]);
                                baseCurves.RemoveAt(i);
                            }
                        }

                        if (curvesToMerge.Count > 1)
                        {
                            Curve mergedCurve = Util.MergeColinearCurves(curvesToMerge);
                            temp.Add(mergedCurve);
                        }
                        else if (curvesToMerge.Count == 1)
                        {
                            temp.Add(curvesToMerge[0]);
                        }
                    }
                    baseCurves = temp;

                    // trim the extended curves and remove interior segments
                    temp = new List<Curve>(); // reset the disposable list
                    XYZ centroid = Util.GetCentroid(baseCurves);
                    foreach (Curve curve in baseCurves)
                    {
                        List<XYZ> iPoints = Util.GetIntersectionPoints(curve, baseCurves);

                        for (int i = 0; i < iPoints.Count - 1; i++)
                        {
                            Line trimmedLine = Line.CreateBound(iPoints[i], iPoints[i + 1]);
                            if (trimmedLine.Distance(centroid).FeetToMeters() > 1.0)
                            {
                                Curve trimmedCurve = trimmedLine as Curve;
                                temp.Add(trimmedCurve);
                            }
                        }
                    }
                    baseCurves = temp;

                    //// order the trimmed curves
                    baseCurves = Util.OrderCurves(baseCurves, 0.001.MetersToFeet(), false);

                    // merge curve again  
                    temp = new List<Curve>(); // reset the disposable list
                    while (baseCurves.Count > 0)
                    {
                        // create a temp list
                        List<Curve> curvesToMerge = new List<Curve> { baseCurves[baseCurves.Count - 1] };
                        baseCurves.RemoveAt(baseCurves.Count - 1);

                        for (int i = baseCurves.Count - 1; i >= 0; i--)
                        {
                            if (Util.AreCurvesParallel(curvesToMerge[curvesToMerge.Count - 1], baseCurves[i]))
                            {
                                curvesToMerge.Add(baseCurves[i]);
                                baseCurves.RemoveAt(i);
                            }
                            else break;
                        }

                        if (curvesToMerge.Count > 1)
                        {
                            Curve mergedCurve = Util.MergeColinearCurves(curvesToMerge);
                            temp.Add(mergedCurve);
                        }
                        else if (curvesToMerge.Count == 1)
                        {
                            temp.Add(curvesToMerge[0]);
                        }
                    }
                    baseCurves = temp;
                }

                // build the new walls
                for (int i = 0; i < baseCurves.Count; i++)
                {
                    Wall wall = Wall.Create(doc, baseCurves[i], wallTypeIds[i], levelIds[i], heights[i], 0, false, false);
                    wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(baseOffsets[i]);

                    // log the wall
                    if (!Log.CommunalWalls.ContainsKey(spaceId)) Log.CommunalWalls[spaceId] = new List<Wall>();
                    Log.CommunalWalls[spaceId].Add(wall);

                    // set Building Name parameter
                    Util.SetBuildingNameParameter(doc, rgElements[i], wall);
                    Log.AddToCreated(rgElements[i]);
                }
            }
        }

        public static void CreateCommunalDoors(Document doc)
        {
            foreach (var kvp in Log.CommunalDoorData)
            {
                string spaceId = kvp.Key;
                List<XYZ> positions = kvp.Value.Select(el => el.OriginPoint).ToList();
                List<Element> doorTypes = kvp.Value.Select(el => doc.GetElement(el.TypeId)).ToList();
                List<RougeElement> rgElements = kvp.Value.Select(el => el.RougeElement).ToList();
                Wall hostWall = null;

                for (int i = 0; i < positions.Count; i++)
                {
                    double minDistance = double.MaxValue;

                    // find the closest wall
                    List<Wall> walls = Log.CommunalWalls[spaceId];
                    foreach (Wall wall in walls)
                    {
                        LocationCurve locCurve = wall.Location as LocationCurve;
                        double currenrDistance = locCurve.Curve.Distance(positions[i]);
                        if (currenrDistance < minDistance)
                        {
                            minDistance = currenrDistance;
                            hostWall = wall;
                        }
                    }
                    Level level = doc.GetElement(hostWall.LevelId) as Level;

                    // create the door
                    FamilySymbol doorType = doorTypes[i] as FamilySymbol;

                    FamilyInstance door = doc.Create.NewFamilyInstance
                        (positions[i], doorType, hostWall, level,
                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                    // set Building Name parameter
                    Util.SetBuildingNameParameter(doc, rgElements[i], door);
                    Log.AddToCreated(rgElements[i]);
                }
            }
        }
        #endregion

        #region Site Creation

        /// <summary>
        /// Goes through each Rouge Element and collects site data
        /// </summary>
        /// <param name="doc"></param>
        public void PreProcessData(Document doc)
        {
            //// give default angle value
            //Log.ApartmentRotations.Add(0);

            if (Type == "Site")
            {
                Log.SiteData.Add(Data.Latitude);
                Log.SiteData.Add(Data.Longitude);
                Log.SiteBoundary = Data.Polygon;
            }
            else if (Type == "Building" && Children.Length == 0)
            {
                Log.ApartmentRotations.Add(0);

                // start building the ProjectBoundingBox for the site model
                XYZ offset = new XYZ(10, 10, 10);
                if (Log.ProjectBoundingBox.Count == 0)
                {
                    List<double> xPoints = Log.SiteBoundary
                        .Select(p => p[0]).ToList();

                    var yPoints = Log.SiteBoundary
                        .Select(p => -p[2]).ToList();

                    Log.ProjectBoundingBox.Add(new XYZ(xPoints.Min().MetersToFeet(), yPoints.Min().MetersToFeet(), 0) - offset);
                    Log.ProjectBoundingBox.Add(new XYZ(xPoints.Max().MetersToFeet(), yPoints.Max().MetersToFeet(), 0) + offset);
                }
            }
            else if (Type == "Apartment")
            {
                double angle = Rot[1] % 90;
                if (!Log.ApartmentRotations.Contains(angle)) Log.ApartmentRotations.Add(angle);
            }

            if (Children.Length > 0)
            {
                foreach (RougeElement child in Children)
                {
                    child.Parent = this;
                    child.PreProcessData(doc);
                }
            }
        }

        /// <summary>
        /// Moves the Survey Point and rotates the Project Base Point
        /// Elevation of the Project Base Pint and all new levels should be revisited
        /// </summary>
        /// <param name="doc"></param> 
        public static void SetSiteCoordinates(Document doc)
        {
            FilteredElementCollector points = new FilteredElementCollector(doc).OfClass(typeof(BasePoint));

            // calculate angle to north
            double angleToNorth = Log.SiteData[2];

            // get the latitude and longitude
            double lat = Log.SiteData[0];
            double lon = Log.SiteData[1];

            XYZ translation = new XYZ(-lat, -lon, 0);
            foreach (Element e in points)
            {
                BasePoint p = e as BasePoint;
                if (p.IsShared) // survey point
                {
#if !Revit2020
                    p.Clipped = true;
#endif
                    p.Location.Move(translation);
                }
                else // project base point
                {
                    p.get_Parameter(BuiltInParameter.BASEPOINT_ANGLETON_PARAM).Set(-angleToNorth);
                }
            }
        }

        /// <summary>
        /// Creates Model Lines to represent the Property lines
        /// </summary>
        public static void CreateSiteBoundary(Document doc)
        {
            // get default line style
            Category lineCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            Category propLineCat = lineCat.SubCategories.get_Item("<Wide Lines>");
            GraphicsStyle propLineStyle = propLineCat.GetGraphicsStyle(GraphicsStyleType.Projection);

            // get the "Property line" line style
            try
            {
                propLineCat = lineCat.SubCategories.get_Item("Property Lines");
                propLineStyle = propLineCat.GetGraphicsStyle(GraphicsStyleType.Projection);
            }
            catch { }

            // create each segment of the property lines
            List<XYZ> points = new List<XYZ>();
            foreach (var p in Log.SiteBoundary)
            {
                if (p.Length == 3)
                {
                    points.Add(new XYZ(p[0].MetersToFeet(), -p[2].MetersToFeet(), p[1].MetersToFeet()));
                }
            }
            if (points.Count >= 2)
            {
                for (int i = 0; i < (points.Count - 1); i++)
                {
                    XYZ start = points[i];
                    XYZ end = points[(i + 1)];
                    start = Util.RotateByAngleToNorth(start);
                    end = Util.RotateByAngleToNorth(end);

                    Line line = Line.CreateBound(start, end);
                    ModelCurve propLine = doc.Create.NewModelCurve(line, SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero)));
                    if (propLineStyle != null)
                    {
                        propLine.LineStyle = propLineStyle;
                    }
                }
            }
        }

        /// <summary>
        /// Creates model lines around the building to set the Levels extent
        /// </summary>
        /// <param name="doc"></param>
        public static void CreateScopeLines(Document doc)
        {
            // find min and max values
            if (Log.ProjectBoundingBox.Count != 0)
            {
                double minX = Log.ProjectBoundingBox[0].X;
                double minY = Log.ProjectBoundingBox[0].Y;
                double maxX = Log.ProjectBoundingBox[1].X;
                double maxY = Log.ProjectBoundingBox[1].Y;

                // construct scope lines
                List<Line> lines = new List<Line>
                {
                Line.CreateBound(new XYZ(minX, minY, 0), new XYZ(maxX, minY, 0)),
                Line.CreateBound(new XYZ(maxX, minY, 0), new XYZ(maxX, maxY, 0)),
                Line.CreateBound(new XYZ(maxX, maxY, 0), new XYZ(minX, maxY, 0)),
                Line.CreateBound(new XYZ(minX, maxY, 0), new XYZ(minX, minY, 0))
                };

                Category lineCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                Category propLineCat = lineCat.SubCategories.get_Item("Scope Lines");
                GraphicsStyle propLineStyle = propLineCat.GetGraphicsStyle(GraphicsStyleType.Projection);

                SketchPlane plane = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));

                foreach (Line line in lines)
                {
                    ModelCurve curve = doc.Create.NewModelCurve(line, plane);
                    curve.LineStyle = propLineStyle;
                }
            }
        }

        #endregion

        private void CollectCommunalWallData(Document doc)
        {
            string spaceId = string.Empty;
            if (Parent.Type == "Module") spaceId = Parent.Parent.Uuid;
            else spaceId = Parent.Uuid;

            WallType wallType = GetElementTypeByProperties() as WallType;
            if (wallType == null) return;

            Line baseline = Util.CreateHorizontalCenterLine(this);
            if (baseline == null) return;

            double elevation;
            Level baseLevel;
            double offset;

            // Check for Parent Level
            if (_singleParents.Contains(Parent.Type)) elevation = Log.UsedLevels.FirstOrDefault(x => x.Key == Parent.Data.Level).Key;
            else elevation = Log.UsedLevels.FirstOrDefault(x => x.Key == Parent.Parent.Data.Level).Key;

            // Get level and evaluate offset
            (baseLevel, offset) = GetLevelAndOffsetFromParent(doc, new XYZ(0, 0, 0));

            ComWallData wallData = new ComWallData();
            wallData.SpaceId = spaceId;
            wallData.OriginCurve = baseline;
            wallData.TypeId = wallType.Id;
            wallData.LevelId = baseLevel.Id;
            wallData.Height = SizeVec.Z.MetersToFeet();
            wallData.BaseOffset = offset;
            wallData.RougeElement = this;

            if (!Log.CommunalWallData.ContainsKey(spaceId)) Log.CommunalWallData[spaceId] = new List<ComWallData>();
            Log.CommunalWallData[spaceId].Add(wallData);
        }
                
        private void CollectCommunalDoorData(Document doc)
        {
            string spaceId = string.Empty;
            if (Parent.Parent.Type == "Module") spaceId = Parent.Parent.Parent.Uuid;
            else spaceId = Parent.Parent.Uuid;

            FamilySymbol symbol = GetSymbolByProperties();
            if (symbol == null) return;

            // need to activate symbol otherwise throws an Symbol is not loaded exception
            if (!symbol.IsActive)
            { symbol.Activate(); doc.Regenerate(); }

            XYZ position = Util.GetAbsolutePosition(this);
            position = Util.RotateByAngleToNorth(position);

            ComWallData doorData = new ComWallData();
            doorData.SpaceId = spaceId;
            doorData.TypeId = symbol.Id;
            doorData.OriginPoint = position;
            doorData.RougeElement = this;

            if (!Log.CommunalDoorData.ContainsKey(spaceId)) Log.CommunalDoorData[spaceId] = new List<ComWallData>();
            Log.CommunalDoorData[spaceId].Add(doorData);
        }
               
        private void SetProjectName(Document doc)
        {
            ProjectInfo info = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ProjectInformation)
                .Cast<ProjectInfo>().First();

            info.get_Parameter(BuiltInParameter.PROJECT_NAME).Set(this.Name);
        }

        /// <summary>
        /// Joins all external walls
        /// </summary>
        /// <param name="doc"></param>
        public static void JoinFacadeWalls(Document doc)
        {
            for (int i = 0; i < Log.FacadeWalls.Count(); i++)
            {
                Wall wall = Log.FacadeWalls[i];
                BoundingBoxXYZ wallBB = wall.get_BoundingBox(null);
                if (wallBB != null)
                {
                    for (int j = i + 1; j < Log.FacadeWalls.Count(); j++)
                    {
                        Wall anotherWall = Log.FacadeWalls[j];
                        BoundingBoxXYZ anotherBB = anotherWall.get_BoundingBox(null);
                        if (anotherBB != null)
                        {
                            bool xIntersect = (wallBB.Max.X >= anotherBB.Min.X) && (wallBB.Min.X <= anotherBB.Max.X);
                            bool yIntersect = (wallBB.Max.Y >= anotherBB.Min.Y) && (wallBB.Min.Y <= anotherBB.Max.Y);
                            bool zIntersect = (wallBB.Max.Z >= anotherBB.Min.Z) && (wallBB.Min.Z <= anotherBB.Max.Z);

                            if (xIntersect && yIntersect && zIntersect)
                            {
                                try
                                {
                                    JoinGeometryUtils.JoinGeometry(doc, wall, anotherWall);
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Joins all internal walls
        /// </summary>
        /// <param name="doc"></param>
        public static void JoinInternalWalls(Document doc)
        {
            for (int i = 0; i < Log.InternalWalls.Count(); i++)
            {
                Wall wall = Log.InternalWalls[i];
                if (wall != null)
                {
                    for (int j = i + 1; j < Log.InternalWalls.Count(); j++)
                    {
                        Wall anotherWall = Log.InternalWalls[j];
                        if (anotherWall != null)
                        {
                            try
                            {
                                JoinGeometryUtils.JoinGeometry(doc, wall, anotherWall);
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set level extents to fit geometry
        /// </summary>
        /// <param name="doc"></param>
        public static void MaximizeLevelExtents(Document doc)
        {
            List<Level> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            foreach (Level level in levels)
            {
                level.Maximize3DExtents();
            }
        }

        #region Document retrieving

        /// <summary>
        /// Retrieves a <see cref="Autodesk.Revit.DB.FamilySymbol"/> of this, using
        /// its <see cref="CodeCategory"/> and <see cref="CodeType"/>
        /// </summary>
        /// <returns></returns>
        private FamilySymbol GetSymbolByProperties()
        {
            if (!Log.AvailableSymbols.TryGetValue($"{CodeCategory}|{CodeType}", out FamilySymbol elementSymbol))
            {
                string err = $"Type of {Type} with Code: Category {CodeCategory} and Code: Type {CodeType} does not exist. Element: {Uuid}";
                if (!Log.ErrorMsgs.Contains(err)) Log.ErrorMsgs.Add(err);
            }

            return elementSymbol;
        }

        /// <summary>
        /// Retrieves a <see cref="Autodesk.Revit.DB.ElementType"/> of this, using
        /// its <see cref="CodeCategory"/> and <see cref="CodeType"/>
        /// </summary>
        /// <returns></returns>
        private ElementType GetElementTypeByProperties()
        {
            if (!Log.AvailableTypes.TryGetValue($"{CodeCategory}|{CodeType}", out ElementType elementType))
            {
                string err = $"Type of {Type} with Code: Category {CodeCategory} and Code: Type {CodeType} does not exist. Element: {Uuid}";
                if (!Log.ErrorMsgs.Contains(err)) Log.ErrorMsgs.Add(err);
            }

            return elementType;
        }

        /// <summary>
        /// Retrieves a Level from the project that matches the input elevation. The elevation is
        /// expected to be in Revit internal units
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="elevation"></param>
        /// <returns></returns>
        private Level GetLevelByElevation(Document doc, double levelElevation, bool throwError = true)
        {
            Level level = Log.UsedLevels[levelElevation];
            if (level != null) return level;
            else
            {
                var Levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>();

                level = Levels.FirstOrDefault(l => l.Elevation.Compare(levelElevation));

                //Check if base level exists
                if (level != null) Log.UsedLevels.Add(levelElevation, level);
                else if (throwError)
                {
                    string err = $"Level with elevation {levelElevation.FeetToMeters()} does not exist. Element: {Uuid}";
                    if (!Log.ErrorMsgs.Contains(err)) Log.ErrorMsgs.Add(err);
                }

            }

            return level;
        }

        /// <summary>
        /// Retrieves a Level and an offset (already in Revit's internal units) to use as the vertical axis positioning of the element.
        /// </summary>
        /// <param name="doc"></param>
        /// <returns>A Level and double tuple</returns>
        private (Level, double) GetLevelAndOffsetFromParent(Document doc, XYZ pivot)
        {
            double elevation;
            // Check for Parent Level 
            if (_singleParents.Contains(Parent.Type)) elevation = Log.UsedLevels.FirstOrDefault(x => x.Key == Parent.Data.Level).Key;
            else elevation = Log.UsedLevels.FirstOrDefault(x => x.Key == Parent.Parent.Data.Level).Key;

            Level baseLevel = GetLevelByElevation(doc, elevation, false);

            // evaluate pivot
            double worldHeight = Util.ApplyParentTransforms(this, Util.EvaluatePivot(this, pivot)).Z.MetersToFeet();
            double offset = worldHeight - baseLevel.Elevation;

            // get the relative Revit level
            var result = Log.UsedLevels
                .Select(kvp => new { Elevation = kvp.Key, Gap = Math.Abs(worldHeight - kvp.Key), Level = kvp.Value })
                .OrderBy(item => item.Gap)
                .First();

            baseLevel = result.Level;
            offset = worldHeight - result.Elevation;

            // if baseLevel in snot fount default to parent level
            if (baseLevel == null)
            {
                double parentElevation = Util.ApplyParentTransforms(this, (Parent.PosVec + Parent.SizeVec.MultiplyVector(Parent.AnchorVec))).Z.MetersToFeet();
                Level parentLevel = GetLevelByElevation(doc, parentElevation);
                if (parentLevel == null) return (null, 0);
                offset = elevation - parentElevation;
                baseLevel = parentLevel;
            }

            return (baseLevel, offset);
        }

        #endregion

        #region Rouge operations

        /// <summary>
        /// Gets the <see cref="BoundingBoxXYZ"/> of this apartment that contains the children of the 
        /// specified types
        /// </summary>
        /// <param name="targetBB"></param>
        /// <param name="types"></param>
        /// <returns>The bounding box local to the apartment</returns>
        private BoundingBoxXYZ GetAptChildrenBoundingBox(BoundingBoxXYZ targetBB, string[] types)
        {
            if (types.Contains(Type))
            {
                targetBB.ExpandToContain(this);
            }
            foreach (var child in Children)
            {
                if (child.Parent == null && Type != "Apartment") child.Parent = this;
                child.GetAptChildrenBoundingBox(targetBB, types);
            }

            return targetBB;
        }

        /// <summary>
        /// Gets the deep children of this, using a query to decide which one should be taken
        /// </summary>
        /// <param name="query"></param>
        /// <param name="target"></param>
        public void GetDeepChildrenWithQuery(Func<RougeElement, bool> query, List<RougeElement> target)
        {
            var filtered = Children.Where(c => query(c));
            target.AddRange(filtered);
            foreach (var child in Children) child.GetDeepChildrenWithQuery(query, target);
        }

        #endregion

    }


}
