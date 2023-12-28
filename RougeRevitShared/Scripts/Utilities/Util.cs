using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using Autodesk.Revit.DB.Architecture;
#if Revit2020||Revit2021||Revit2022
using Autodesk.Revit.UI.Selection;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using Amazon.S3.Model.Internal.MarshallTransformations;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Windows.Forms.VisualStyles;
using System.Xml;
using System.Data;
using System.Reflection.Emit;
#endif
#if RevitForge || RevitForgeDebug
using DesignAutomationFramework;
#else
using Autodesk.Revit.UI;
#endif

namespace RougeRevit.Utilities
{
    /// <summary>
    /// Holds methods and extensions used throughout the project
    /// </summary>
    static class Util
    {
#if Revit2021 || Revit2022 || RevitForge || RevitForgeDebug
        private static ForgeTypeId _meters = UnitTypeId.Meters;
#else
        private static DisplayUnitType _meters = DisplayUnitType.DUT_METERS;
#endif
        public static readonly double ToRadians = Math.PI / 180.00;


#region Revit utils

        private static View3D _default3DView;

        /// <summary>
        /// Gets the default 3D view of the document. If it has been set before, uses the cached <see cref="View3D"/>
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static View3D GetDefault3DView(Document doc)
        {
            if (_default3DView == null) return _default3DView;
            List<View3D> all3DViews = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().ToList();
            _default3DView = all3DViews.FirstOrDefault(o => o.Name.Contains("3D"));
            return _default3DView;
        }

#if RevitForge || RevitForgeDebug
#else


        /// <summary>
        /// Shows a simple <see cref="TaskDialog"/> box with Ok and Cancel buttons, and the specified title and content
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <returns>The result</returns>
        public static TaskDialogResult ShowOkCancelDialog(string title, string message)
        {
            TaskDialog dialogBox = new TaskDialog(title);
            dialogBox.MainContent = message;
            TaskDialogCommonButtons buttons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;
            dialogBox.CommonButtons = buttons;

            return dialogBox.Show(); ;
        }
#endif


        /// <summary>
        /// Converts a <see cref="XYZ"/> from meters to feet (Revit's internal units)
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public static XYZ MetersToFeet(this XYZ point)
        {
            double x = UnitUtils.ConvertToInternalUnits(point.X, _meters);
            double y = UnitUtils.ConvertToInternalUnits(point.Y, _meters);
            double z = UnitUtils.ConvertToInternalUnits(point.Z, _meters);

            return new XYZ(x, y, z);
        }

        /// <summary>
        /// Converts a <see cref="XYZ"/> from feet (Revit's internal units) to meters
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public static XYZ FeetToMeters(this XYZ point)
        {
            double x = UnitUtils.ConvertFromInternalUnits(point.X, _meters);
            double y = UnitUtils.ConvertFromInternalUnits(point.Y, _meters);
            double z = UnitUtils.ConvertFromInternalUnits(point.Z, _meters);

            return new XYZ(x, y, z);
        }

        /// <summary>
        /// Converts a <see cref="double"/> to feet (Revit's internal units)
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public static double MetersToFeet(this double d)
        {
            return UnitUtils.ConvertToInternalUnits(d, _meters);
        }

        /// <summary>
        /// Converts a <see cref="double"/> to feet (Revit's internal units)
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public static double FeetToMeters(this double d)
        {
            return UnitUtils.ConvertFromInternalUnits(d, _meters);
        }

        /// <summary>
        /// Loads a Revit link into the target document
        /// </summary>
        /// <param name="doc">the target document</param>
        /// <param name="pathName">the path for the link file</param>
        /// <returns>the loaded element's <see cref="ElementId"/></returns>
        public static ElementId LoadRevitLink(Document doc, string pathName)
        {
            FilePath path = new FilePath(pathName);
            RevitLinkOptions options = new RevitLinkOptions(true);
            LinkLoadResult result = RevitLinkType.Create(doc, path, options);
            return (result.ElementId);
        }

