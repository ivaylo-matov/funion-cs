using Autodesk.Revit.DB;
using RougeRevit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RougeRevit
{
    public static class Ext
    {
        /// <summary>
        /// Expand to Contain Utility Class Example takne from: https://github.com/jeremytammik/the_building_coder_samples/blob/master/BuildingCoder/Util.cs
        /// Make this bounding box empty by setting the
        /// Min value to plus infinity and Max to minus.
        /// </summary>
        public static void Clear(this BoundingBoxXYZ bb)
        {
            var infinity = double.MaxValue;
            bb.Min = new XYZ(infinity, infinity, infinity);
            bb.Max = -bb.Min;
        }

        /// <summary>
        /// Expand the given bounding box to include
        /// and contain the given point.
        /// </summary>
        public static void ExpandToContain(this BoundingBoxXYZ bb, XYZ p)
        {
            bb.Min = new XYZ(
                Math.Min(bb.Min.X, p.X),
                Math.Min(bb.Min.Y, p.Y),
                Math.Min(bb.Min.Z, p.Z));

            bb.Max = new XYZ(
                Math.Max(bb.Max.X, p.X),
                Math.Max(bb.Max.Y, p.Y),
                Math.Max(bb.Max.Z, p.Z));
        }

        /// <summary>
        /// Expand this bounding box to include the given <see cref="RougeElement"/>
        /// </summary>
        /// <param name="bb"></param>
        /// <param name="obj"></param>
        public static void ExpandToContain(this BoundingBoxXYZ bb, RougeElement obj)
        {
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        XYZ pivot = Util.ApplyParentTransforms(obj, Util.EvaluatePivot(obj, new XYZ(x, y, z)));
                        bb.ExpandToContain(pivot);
                    }
                }
            }
        }

        /// <summary>
        /// Expand the given bounding box to include
        /// and contain the given points.
        /// </summary>
        public static void ExpandToContain(this BoundingBoxXYZ bb, IEnumerable<XYZ> pts)
        {
            bb.ExpandToContain(new XYZ(
                pts.Min<XYZ, double>(p => p.X),
                pts.Min<XYZ, double>(p => p.Y),
                pts.Min<XYZ, double>(p => p.Z)));

            bb.ExpandToContain(new XYZ(
                pts.Max<XYZ, double>(p => p.X),
                pts.Max<XYZ, double>(p => p.Y),
                pts.Max<XYZ, double>(p => p.Z)));
        }

        /// <summary>
        /// Expand the given bounding box to include
        /// and contain the given other one.
        /// </summary>
        public static void ExpandToContain(this BoundingBoxXYZ bb, BoundingBoxXYZ other)
        {
            bb.ExpandToContain(other.Min);
            bb.ExpandToContain(other.Max);
        }
    }

}
