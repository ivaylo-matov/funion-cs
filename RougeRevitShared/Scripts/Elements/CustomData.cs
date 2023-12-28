using Autodesk.Revit.DB;
using System;

namespace RougeRevit
{
    
    public class CustomData
    {
        public RougeLevels[] Levels;
        public double Level;
        public string Category;
        public double[][] Polygon;
        public Boolean DtoMuscleUnit;
        public double Latitude;
        public double Longitude;
    }

    public class RougeLevels
    {
        public string Id;
        public string UuId;
        public double FloorWorldBottom;
        public double FFL;
        public string LevelConfig;
        public string levelIndex;
        public bool IsRoof;
        public double[][] GEAPolygon;
        public double[][] GIAPolygon;
    }

    public class ComWallData
    {
        public string SpaceId;
        public Curve OriginCurve;
        public XYZ OriginPoint;
        public ElementId TypeId;
        public ElementId LevelId;
        public double Height;
        public double BaseOffset;
        public RougeElement RougeElement;
    }
}