        public static void SetBuildingNameParameter(Document doc, RougeElement obj, Element type)
        {            
            // Set Building Name Parameter - GUID is referring to the SharedParameter BuildingName in the Template
            Parameter parameter = type.get_Parameter(new Guid("2e8eee5a-5768-46ce-b297-2de118f395cf"));
            if (obj.Parent.Type == "Building")
            {
                parameter.Set(obj.Parent.Name);
            }
            else if (obj.Parent.Parent.Type == "Building")
            {
                parameter.Set(obj.Parent.Parent.Name);
            }
            else if (obj.Parent.Parent.Parent.Type == "Building")
            {
                parameter.Set(obj.Parent.Parent.Parent.Name);
            }
        }

        /// <summary>
        /// Hides any warnings Revit might throw back but passes Errors to the user
        /// </summary>
        public class HideWarnings : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failureAccelerator)
            {
                IList<FailureMessageAccessor> failureMessages = failureAccelerator.GetFailureMessages();
                foreach (FailureMessageAccessor failureMessage in failureMessages)
                {
                    if (failureMessage.GetSeverity() == FailureSeverity.Warning)
                    {
                        failureAccelerator.DeleteWarning(failureMessage);
                    }

                    else if (failureMessage.GetSeverity() == FailureSeverity.Error)
                    {
                        if (failureMessage.GetDescriptionText().Contains("joined"))
                        {
                            failureMessage.SetCurrentResolutionType(FailureResolutionType.DetachElements);
                            failureAccelerator.ResolveFailure(failureMessage);
                        }
                    }
                }
                return FailureProcessingResult.Continue;
            }
        }

        /// <summary>
        /// Set Limit Offset parameter on all communal rooms so that it aligns with bottom of the ceiling
        /// </summary>
        /// <param name="doc"></param>
        public static void SetRoomHeights(Document doc)
        {
            // set reference intersection variables
            View3D def3Dview = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().FirstOrDefault();
            XYZ direction = new XYZ(0, 0, 1);
            ElementClassFilter filter = new ElementClassFilter(typeof(RoofBase));

            // collect and iterate through all rooms in the model
            List<SpatialElement> spacialEls = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement)).Cast<SpatialElement>().ToList();
            foreach (SpatialElement el in spacialEls)
            {
                Room room = el as Room;
                if (room != null)
                {
                    LocationPoint location = room.Location as LocationPoint;
                    XYZ centre = location.Point;

                    // !!!! remove once rg-suite have pushed new levels
                    centre = new XYZ(centre.X, centre.Y, centre.Z + 0.305.MetersToFeet());

                    ReferenceIntersector refIntersector = new ReferenceIntersector(filter, FindReferenceTarget.Face, def3Dview);
                    ReferenceWithContext refWithContext = refIntersector.FindNearest(centre, direction);
                    if (refWithContext != null)
                    {
                        Reference reference = refWithContext.GetReference();
                        XYZ intersection = reference.GlobalPoint;
                        double limitOffset = centre.DistanceTo(intersection);

                        // !!!! remove once rg-suite have pushed new levels
                        limitOffset += 0.305.MetersToFeet();

                        room.LimitOffset = limitOffset;
                    }
                }
            }
        }

        #endregion Revit utils

        #region Geometry utils

        /// <summary>
        /// Get the absolute position of a <see cref="RougeElement"/>.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>The absolute position in Revit's internal units</returns>
        public static XYZ GetAbsolutePosition(RougeElement obj)
        {
            XYZ localPosition = obj.PosVec + obj.Rotation.OfPoint(
                - obj.AnchorVec.MultiplyVector(obj.SizeVec))
                + obj.AnchorVec.MultiplyVector(obj.SizeVec);
            return ApplyParentTransforms(obj, localPosition).MetersToFeet();
        }

        /// <summary>
        /// Create a horizontal <see cref="Line"/> in the XY plane from a <see cref="RougeElement"/>, using its size, position, anchor and rotation.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static Line CreateHorizontalCenterLine(RougeElement obj)
        {
            // Apply transforms
            var start = EvaluatePivot(obj, new XYZ(0, 0.5, 0));
            var end = EvaluatePivot(obj, new XYZ(1, 0.5, 0));

            start = ApplyParentTransforms(obj, start);
            end = ApplyParentTransforms(obj, end);

            start = RotateByAngleToNorth(start);
            end = RotateByAngleToNorth(end);

            double distLine = start.MetersToFeet().DistanceTo(end.MetersToFeet());

            // introduce tolerance value to ignore any 2 points that are too close to each other that causes a Revit line generation failure.
            if (distLine >= 0.0027)
            {
                return Line.CreateBound(start.MetersToFeet(), end.MetersToFeet());
            }
             return null; 


            //return Line.CreateBound(start.MetersToFeet(), end.MetersToFeet());
        }

        /// <summary>
        /// Creates a rectangle CurveArray based on the element's Size, Position, Rotation and Anchor
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static CurveArray CreateHorizontalRectangle(RougeElement obj)
        {
            // get the 4 corners
            XYZ v1 = EvaluatePivot(obj, new XYZ(0, 0, 0));
            XYZ v2 = EvaluatePivot(obj, new XYZ(1, 0, 0));
            XYZ v3 = EvaluatePivot(obj, new XYZ(1, 1, 0));
            XYZ v4 = EvaluatePivot(obj, new XYZ(0, 1, 0));

            v1 = ApplyParentTransforms(obj, v1).MetersToFeet();
            v2 = ApplyParentTransforms(obj, v2).MetersToFeet();
            v3 = ApplyParentTransforms(obj, v3).MetersToFeet();
            v4 = ApplyParentTransforms(obj, v4).MetersToFeet();

            v1 = RotateByAngleToNorth(v1);
            v2 = RotateByAngleToNorth(v2);
            v3 = RotateByAngleToNorth(v3);
            v4 = RotateByAngleToNorth(v4);

            CurveArray curves = new CurveArray();
            curves.Append(Line.CreateBound(v1, v2));
            curves.Append(Line.CreateBound(v2, v3));
            curves.Append(Line.CreateBound(v3, v4));
            curves.Append(Line.CreateBound(v4, v1));

            return curves;
        }

        /// <summary>
        /// Creates a polygon CurveArray based on the polygon verticies defined in the polygon array.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static CurveArray CreatePolygon(RougeElement obj, double[][] polygon)
        {
            double[][] vertices = polygon;
            CurveArray profile = new CurveArray();

            List<double[]> points = new List<double[]>();

            // check to see if the Array curve is open or closed, and if closed remove the last array point to create an open curved array.
            for (int p = 0; p < vertices.Length; p++) {
                points.Add(vertices[p]);
            }

            if (points[0][0] == points[vertices.Length - 1][0] && points[0][1] == points[vertices.Length - 1][1] && points[0][2] == points[vertices.Length - 1][2])
            {
                points.RemoveAt(vertices.Length-1);
            }

            XYZ previousPoint = ApplyParentTransforms(obj, ApplyObjectTransform(obj, new XYZ(points[0][0], -points[0][2], points[0][1])).MetersToFeet());
            previousPoint = RotateByAngleToNorth(previousPoint);

            // get all the polygon vertices///
            for (int i = 0; i < vertices.Length; i++)
            {
                XYZ v2;

                // Condition to test whether it is the last vertex in the array, and if it is to set v2 to be the first vertex.
                if (i != vertices.Length - 1)
                {
                    v2 = ApplyParentTransforms(obj, ApplyObjectTransform(obj, new XYZ(vertices[i + 1][0], -vertices[i + 1][2], vertices[i + 1][1])).MetersToFeet());
                }
                else
                {
                    v2 = ApplyParentTransforms(obj, ApplyObjectTransform(obj, new XYZ(vertices[0][0], -vertices[0][2], vertices[0][1])).MetersToFeet());
                }

                v2 = RotateByAngleToNorth(v2);

                double dist = previousPoint.DistanceTo(v2);

                // introduce tolerance value to ignore any 2 points that are too close to each other that causes a Revit line generation failure.
                if (dist >= 0.001)
                {
                    profile.Append(Line.CreateBound(previousPoint, v2));
                    previousPoint = v2;
                }
            }

            // ensure the profile forms a closed loop
            profile = EnsureClosedLoop(profile);

            return profile;
        }

        /// <summary>
        /// Recursively apply the rotation and position transformations of the parents of the object 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static XYZ ApplyParentTransforms(RougeElement obj, XYZ point)
        {
            var parent = obj.Parent;
            while (parent != null)
            {
                point = ApplyObjectTransform(parent, point);
                parent = parent.Parent;
            }

            return point;
        }

        /// <summary>
        /// Apply the rotation and position transformations of the object 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static XYZ ApplyObjectTransform(RougeElement obj, XYZ point)
        {
            return obj.PosVec + obj.Rotation.OfPoint(point);
        }

        /// <summary>
        /// Evaluates a local pivot on the given <see cref="RougeElement"/>
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="pivot"></param>
        /// <returns></returns>
        public static XYZ EvaluatePivot(RougeElement obj, XYZ pivot)
        {
            return obj.PosVec -
                obj.Rotation.OfPoint(obj.SizeVec.MultiplyVector(obj.AnchorVec)) +
                obj.Rotation.OfPoint(obj.SizeVec.MultiplyVector(pivot));
        }

        /// <summary>
        /// Apply rotation from the internal origin by angle to North 
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public static XYZ RotateByAngleToNorth(XYZ point)
        {
            double angle = Log.SiteData[2];
            Transform adjustToNoth = Transform.CreateRotationAtPoint(XYZ.BasisZ, -angle, XYZ.Zero);
            XYZ rotatedPoint = adjustToNoth.OfPoint(point);
            return rotatedPoint;
        }

        /// <summary>
        /// Ensures profiles from rouge polygons are forming closed loops
        /// </summary>
        /// <param name="curveArray"></param>
        /// <returns></returns>
        private static CurveArray EnsureClosedLoop(CurveArray curveArray)
        {
            // make sure the curves are in correct order
            List<Curve> orderedCurves = new List<Curve>();
            List<Curve> remainingCurves = new List<Curve>();

            foreach (Curve curve in curveArray)
            {
                remainingCurves.Add(curve);
            }

            Curve firstCurve = remainingCurves[0];
            remainingCurves.RemoveAt(0);
            orderedCurves.Add(firstCurve);

            // iterate through the curves find the next curve in the order
            while (remainingCurves.Count > 0)
            {
                XYZ lastPoint = orderedCurves.Last().GetEndPoint(1);

                double minDistance = double.MaxValue;
                Curve nextCurve = null;
                Curve removeCurve = null;

                for (int i = 0; i < remainingCurves.Count; i++)
                {
                    Curve curve = remainingCurves[i];
                    XYZ start = curve.GetEndPoint(0);
                    XYZ end = curve.GetEndPoint(1);

                    double distanceStart = lastPoint.DistanceTo(start);
                    double distanceEnd = lastPoint.DistanceTo(end);

                    if (distanceStart < minDistance)
                    {
                        minDistance = distanceStart;
                        nextCurve = curve;
                        removeCurve = curve;
                    }
                    if (distanceEnd < minDistance)
                    {
                        minDistance = distanceEnd;
                        nextCurve = curve.CreateReversed();
                        removeCurve = curve;
                    }
                }
                remainingCurves.Remove(nextCurve);
                orderedCurves.Add(removeCurve);
            }

            // iterate through the ordered curves and fill any gaps
            double minLength = 0.007.MetersToFeet();

            for (int i = 0; i < orderedCurves.Count; i++)
            {
                Curve currentCurve = orderedCurves[i];
                Curve nextCurve = orderedCurves[(i + 1) % orderedCurves.Count];

                double gapDistance = Math.Abs(currentCurve.GetEndPoint(1).DistanceTo(nextCurve.GetEndPoint(0)));

                // check if additional curve can be created to close potential gap
                if (gapDistance >= minLength)
                {
                    Line closingLine = Line.CreateBound(currentCurve.GetEndPoint(1), nextCurve.GetEndPoint(0));
                    orderedCurves.Add(closingLine);
                }
                // if distance is too short adjust the endpoint of the current curve to match the endpoint of the next curve
                else if (gapDistance > 0)
                {
                    orderedCurves[i] = Line.CreateBound(currentCurve.GetEndPoint(0), nextCurve.GetEndPoint(0));
                }
            }

            // create new curve array
            CurveArray closedArray = new CurveArray();
            foreach (Curve curve in orderedCurves) { closedArray.Append(curve); }

            return closedArray;
        }

        #endregion Geometry utils

        #region Math utils

        /// <summary>
        /// Compare two <see cref="double"/> are within the given difference
        /// </summary>
        /// <param name="d"></param>
        /// <param name="other"></param>
        /// <param name="difference"></param>
        /// <returns></returns>
        public static bool Compare(this double d, double other, double difference = 0.001)
        {
            return Math.Abs(d - other) <= difference;
        }

        /// <summary>
        /// Multiply the coordinates of two vectors
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static XYZ MultiplyVector(this XYZ a, XYZ b)
        {
            return new XYZ(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        }

        #endregion Math utils
              
        /// <summary>
        /// Takes a collection of collinear curves and consolidates them into a unified curve
        /// by connecting the end points that are farthest apart.
        /// </summary>
        /// <param name="curves"></param>
        /// <returns></returns>
        public static Curve MergeColinearCurves(List<Curve> curves)
        {
            double maxDistance = 0;
            Curve longestCurve = null;

            // collect all end points
            List<XYZ> endPoints = curves
                .SelectMany(curve => new[] { curve.GetEndPoint(0), curve.GetEndPoint(1)})
                .Distinct()
                .ToList();           

            for (int i = 0; i < endPoints.Count - 1; i++)
            {
                for (int j = i + 1; j < endPoints.Count; j++)
                {
                    double currentDistance = Math.Abs(endPoints[i].DistanceTo(endPoints[j]));
                    if (maxDistance < currentDistance)
                    {
                        maxDistance = currentDistance;
                        Line line = Line.CreateBound(endPoints[i], endPoints[j]);
                        longestCurve = line as Curve;                       
                    }
                }
            }
            return longestCurve;
        }

        public static bool AreCurvesOverlapping(Curve curve1, Curve curve2)
        {
            Line line1 = curve1 as Line;
            Line line2 = curve2 as Line;

            if (line1.Direction.IsAlmostEqualTo(line2.Direction) || line1.Direction.IsAlmostEqualTo(-line2.Direction))
            {
                IntersectionResultArray resultArray = new IntersectionResultArray();
                SetComparisonResult compResult = line1.Intersect(line2, out resultArray);

                if (compResult == SetComparisonResult.Equal) return true;
            }
            return false;
        }

        public static bool AreCurvesParallel(Curve curve1, Curve curve2)
        {
            Line line1 = curve1 as Line;
            Line line2 = curve2 as Line;

            if (line1.Direction.IsAlmostEqualTo(line2.Direction) || line1.Direction.IsAlmostEqualTo(-line2.Direction))
            {
                return true;
            }
            return false;
        }

        public static List<XYZ> GetIntersectionPoints(Curve curve, List<Curve> curves)
        {
            List<XYZ> intersectionPoints = new List<XYZ>();

            foreach (Curve otherCurve in curves)
            {
                if (curve.Equals(otherCurve)) continue;

                SetComparisonResult result = curve.Intersect(otherCurve, out IntersectionResultArray intersectionResults);
                if (result == SetComparisonResult.Overlap)
                {
                    foreach (IntersectionResult intersectionResult in intersectionResults)
                    {
                        intersectionPoints.Add(intersectionResult.XYZPoint);
                    }
                }
            }
            if (intersectionPoints.Count != 0)
            {
                List<XYZ> sortedPoints = intersectionPoints
                    .OrderBy(point => point.X)
                    .ThenBy(point => point.Y).ToList();

                return sortedPoints;
            }
            return null; 
        }

        public static XYZ GetCentroid(List<Curve> curves)
        {
            double totalX = 0.0;
            double totalY = 0.0;
            double totalZ = 0.0;

            foreach (Curve curve in curves)
            {
                XYZ startPoint = curve.GetEndPoint(0);
                XYZ endPoint = curve.GetEndPoint(1);

                totalX += (startPoint.X + endPoint.X) / 2.0;
                totalY += (startPoint.Y + endPoint.Y) / 2.0;
                totalZ += (startPoint.Z + endPoint.Z) / 2.0;
            }
            int numCurves = curves.Count;
            XYZ centroid = new XYZ(totalX / numCurves, totalY / numCurves, totalZ / numCurves);

            return centroid;
        }

        public static Curve ExtendCurve(Curve curve, double length)
        {
            if (curve == null || length == 0) return curve;

            XYZ direction = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
            XYZ extensionVector = direction * length;

            Line extendedCurve = Line.CreateBound(
                curve.GetEndPoint(0) - extensionVector,
                curve.GetEndPoint(1) + extensionVector);

            return extendedCurve as Curve;
        }

        /// <summary>
        /// Takes a list of curves and orders them. The process begins with the first curve, searching
        /// for the curve that is connected to it or the closest one. Additional curve will be created
        /// to close any gaps.
        /// </summary>
        /// <param name="curves"></param>
        /// <returns></returns>
        public static List<Curve> OrderCurves(List<Curve> curves, double tolerance, bool closeLoop)
        {
            if (curves.Count < 2) return curves;

            List<Curve> orderedCurves = new List<Curve> { curves[0] };
            curves.RemoveAt(0);

            while (curves.Count > 0)
            {
                bool foundNext = false;
                double minDistance = double.MaxValue;
                Curve closestCurve = null;
                Curve gapCurve = null;
                int index = curves.Count;

                XYZ evaluationPoint = orderedCurves[orderedCurves.Count - 1].GetEndPoint(1);

                // find connected curves (with tolerance = 0.001m)
                for (int i = 0; i < curves.Count; i++)
                {
                    double currentDistance0 = evaluationPoint.DistanceTo(curves[i].GetEndPoint(0));
                    double currentDistance1 = evaluationPoint.DistanceTo(curves[i].GetEndPoint(1));

                    if (currentDistance0 < tolerance)
                    {
                        orderedCurves.Add(curves[i]);
                        curves.RemoveAt(i);
                        foundNext = true;
                        break;
                    }
                    if (currentDistance1 < tolerance)
                    {
                        // add reversed curve
                        orderedCurves.Add(Line.CreateBound(curves[i].GetEndPoint(1), curves[i].GetEndPoint(0)));
                        curves.RemoveAt(i);
                        foundNext = true;
                        break;
                    }
                    if (currentDistance0 < minDistance)
                    {
                        minDistance = currentDistance0;
                        closestCurve = curves[i];
                        index = i;
                        // create gap curve
                        gapCurve = Line.CreateBound(evaluationPoint, curves[i].GetEndPoint(0));
                    }
                    else if (currentDistance1 < minDistance)
                    {
                        minDistance = currentDistance1;                        
                        index = i;
                        // create reversed curve and a gap curve 
                        closestCurve = Line.CreateBound(curves[i].GetEndPoint(1), curves[i].GetEndPoint(0));
                        gapCurve = Line.CreateBound(evaluationPoint, curves[i].GetEndPoint(1));
                    }
                }
                if (!foundNext)
                {
                    orderedCurves.Add(gapCurve);
                    orderedCurves.Add(closestCurve);
                    curves.RemoveAt(index);
                }
            }
            if (closeLoop)
            {
                if (orderedCurves[0].GetEndPoint(0).IsAlmostEqualTo(orderedCurves[orderedCurves.Count - 1].GetEndPoint(1)))
                {
                    orderedCurves.Add(Line.CreateBound(
                        orderedCurves[orderedCurves.Count - 1].GetEndPoint(1),
                        orderedCurves[0].GetEndPoint(0)));
                }
            }
            return orderedCurves;
        }
    }

}
