using Autodesk.Revit.DB;
using RougeRevit.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System;
using FilterRule = Autodesk.Revit.DB.FilterRule;
using static RougeRevit.Utilities.Util;
#if Revit2020 || Revit2021 || Revit2022
using Autodesk.Revit.UI;
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.S3.Model;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
#endif

namespace RougeRevit
{
    /// <summary>
    /// Object that holds all the imported Rouge Data that needs to be added to a Revit document.
    /// </summary>
    public class RougeData
    {
        /// <summary>
        /// Creates all the elements that have been imported in the input <see cref="Document"/> inside a Transaction.
        /// </summary>
        /// <param name="doc"></param>
        public void CreateElements(RougeElement rgObject, Document doc)
        {
            Log.Clear();

            using (Transaction trans = new Transaction(doc))
            {
                trans.Start("Create imported elements");

                // register to hide Warnings that can be ignored but pass Errors for user to handle
                FailureHandlingOptions options = trans.GetFailureHandlingOptions();
                HideWarnings hideWarnings = new HideWarnings();
                options.SetFailuresPreprocessor(hideWarnings);
                trans.SetFailureHandlingOptions(options);

                GetModulousIdsParameters(doc);
                GetAvailableSymbolsAndTypes(doc);

#if Revit2020 || Revit2021 || Revit2022
                // Collect the available apartment files, so that they are loaded when required
                string currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string apartmentsFolder = Path.Combine(currentPath, @"modulous\apartments\");
                string apartmentsLocation = Path.Combine(currentPath, @"modulous\");
                if (!Directory.Exists(apartmentsFolder)) Directory.CreateDirectory(apartmentsFolder);

                // Collect the available building files
                string buildingFolder = Path.Combine(currentPath, @"modulous\buildings\");
                if (!Directory.Exists(buildingFolder)) Directory.CreateDirectory(buildingFolder);

                string accessKey = Environment.GetEnvironmentVariable("APIAccessKey");
                string secretKey = Environment.GetEnvironmentVariable("APISecretKey");

                //Download Current Apartment files from AWS S3 Server
                // create an instance of AmazonS3Client, passing in your AWS access key and secret key
                AmazonS3Client s3Client = new AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.EUWest2);

                // create an instance of TransferUtility, which provides high-level functionality for managing transfers to and from Amazon S3
                TransferUtility transferUtility = new TransferUtility(s3Client);

                // specify the bucket name and key of the file you want to download
                string bucketName = "mcs-revitdata";
                ListObjectsRequest request = new ListObjectsRequest
                {
                    BucketName = bucketName,
                    Prefix = "apartments/" // Optional prefix to list objects under a specific folder
                };

                // Call the list objects operation and retrieve the response
                // ListObjectsResponse response = s3Client.ListObjects(request); temporarily disabled until get proper URLS

                // Loop through the objects in the response and print their keys (filenames)
                //foreach (S3Object obj in response.S3Objects)
                //{
                //    TransferUtilityDownloadRequest downloadRequest = new TransferUtilityDownloadRequest
                //    {
                //        BucketName = bucketName,
                //        Key = obj.Key,
                //        FilePath = apartmentsLocation + obj.Key // specify the local file path where you want to save the downloaded file
                //    };
                //    // use TransferUtility to download the file
                //    if (obj.Key != "apartments/")
                //    {
                //        transferUtility.Download(downloadRequest);
                //    }
                //}
#endif

#if RevitForge || RevitForgeDebug
                string apartmentsFolder = Directory.GetCurrentDirectory();
                string buildingFolder = Directory.GetCurrentDirectory();
                // ReadRougeDB.LogTrace(apartmentsFolder);
                //string currentPath = Directory.GetCurrentDirectory();
                //string apartmentsFolder = Path.Combine(currentPath, @"apartments\");

#endif
                string[] apartmentFiles = Directory.GetFiles(apartmentsFolder, "*.rvt");
                foreach (string apartmentFile in apartmentFiles)
                {
                    string aptName = Path.GetFileNameWithoutExtension(apartmentFile);
                    if (!Log.ApartmentFiles.ContainsKey(aptName)) Log.ApartmentFiles[aptName] = apartmentFile;
                }

                string[] buildingFiles = Directory.GetFiles(buildingFolder, "*.rvt");
                foreach (string buildingFile in buildingFiles)
                {
                    string bldName = Path.GetFileNameWithoutExtension(buildingFile);
                    if (!Log.BuildingFiles.ContainsKey(bldName)) Log.BuildingFiles[bldName] = buildingFile;
                }

                // Create Revit Physical Elements
                rgObject.CreateElement(doc);
                RougeElement.CreateScopeLines(doc);
                doc.Regenerate();

                // Set initial levels extends
                RougeElement.MaximizeLevelExtents(doc);

                // Set the site
                RougeElement.SetSiteCoordinates(doc);
                RougeElement.CreateSiteBoundary(doc);

                //Create Views, Plan, Elevation, etc..
                ViewCreation.CreateViews(doc);

                // Create Drawings Sheets
                ViewCreation.CreateSheets(doc);

                // Align room height to bottom of ceiling
                //Util.SetRoomHeights(doc);

                // Process walls
                RougeElement.JoinFacadeWalls(doc);
                RougeElement.JoinInternalWalls(doc);
                RougeElement.CreateCommunalWalls(doc);
                RougeElement.CreateCommunalDoors(doc);

#if Revit2020 || Revit2021 || Revit2022
                Log.ShowLog();
#endif
                trans.Commit();
            }

#if RevitForge || RevitForgeDebug

            //Path of the Rouge Template file
            string OUTPUT_FILE = "TessaDesignOption.rvt";

            //ReadRougeDB.LogTrace(OUTPUT_FILE);
            //Save the updated file by overwriting the existing file
            ModelPath ProjectModelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(OUTPUT_FILE);
            SaveAsOptions SAO = new SaveAsOptions();
            SAO.OverwriteExistingFile = true;

            //Save the project file output from Rouge Generated Design
            ReadRougeDB.LogTrace("Saving file...");
            doc.SaveAs(ProjectModelPath, SAO);
#endif
        }

        /// <summary>
        /// Collects the IDs of the Modulous Id parameters, so that
        /// the filtering process can be sped up
        /// </summary>
        /// <param name="doc"></param>
        public void GetModulousIdsParameters(Document doc)
        {
            // Start with empty definitions and use the document's ParameterBuinding to find them
            Definition codeCategoryDefinition = null;
            Definition codeTypeDefinition = null;
            var bm = doc.ParameterBindings;
            var iterator = bm.ForwardIterator();
            while (iterator.MoveNext())
            {
                if (iterator.Key.Name.Contains("Code: Category"))
                {
                    codeCategoryDefinition = iterator.Key;
                }
                else if (iterator.Key.Name.Contains("Code: Type"))
                {
                    codeTypeDefinition = iterator.Key;
                }
                if (codeCategoryDefinition != null && codeTypeDefinition != null) break;
            }

#if Revit2020 || Revit2021 || Revit2022
            // Check if collection of definitions was successful
            if (codeCategoryDefinition == null || codeTypeDefinition == null)
            {
                string err = $"File is missing Modulous IDs parameters!";
                Util.ShowOkCancelDialog("Error, wrong template", err);
                throw new System.Exception(err);
            }
#endif

            // Use the first wall element (expected to always exist) to get the parameters ids
            Element firstWallTypeElement = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsElementType().FirstElement();
            var categoryParam = firstWallTypeElement.get_Parameter(codeCategoryDefinition);
            var typeParam = firstWallTypeElement.get_Parameter(codeTypeDefinition);

            // Store the param Ids on Log
            Log.CodeCategoryParamId = categoryParam.Id;
            Log.CodeTypeParamId = typeParam.Id;
        }

        /// <summary>
        /// Stores the <see cref="ElementType"/> and <see cref="FamilySymbol"/> of all the
        /// available Revit Types and Symbols that have Modulous Ids set
        /// </summary>
        /// <param name="doc"></param>
        public void GetAvailableSymbolsAndTypes(Document doc)
        {
            // Create the filters for the parameters
            var categoryFilter = ParameterFilterRuleFactory.CreateHasValueParameterRule(Log.CodeCategoryParamId);
            var typeFilter = ParameterFilterRuleFactory.CreateHasValueParameterRule(Log.CodeTypeParamId);

            // Get the symbols with values set for both parameters
            var symbols = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).
                WherePasses(new ElementParameterFilter(new List<FilterRule> { categoryFilter, typeFilter })).Cast<FamilySymbol>();

            // Iterate through the symbols and store them
            foreach (FamilySymbol sym in symbols)
            {
                string categoryValue = null;
                string typeValue = null;
                foreach (Parameter parameter in sym.Parameters)
                {
                    if (parameter.Id.Equals(Log.CodeCategoryParamId)) categoryValue = parameter.AsString();
                    if (parameter.Id.Equals(Log.CodeTypeParamId)) typeValue = parameter.AsString();
                    if (categoryValue != null && typeValue != null) break;
                }
                if (categoryValue == null && typeValue == null) return;

                // Store the symbols using the category and type as keys
                Log.AvailableSymbols[$"{categoryValue}|{typeValue}"] = sym;
            }

            // Get the element types with values set for both parameters
            var elementTypes = new FilteredElementCollector(doc).OfClass(typeof(ElementType)).
                WherePasses(new ElementParameterFilter(new List<FilterRule> { categoryFilter, typeFilter })).Cast<ElementType>();

            // Iterate through the element types and store them
            foreach (ElementType elementType in elementTypes)
            {
                string categoryValue = null;
                string typeValue = null;
                foreach (Parameter parameter in elementType.Parameters)
                {
                    if (parameter.Id.Equals(Log.CodeCategoryParamId)) categoryValue = parameter.AsString();
                    if (parameter.Id.Equals(Log.CodeTypeParamId)) typeValue = parameter.AsString();
                    if (categoryValue != null && typeValue != null) break;
                }
                if (categoryValue == null && typeValue == null) return;

                // Store the element types using the category and type as keys
                Log.AvailableTypes[$"{categoryValue}|{typeValue}"] = elementType;
            }
        }

        /// <summary>
        /// Calculates the Angle To North before any elements are created and logs it
        /// </summary>
        /// <param name="rgObject"></param>
        /// <param name="doc"></param>
        public void GetAngleToNorth(RougeElement rgObject, Document doc)
        {
            // log the unique rotation angles of all link instances into ApartmentRotations
            // log technical space walls
            rgObject.PreProcessData(doc);

            // calculate angle to north
            double angle = 0;
            double minVal = Log.ApartmentRotations.Min();

            if (minVal <= 45) angle = minVal * (Math.PI / 180);
            else if (minVal > 45) angle = (360 - (90 - minVal)) * (Math.PI / 180);

            // log the result
            Log.SiteData.Add(angle);
        }
    }
}
