
using System.Data.Entity.Core.Common.EntitySql;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms.VisualStyles;
using FFmpeg.NET;
using Newtonsoft.Json;

namespace Ogama.Modules.ImportExport.UXI
{
    using Ogama.ExceptionHandling;
    using Ogama.MainWindow;
    using Ogama.Modules.Common.SlideCollections;
    using Ogama.Modules.Common.Tools;
    using Ogama.Modules.Common.Types;
    using Ogama.Modules.Fixations;
    using Ogama.Modules.ImportExport.Common;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Windows.Forms;
    using System.Xml.Serialization;
    using System.Web.Script.Serialization;
    using VectorGraphics.Elements;
    using VectorGraphics.Elements.ElementCollections;
    using VectorGraphics.StopConditions;

    class UXImport
    {
        // --------------------------------------------------------------------------------------------------------------------
        // <copyright file="UXImport.cs" company="FIIT">
        //   OGAMA - open gaze and mouse analyzer 
        //   Copyright (C) 2015 Dr. Adrian Voßkühler  
        //   Licensed under GPL V3
        // </copyright>
        // <author>Martin Civan</author>
        // <email>martin.civan5@gmail.com</email>
        // <summary>
        //   Class for importing UXI data through multiple dialogs.
        // </summary>
        // --------------------------------------------------------------------------------------------------------------------
        ///////////////////////////////////////////////////////////////////////////////
        // Defining Constants                                                        //
        ///////////////////////////////////////////////////////////////////////////////
        

        private const string BOTH = "Both";
        private const string RIGHT = "Right";
        private const string LEFT = "Left";
        private const string BUTTON_UP = "ButtonUp";
        private const string BUTTON_DOWN = "ButtonDown";
        private const string KEY_UP = "KeyUp";
        private const string KEY_DOWN = "KeyDown";
        private const string KEY_PRESS = "KeyPress";
        private const string MOVE = "Move";
        private static readonly String[] VALIDITY_WHITELIST = { BOTH, LEFT, RIGHT};
        private static readonly String[] EVENTTYPES_WHITELIST = { BUTTON_DOWN, BUTTON_UP, MOVE };

        ///////////////////////////////////////////////////////////////////////////////
        // Defining Variables, Enumerations, Events                                  //
        ///////////////////////////////////////////////////////////////////////////////
        #region Static Fields

        /// <summary>
        ///   List to fill with imported and filtered <see cref="RawData" />
        /// </summary>
        protected static readonly List<RawData> RawDataList;

        /// <summary>
        ///   List to fill with generated <see cref="SubjectsData" />
        /// </summary>
        protected static readonly List<SubjectsData> SubjectList;

        /// <summary>
        ///   List to fill with generated or imported <see cref="TrialsData" />
        /// </summary>
        protected static readonly List<TrialsData> TrialList;

        /// <summary>
        /// List to fill with trial event data <see cref="TrialEventsData"/>
        /// </summary>
        protected static readonly List<TrialEventsData> EventList;

        /// <summary>
        ///   Saves the ASCII file import specialized settings
        ///   during this import session.
        /// </summary>
        protected static UXISettings asciiSetting;

        /// <summary>
        ///   Saves the specialized settings used during this import session.
        /// </summary>
        protected static DetectionSettings detectionSetting;

        public static MainForm mainWindowCache;

        #endregion

        ///////////////////////////////////////////////////////////////////////////////
        // Construction and Initializing methods                                     //
        ///////////////////////////////////////////////////////////////////////////////
        #region Constructors and Destructors

        /// <summary>
        ///   Initializes static members of the ImportRawData class.
        /// </summary>
        static UXImport()
        {
            SubjectList = new List<SubjectsData>();
            TrialList = new List<TrialsData>();
            RawDataList = new List<RawData>();
            EventList = new List<TrialEventsData>();

            detectionSetting = new DetectionSettings();
            asciiSetting = new UXISettings();
        }

        #endregion

        ///////////////////////////////////////////////////////////////////////////////
        // Defining Properties                                                       //
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Properties

        /// <summary>
        ///   Gets the ASCII file import specialized settings
        ///   during this import session.
        /// </summary>
        /// <value>A <see cref="ASCIISettings" />.</value>
        /// <seealso cref="ASCIISettings" />
        public static UXISettings ASCIISettings
        {
            get
            {
                return asciiSetting;
            }
        }

        /// <summary>
        ///   Gets the specialized settings used during this import session.
        /// </summary>
        /// <value>A <see cref="DetectionSettings" />.</value>
        /// <seealso cref="DetectionSettings" />
        public static DetectionSettings DetectionSetting
        {
            get
            {
                return detectionSetting;
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// This method extracts a filename of a iViewX msg line
        ///   with the given trigger string.
        /// </summary>
        /// <param name="line">
        /// The line to search for.
        /// </param>
        /// <param name="triggerString">
        /// The trigger string in the message after which the filename
        ///   appears.
        /// </param>
        /// <param name="currentTrialID">
        /// The ID of the current trial.
        /// </param>
        public static void ExtractImageNameFromiViewXmsg(string line, string triggerString, int currentTrialID)
        {
            if (line.Contains(triggerString))
            {
                string[] items = line.Trim().Split(asciiSetting.ColumnSeparatorCharacter);
                foreach (string item in items)
                {
                    // Find item with trigger string
                    if (item.Contains(triggerString))
                    {
                        // Remove "Scene Image" Prefix to get the filename
                        string imagepath = item.Replace(triggerString, string.Empty);
                        string stimulusFile = Path.GetFileName(imagepath).Trim();
                        if (!detectionSetting.ImageDictionary.ContainsKey(currentTrialID))
                        {
                            detectionSetting.ImageDictionary.Add(currentTrialID, stimulusFile);
                        }
                        else
                        {
                            detectionSetting.ImageDictionary[currentTrialID] = stimulusFile;
                        }

                        detectionSetting.TrialSequenceToTrialIDAssignments[currentTrialID] = currentTrialID;

                        if (!detectionSetting.TrialIDToImageAssignments.ContainsKey(currentTrialID))
                        {
                            detectionSetting.TrialIDToImageAssignments.Add(currentTrialID, stimulusFile);
                        }

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// This static method creates a slide with a sized image
        ///   for each trial and adds it to the slideshow.
        /// </summary>
        /// <param name="detectonSettings">
        /// The <see cref="DetectionSettings"/>
        ///   used in this import.
        /// </param>
        /// <param name="mainWindow">
        /// The <see cref="MainForm"/> to get access to the status label.
        /// </param>
        public static void GenerateOgamaSlideshowTrials(DetectionSettings detectonSettings, MainForm mainWindow)
        {
            // Stores found stimuli files
            List<string> trialNames = Document.ActiveDocument.ExperimentSettings.SlideShow.GetTrialNames();

            foreach (KeyValuePair<int, int> kvp in detectonSettings.TrialSequenceToTrialIDAssignments)
            {
                int trialID = kvp.Value;
                string file = string.Empty;
                if (detectonSettings.TrialIDToImageAssignments.ContainsKey(trialID))
                {
                    file = detectonSettings.TrialIDToImageAssignments[trialID];
                }

                string filename = Path.GetFileNameWithoutExtension(file);

                // Create slide
                var stopConditions = new StopConditionCollection
                               {
                                 new MouseStopCondition(
                                   MouseButtons.Left,
                                   true,
                                   string.Empty,
                                   null,
                                   Point.Empty)
                               };

                VGImage stimulusImage = null;

                if (file != string.Empty)
                {
                    stimulusImage = new VGImage(
                      ShapeDrawAction.None,
                      Pens.Black,
                      Brushes.Black,
                      SystemFonts.MenuFont,
                      Color.White,
                      Path.GetFileName(file),
                      Document.ActiveDocument.ExperimentSettings.SlideResourcesPath,
                      ImageLayout.Zoom,
                      1f,
                      Document.ActiveDocument.PresentationSize,
                      VGStyleGroup.None,
                      filename,
                      string.Empty,
                      true)
                    {
                        Size = Document.ActiveDocument.PresentationSize
                    };
                }

                var newSlide = new Slide(
                  filename,
                  Color.White,
                  null,
                  stopConditions,
                  null,
                  string.Empty,
                  Document.ActiveDocument.PresentationSize)
                { Modified = true, MouseCursorVisible = true };

                // Only add stimulus if an image exists
                if (file != string.Empty)
                {
                    newSlide.VGStimuli.Add(stimulusImage);
                }
                else
                {
                    newSlide.Name = "No stimulus detected";
                }

                // Create trial
                if (Document.ActiveDocument.ExperimentSettings.SlideShow.GetNodeByID(trialID) != null)
                {
                    // trialID = int.Parse(Document.ActiveDocument.ExperimentSettings.SlideShow.GetUnusedNodeID());
                    // var message = string.Format("The trial with the ID:{0} exists already in the slideshow so it will not be created."
                    // + Environment.NewLine + "Delete the trial with this ID in the slideshow design module if you want it to be newly created by the importer, or assign a new ID to the imported data.", trialID);
                    // ExceptionMethods.ProcessMessage("This trial exists already", message);
                    continue;
                }

                var newTrial = new Trial(filename, trialID) { Name = filename };

                newTrial.Add(newSlide);

                if (trialNames.Contains(filename) || (filename == string.Empty && trialNames.Contains("No stimulus detected")))
                {
                    // Trial already exists
                    continue;
                }

                trialNames.Add(filename);

                // Create slide node
                var slideNode = new SlideshowTreeNode(newSlide.Name)
                {
                    Name = trialID.ToString(CultureInfo.InvariantCulture),
                    Slide = newSlide
                };

                // Add slide node to slideshow
                Document.ActiveDocument.ExperimentSettings.SlideShow.Nodes.Add(slideNode);
                Document.ActiveDocument.Modified = true;
            }

            mainWindow.StatusLabel.Text = "Saving slideshow to file ...";
            if (!Document.ActiveDocument.SaveSettingsToFile(Document.ActiveDocument.ExperimentSettings.DocumentFilename))
            {
                ExceptionMethods.ProcessErrorMessage("Couldn't save slideshow to experiment settings.");
            }

            mainWindow.StatusLabel.Text = "Refreshing context panel ...";
            //mainWindow.RefreshContextPanelImageTabs();
            mainWindow.StatusLabel.Text = "Ready ...";
            mainWindow.StatusProgressbar.Value = 0;
        }

        ///////////////////////////////////////////////////////////////////////////////
        // Public methods                                                            //
        ///////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Generates the trial list for the current import settings
        ///   up to the number of lines that are given.
        /// </summary>
        /// <param name="numberOfImportLines">
        /// An <see cref="int"/>
        ///   with the max number of lines to import.
        ///   Set it to -1 to use all lines.
        /// </param>
        /// <returns>
        /// A <see cref="List{TrialsData}"/> with the calculated trials.
        /// </returns>
        public static List<TrialsData> GetTrialList(int numberOfImportLines)
        {
            // Convert the import file into ogama column format
            GenerateOgamaRawDataList(numberOfImportLines);

            // Generate the trial list from the raw data with the current settings.
            GenerateOgamaSubjectAndTrialList();

            return TrialList;
        }

        /// <summary>
        /// This method splits the given trials data list into a
        ///   dictionary of trial data lists separated by subjects.
        /// </summary>
        /// <param name="wholeTrialDataList">
        /// A <see cref="List{TrialsData}"/>
        ///   with all the imported samples.
        /// </param>
        /// <returns>
        /// A Dictionary with the splitted input.
        /// </returns>
        public static Dictionary<string, List<TrialsData>> SplitTrialDataListBySubjects(List<TrialsData> wholeTrialDataList)
        {
            // Create the return dictionary
            var trialDataBySubject = new Dictionary<string, List<TrialsData>>();

            // Get First subject name
            string lastSubjectName = string.Empty; // = wholeTrialDataList[0].SubjectName;

            // Create list for current subject
            var currentList = new List<TrialsData>();

            // Iterate whole raw data list and add for each subject a 
            // new entry in the rawDataBySubject list.
            foreach (TrialsData data in wholeTrialDataList)
            {
                if (data.SubjectName != lastSubjectName)
                {
                    currentList = new List<TrialsData>();
                    trialDataBySubject.Add(data.SubjectName, currentList);
                    lastSubjectName = data.SubjectName;
                }

                currentList.Add(data);
            }

            // Return list.
            return trialDataBySubject;
        }

        /// <summary>
        /// Starts a multiple dialog routine (raw data import assistant)
        ///   for reading raw data files into the programs database
        /// </summary>
        /// <param name="mainWindow">
        /// The <see cref="MainForm"/> to get access to the status label.
        /// </param>
        public static void Start(MainForm mainWindow)
        {
            mainWindowCache = mainWindow;

            var folderBrowser = new FolderBrowserDialog();
            if (folderBrowser.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            asciiSetting.Folder = folderBrowser.SelectedPath;
            var folders = CreateFolderList(asciiSetting.Folder);
            if (folders.Count == 0)
            {
                MessageBox.Show("No projects have been found in selected folder and folders inside it.", "Project not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dialog = new UXImportDialog();
            dialog.setDirectories(folders);
            dialog.setPreferredEye("Average");
            dialog.ShowDialog();
        }

        public static List<String> CreateFolderList(Object d)
        {
            List<String> result = new List<string>();

            if (CheckDirectory((String) d))
            {
                result.Add((String) d);
            }
            foreach (String dir in Directory.EnumerateDirectories((String) d))
            {
                if (UXImport.CheckDirectory(dir))
                {
                    result.Add((String) dir);
                }
                else
                {
                    result = result.Concat(CreateFolderList(dir)).ToList();
                }
            }
            return result;
        }

        public static bool CheckDirectory(String dir)
        {
            return File.Exists(dir + "\\settings.json");
        }

        public static void ImportVideo(string input, string output, CancellationToken token)
        {
            var inputFile = new MediaFile(input);
            var outputFile = new MediaFile(output);
            var e = new Engine("ffmpeg/ffmpeg.exe");
            var task = e.ConvertAsync(inputFile, outputFile, token);
            task.Wait();
        }

        public static void Run(List<String> values, IProgress<int> progress, CancellationToken token, String preferredEye)
        {
            int p = 0;
            foreach (String value in values)
            {
               
                Run(value, preferredEye);
                progress.Report(++p);
                ImportVideo(asciiSetting.GetScreenVideoPath(), Path.Combine(Document.ActiveDocument.ExperimentSettings.ThumbsPath, detectionSetting.SubjectName + "-" + "0.avi") ,token);
                if (token.IsCancellationRequested)
                {
                    return;
                }  
            }
            string message = "Import data successfully written to database." + Environment.NewLine
                                                                             + "Please don´t forget to move the stimuli images to the SlideResources subfolder"
                                                                             + "of the experiment, otherwise no images will be shown.";
            ExceptionMethods.ProcessMessage("Success", message);
        }

        public static void Run(Object dir, String pre)
        {
            try
            {
                //OpenFile:
                //    detectionSetting.ImportType = ImportTypes.Rawdata;
                //        var objfrmImportReadFile = new ImportParseFileDialog(ref asciiSetting);
                //    ReadFile:
                //        DialogResult resultRawData = objfrmImportReadFile.ShowDialog();
                //        if (resultRawData == DialogResult.OK)
                //        {
                //            var objfrmImportRawDataAssignColumns = new ImportRawDataAssignColumnsDialog();

                //        MakeAssignments:
                //            DialogResult resultAssign = objfrmImportRawDataAssignColumns.ShowDialog();
                //            if (resultAssign == DialogResult.OK)
                //            {
                //                var objfrmImportTrials = new ImportTrialsDialog(ref asciiSetting, ref detectionSetting);

                //            CheckTrials:
                //                DialogResult resultTrials = objfrmImportTrials.ShowDialog();
                //                if (resultTrials == DialogResult.OK)
                //                {
                //                    var objfrmImportImages = new ImportImagesDialog(ref asciiSetting, ref detectionSetting);

                //                    DialogResult resultImages = objfrmImportImages.ShowDialog();
                //                    if (resultImages == DialogResult.OK)
                //                    {
                //                        if (MessageBox.Show(
                //                          "Would you like to save the import settings ?",
                //                          Application.ProductName,
                //                          MessageBoxButtons.YesNo,
                //                          MessageBoxIcon.Question) == DialogResult.Yes)
                //                        {
                //                          SaveImportSettings();
                //                        }

                // Show import splash window
                //asciiSetting.WaitingSplash.RunWorkerAsync();

                // Give some time to show the splash ...
                Application.DoEvents();

                asciiSetting = new UXISettings();
                detectionSetting = new DetectionSettings();
                asciiSetting.Folder = (string) dir;

                // Read log file again, but complete
                GenerateOgamaRawDataList(-1);

                // Generate the trials
                GenerateOgamaSubjectAndTrialList();

                GenerateKeyboardEventList();

                // Save the import into ogamas database and the mdf file.
                bool successful = SaveImportIntoTablesAndDB();

                // Create slideshow trials
                GenerateOgamaSlideshowTrials(detectionSetting, mainWindowCache);

                // Calculate Fixations
                CalculateFixations(mainWindowCache);

                // Clear lists
                SubjectList.Clear();
                TrialList.Clear();
                RawDataList.Clear();

                // Import has finished.
                asciiSetting.WaitingSplash.CancelAsync();

                // Inform user about success.
//                if (successful)
//                {
//                    string message = "Import data successfully written to database." + Environment.NewLine
//                                     + "Please don´t forget to move the stimuli images to the SlideResources subfolder"
//                                     + "of the experiment, otherwise no images will be shown.";
//                    ExceptionMethods.ProcessMessage("Success", message);
//                }
//                else
//                {
//                    string message = "Import had errors. Some or all of the import data "
//                                     + "could not be written the database.";
//                    ExceptionMethods.ProcessErrorMessage(message);
//                }
                //}
                //else if (resultImages == DialogResult.Cancel)
                //{
                //    goto CheckTrials;
                //}
                //}
                //else if (resultTrials == DialogResult.Cancel)
                //{
                //    goto MakeAssignments;
                //}
                //}
                //else if (resultAssign == DialogResult.Cancel)
                //{
                //    goto ReadFile;
                //}
                //}
                //else if (resultRawData == DialogResult.Cancel)
                //{
                //    goto OpenFile;
                //}
                //    }
                //}
            }
            catch (Exception ex)
            {
                string message = "Something failed during import." + Environment.NewLine
                                 + "Please try again with other settings. " + Environment.NewLine + "Error: " + ex.Message
                                 + Environment.NewLine + ex.StackTrace ;
                ExceptionMethods.ProcessErrorMessage(message);
                if (asciiSetting.WaitingSplash.IsBusy)
                {
                    asciiSetting.WaitingSplash.CancelAsync();
                }
            }

            //try
            //{
            //    asciiSetting = new ASCIISettings();
            //    detectionSetting = new DetectionSettings();

            //    var objfrmImportAssistent = new ImportRawDataAssistentDialog();
            //    if (objfrmImportAssistent.ShowDialog() == DialogResult.OK)
            //    {
            //    OpenFile:
            //        if (asciiSetting.FileDialog.ShowDialog() == DialogResult.OK)
            //        {
            //            // Save filename
            //            string filename = asciiSetting.FileDialog.FileName;

            //            // Ask the user to use a settings file
            //            // and loads it into the importsettings, if true.
            //            AskforUsingSettingsFile();

            //            // Save import file
            //            asciiSetting.Filename = filename;

            //            // Set import mode
            //            detectionSetting.ImportType = ImportTypes.Rawdata;

            //            var objfrmImportReadFile = new ImportParseFileDialog(ref asciiSetting);
            //        ReadFile:
            //            DialogResult resultRawData = objfrmImportReadFile.ShowDialog();
            //            if (resultRawData == DialogResult.OK)
            //            {
            //                var objfrmImportRawDataAssignColumns = new ImportRawDataAssignColumnsDialog();

            //            MakeAssignments:
            //                DialogResult resultAssign = objfrmImportRawDataAssignColumns.ShowDialog();
            //                if (resultAssign == DialogResult.OK)
            //                {
            //                    var objfrmImportTrials = new ImportTrialsDialog(ref asciiSetting, ref detectionSetting);

            //                CheckTrials:
            //                    DialogResult resultTrials = objfrmImportTrials.ShowDialog();
            //                    if (resultTrials == DialogResult.OK)
            //                    {
            //                        var objfrmImportImages = new ImportImagesDialog(ref asciiSetting, ref detectionSetting);

            //                        DialogResult resultImages = objfrmImportImages.ShowDialog();
            //                        if (resultImages == DialogResult.OK)
            //                        {
            //                            if (MessageBox.Show(
            //                              "Would you like to save the import settings ?",
            //                              Application.ProductName,
            //                              MessageBoxButtons.YesNo,
            //                              MessageBoxIcon.Question) == DialogResult.Yes)
            //                            {
            //                                SaveImportSettings();
            //                            }

            //                            // Show import splash window
            //                            asciiSetting.WaitingSplash.RunWorkerAsync();

            //                            // Give some time to show the splash ...
            //                            Application.DoEvents();

            //                            // Read log file again, but complete
            //                            GenerateOgamaRawDataList(-1);

            //                            // Generate the trials
            //                            GenerateOgamaSubjectAndTrialList();

            //                            // Save the import into ogamas database and the mdf file.
            //                            bool successful = SaveImportIntoTablesAndDB();

            //                            // Create slideshow trials
            //                            GenerateOgamaSlideshowTrials(detectionSetting, mainWindow);

            //                            // Calculate Fixations
            //                            CalculateFixations(mainWindow);

            //                            // Clear lists
            //                            SubjectList.Clear();
            //                            TrialList.Clear();
            //                            RawDataList.Clear();

            //                            // Import has finished.
            //                            asciiSetting.WaitingSplash.CancelAsync();

            //                            // Inform user about success.
            //                            if (successful)
            //                            {
            //                                string message = "Import data successfully written to database." + Environment.NewLine
            //                                                 + "Please don´t forget to move the stimuli images to the SlideResources subfolder"
            //                                                 + "of the experiment, otherwise no images will be shown.";
            //                                ExceptionMethods.ProcessMessage("Success", message);
            //                            }
            //                            else
            //                            {
            //                                string message = "Import had errors. Some or all of the import data "
            //                                                 + "could not be written the database.";
            //                                ExceptionMethods.ProcessErrorMessage(message);
            //                            }
            //                        }
            //                        else if (resultImages == DialogResult.Cancel)
            //                        {
            //                            goto CheckTrials;
            //                        }
            //                    }
            //                    else if (resultTrials == DialogResult.Cancel)
            //                    {
            //                        goto MakeAssignments;
            //                    }
            //                }
            //                else if (resultAssign == DialogResult.Cancel)
            //                {
            //                    goto ReadFile;
            //                }
            //            }
            //            else if (resultRawData == DialogResult.Cancel)
            //            {
            //                goto OpenFile;
            //            }
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    string message = "Something failed during import." + Environment.NewLine
            //                     + "Please try again with other settings. " + Environment.NewLine + "Error: " + ex.Message;
            //    ExceptionMethods.ProcessErrorMessage(message);
            //    if (asciiSetting.WaitingSplash.IsBusy)
            //    {
            //        asciiSetting.WaitingSplash.CancelAsync();
            //    }
            //}
        }

        private static void GenerateKeyboardEventList()
        {
            long startedmilis = asciiSetting.StartTime;
            string currentSubjectName = detectionSetting.SubjectName;
            StreamReader MEDataFile = new StreamReader(asciiSetting.GetKBDataPath());
            JavaScriptSerializer deserializer = new JavaScriptSerializer();
            var json = deserializer.Deserialize<dynamic>(MEDataFile.ReadToEnd());
            SortedList<int, long> trial2Time = detectionSetting.TrialSequenceToStarttimeAssignments;
            int currentTrialSequence = 0;
            if (trial2Time.Count > 0)
            {
                currentTrialSequence = trial2Time.Keys[0];
            }
            foreach (dynamic record in json)
            {
                if (record["EventType"] == KEY_PRESS)
                {
                    continue;
                }
                var newEventData = new TrialEventsData();
                newEventData.SubjectName = currentSubjectName;
                newEventData.TrialSequence = currentTrialSequence;
                DateTime time = DateTime.Parse(record["Timestamp"]);
                DateTimeOffset timeOffset = new DateTimeOffset(time);
                newEventData.EventTime = timeOffset.ToUnixTimeMilliseconds() - startedmilis;
                newEventData.EventType = "Key";
                if (record["EventType"] == KEY_UP)
                {
                    newEventData.EventTask = "Up";
                }
                if (record["EventType"] == KEY_DOWN)
                {
                    newEventData.EventTask = "Down";
                }
                newEventData.EventParam = "Key: " + record["KeyCode"];
                EventList.Add(newEventData);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// This method calculates the fixations for the subjects
        ///   that are currently imported.
        /// </summary>
        /// <param name="mainWindow">
        /// The <see cref="MainForm"/> to get access to the status label.
        /// </param>
        private static void CalculateFixations(MainForm mainWindow)
        {
            mainWindow.StatusLabel.Text = "Calculating Fixations ...";

            foreach (SubjectsData subject in SubjectList)
            {
                // Get trial data of current subject
                DataTable trialsTable = Document.ActiveDocument.DocDataSet.TrialsAdapter.GetDataBySubject(subject.SubjectName);

                // Calculate fixations
                var calculationObject = new FixationCalculation();
                calculationObject.CalcFixations(SampleType.Gaze, subject.SubjectName, trialsTable, null, null);
                calculationObject.CalcFixations(SampleType.Mouse, subject.SubjectName, trialsTable, null, null);
            }

            mainWindow.StatusLabel.Text = "Fixation calculation done ...";
        }

        /// <summary>
        /// Reads an OGAMA import settings file.
        /// </summary>
        /// <param name="filePath">
        /// A <see cref="string"/> with the path to the
        ///   OGAMA import settings xml file.
        /// </param>
        /// <returns>
        /// <strong>True</strong> if successful,
        ///   otherwise <strong>null</strong>.
        /// </returns>
        private static bool DeserializeSettings(string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open))
                {
                    // Create an instance of the XmlSerializer class;
                    // specify the type of object to be deserialized 
                    var serializer = new XmlSerializer(typeof(MergedSettings));

                    /* Use the Deserialize method to restore the object's state with
                    data from the XML document. */
                    var settings = (MergedSettings)serializer.Deserialize(fs);

                    //asciiSetting = settings.AsciiSetting;
                    detectionSetting = settings.DetectionSetting;
                }
            }
            catch (Exception ex)
            {
                ExceptionMethods.HandleException(ex);

                return false;
            }

            return true;
        }

        private static DateTime GetStartTime()
        {
            dynamic states = null;
            try
            {
                states = ASCIISettings.GetScreenCaptureStates();
            }
            catch (Exception e)
            {
                states = ASCIISettings.GetEyeTrackerStates();
            }

            foreach (var state in states)
            {
                if (state["State"] == "Recording")
                {
                    return DateTime.Parse(state["Timestamp"]);
                }
            }

            throw new System.MissingFieldException("Recording state timestamp not found");
        }

        /// <summary>
        /// Generate the list of raw data under the current
        ///   parsing conditions.
        /// </summary>
        /// <param name="numberOfImportLines">
        /// An <see cref="int"/>
        ///   with the max number of lines to import.
        ///   Set it to -1 to use all lines.
        /// </param>
        /// <remarks>
        /// This is the heart of the class. If something does not work as expected,
        ///   first have a look here.
        /// </remarks>
        private static void GenerateOgamaRawDataList(int numberOfImportLines)
        {
            // Clear existing values
            RawDataList.Clear();
            EventList.Clear();

            double lastTimeInFileTime = -1;

            // Retrieve existing slideshow trials (to check matching filenames for 
            // correct trial ID numbering
            List<Trial> trials = Document.ActiveDocument.ExperimentSettings.SlideShow.Trials;
            List<string> trialNames = Document.ActiveDocument.ExperimentSettings.SlideShow.GetTrialNames();

            // Enumerate the columns in the import file and assign their title.
            //var columnsImportNum = new Dictionary<string, int>();

            // Get the assigned titles of the import columns for Ogamas columns
            //string strSubjectNameImportColumn = asciiSetting.ColumnAssignments["SubjectName"];
            //string strTrialSequenceImportColumn = asciiSetting.ColumnAssignments["TrialSequence"];
            //string strTrialIDImportColumn = asciiSetting.ColumnAssignments["TrialID"];
            //string strTrialImageImportColumn = asciiSetting.ColumnAssignments["TrialImage"];
            //string strCategoryImportColumn = asciiSetting.ColumnAssignments["TrialCategory"];
            //string strTimeImportColumn = asciiSetting.ColumnAssignments["Time"];
            //string strPupilDiaXImportColumn = asciiSetting.ColumnAssignments["PupilDiaX"];
            //string strPupilDiaYImportColumn = asciiSetting.ColumnAssignments["PupilDiaY"];
            //string strGazePosXImportColumn = asciiSetting.ColumnAssignments["GazePosX"];
            //string strGazePosYImportColumn = asciiSetting.ColumnAssignments["GazePosY"];
            //string strMousePosXImportColumn = asciiSetting.ColumnAssignments["MousePosX"];
            //string strMousePosYImportColumn = asciiSetting.ColumnAssignments["MousePosY"];
            int counter = 0;
            int trialCounter = 0;
            int currentTrialSequence = 0;
            bool isLastTrial = false;
            string lastSubjectName = "#";
            string currentSubjectName = Path.GetFileName(asciiSetting.Folder);

            if (!Char.IsLetter(currentSubjectName[0]))
            {
                currentSubjectName = "I" + currentSubjectName;
            }

            Regex rgx = new Regex("[^a-zA-Z0-9]");
            currentSubjectName = rgx.Replace(currentSubjectName, "");

            SortedList<int, long> trial2Time = detectionSetting.TrialSequenceToStarttimeAssignments;
            if (trial2Time.Count > 0)
            {
                currentTrialSequence = trial2Time.Keys[0];
            }
            DateTime started = GetStartTime();
            DateTimeOffset startedOffset = new DateTimeOffset(started.ToUniversalTime());
            long startedmilis = startedOffset.ToUnixTimeMilliseconds();

            detectionSetting.SubjectName = currentSubjectName;
            detectionSetting.TimeFactor = 1;
            detectionSetting.ImportType = ImportTypes.Rawdata;
            asciiSetting.StartTime = startedmilis;

            StreamReader ETDataFile = new StreamReader(asciiSetting.GetETDataPath());
            JavaScriptSerializer deserializer = new JavaScriptSerializer();
            deserializer.MaxJsonLength = 99999999;
            var json = deserializer.Deserialize<dynamic>(ETDataFile.ReadToEnd());

            foreach(dynamic record in json)
            {
                if (Array.IndexOf(VALIDITY_WHITELIST,  record["Validity"]) == -1)
                {
                    continue;
                }
                var newRawData = new RawData();
                newRawData.SubjectName = currentSubjectName;
                dynamic gazeData = record["RightEye"];
                if (record["Validity"] == BOTH)
                {
                    if (asciiSetting.getPreferredEye() == "Average")
                    {
                        gazeData["GazePoint2D"]["X"] = ((double) record["RightEye"]["GazePoint2D"]["X"] + (double) record["LeftEye"]["GazePoint2D"]["X"]) / 2;
                        gazeData["GazePoint2D"]["Y"] = ((double) record["RightEye"]["GazePoint2D"]["Y"] + (double) record["LeftEye"]["GazePoint2D"]["Y"]) / 2;
                    }
                    else
                    {
                        gazeData = record[asciiSetting.getPreferredEye() + "Eye"];
                    }
                }
                else
                {
                    gazeData = record[record["Validity"] + "Eye"];
                }
                
                //                newRawData.GazePosX = (float) gazeData["GazePoint2D"]["X"] * 1920;
                newRawData.GazePosX = (float) gazeData["GazePoint2D"]["X"] * Document.ActiveDocument.ExperimentSettings.WidthStimulusScreen;
                //newRawData.GazePosX *= 1920;
//                newRawData.GazePosY = (float) gazeData["GazePoint2D"]["Y"] * 1080;
                newRawData.GazePosY = (float ) gazeData["GazePoint2D"]["Y"] * Document.ActiveDocument.ExperimentSettings.HeightStimulusScreen;
                //newRawData.GazePosY *= 1080; 
                newRawData.PupilDiaX = (float) gazeData["PupilDiameter"];
                newRawData.PupilDiaX = (float) gazeData["PupilDiameter"];
                DateTime time = DateTime.Parse(record["Timestamp"]);
                //time.Subtract(started);
                DateTimeOffset timeOffset = new DateTimeOffset(time);
                newRawData.Time = timeOffset.ToUnixTimeMilliseconds() - startedmilis;
                RawDataList.Add(newRawData);
                counter++;
            }
            ETDataFile.Close();
            StreamReader MEDataFile = new StreamReader(asciiSetting.GetMEDataPath());
            json = deserializer.Deserialize<dynamic>(MEDataFile.ReadToEnd());
            foreach (dynamic record in json)
            {
                if (Array.IndexOf(EVENTTYPES_WHITELIST, record["EventType"]) == -1)
                {
                    continue;
                }
                if (record["EventType"] == MOVE)
                {
                    var newRawData = new RawData();
                    newRawData.SubjectName = currentSubjectName;
                    DateTime time = DateTime.Parse(record["Timestamp"]);
                    //time.Subtract(started);
                    DateTimeOffset timeOffset = new DateTimeOffset(time);
                    newRawData.Time = timeOffset.ToUnixTimeMilliseconds() - startedmilis;
                    newRawData.MousePosX = record["X"];
                    newRawData.MousePosY = record["Y"];
                    RawDataList.Add(newRawData);
                }
                else
                {
                    var newEventData = new TrialEventsData();
                    newEventData.SubjectName = currentSubjectName;
                    newEventData.TrialSequence = currentTrialSequence;
                    DateTime time = DateTime.Parse(record["Timestamp"]);
                    DateTimeOffset timeOffset = new DateTimeOffset(time);
                    newEventData.EventTime = timeOffset.ToUnixTimeMilliseconds() - startedmilis;
                    newEventData.EventType = "Mouse";
                    if (record["EventType"] == BUTTON_UP)
                    {
                        newEventData.EventTask = "Up";
                    }
                    if (record["EventType"] == BUTTON_DOWN)
                    {
                        newEventData.EventTask = "Down";
                    }
                    newEventData.EventParam = String.Format("Mouse: {0} ({1},{2})", record["Button"], record["X"], record["Y"]);
                    EventList.Add(newEventData);
                }
            }
            MEDataFile.Close();
        }

        /// <summary>
        ///   This method iterates the imported raw data rows to
        ///   catch the trial changes that are detected during the call
        ///   of <see cref="GenerateOgamaRawDataList(int)" />.
        ///   The trials are then written into the trial list.
        /// </summary>
        private static void GenerateOgamaSubjectAndTrialList()
{
    // Clear foregoing imports.
    TrialList.Clear();
    SubjectList.Clear();

    if (RawDataList.Count == 0)
    {
        // string message = "The parsing of the log file into OGAMAs " +
        // "Raw data columns failed. No lines have been successfully read. " +
        // Environment.NewLine +
        // "So the trial generation could not be started." +
        // Environment.NewLine + "Please change the import settings and try again";
        // ExceptionMethods.ProcessErrorMessage(message);
        return;
    }

    // Initializes variables
    int currentSequence = 0;
    int lastSequence = -5;
    string currentSubject = "#";
    string lastSubject = "#";
    int overallTrialCounter = 0;
    int trialCounter = 0;
    int subjectCounter = 0;

    // Iterate raw data list
    for (int i = 0; i < RawDataList.Count; i++)
    {
        RawData importRow = RawDataList[i];
        currentSequence = importRow.TrialSequence;
        currentSubject = importRow.SubjectName;

        // If subject has changed write new subject table entry.
        if (currentSubject != lastSubject)
        {
            var newSubjectsData = new SubjectsData();
            newSubjectsData.SubjectName = currentSubject;
            SubjectList.Add(newSubjectsData);

            if (subjectCounter > 0)
            {
                TrialsData tempSubjetData = TrialList[overallTrialCounter - 1];
                tempSubjetData.Duration = (int)(RawDataList[i - 1].Time - tempSubjetData.TrialStartTime);
                TrialList[overallTrialCounter - 1] = tempSubjetData;
            }

            lastSubject = currentSubject;
            lastSequence = -5;
            trialCounter = 0;
            subjectCounter++;
        }

        // If trial has changed parse the trial information to 
        // create a trial entry in the trialList.
        if (currentSequence != lastSequence)
        {
            string subject = importRow.SubjectName != null ? importRow.SubjectName : "Subject1";
            string categorie = importRow.Category != null ? importRow.Category : string.Empty;
            int trialID = currentSequence;
            if (detectionSetting.TrialSequenceToTrialIDAssignments.ContainsKey(currentSequence))
            {
                trialID = detectionSetting.TrialSequenceToTrialIDAssignments[currentSequence];
            }

            string image = "No image file specified";

            switch (detectionSetting.StimuliImportMode)
            {
                case StimuliImportModes.UseiViewXMSG:
                    if (detectionSetting.ImageDictionary.ContainsKey(currentSequence))
                    {
                        image = detectionSetting.ImageDictionary[currentSequence];
                    }

                    break;
                case StimuliImportModes.UseImportColumn:
                case StimuliImportModes.UseAssignmentTable:
                    if (detectionSetting.TrialSequenceToTrialIDAssignments.ContainsKey(currentSequence))
                    {
                        trialID = detectionSetting.TrialSequenceToTrialIDAssignments[currentSequence];
                        if (detectionSetting.TrialIDToImageAssignments.ContainsKey(trialID))
                        {
                            image = detectionSetting.TrialIDToImageAssignments[trialID];
                        }
                    }

                    break;
                case StimuliImportModes.SearchForImageEnding:
                    if (detectionSetting.ImageDictionary.ContainsKey(currentSequence))
                    {
                        image = detectionSetting.ImageDictionary[currentSequence];
                    }

                    break;
            }

            // Add empty trial to sequence numbering.
            if (detectionSetting.StimuliImportMode != StimuliImportModes.UseImportColumn
                && image == "No image file specified")
            {
                if (!detectionSetting.TrialSequenceToTrialIDAssignments.ContainsKey(currentSequence))
                {
                    detectionSetting.TrialSequenceToTrialIDAssignments.Add(currentSequence, 0);
                }
            }

            long time = 0;
            switch (detectionSetting.TrialImportMode)
            {
                ////// Use the table timing
                ////if (detectionSetting.TrialSequenceToStarttimeAssignments.ContainsKey(currentSequence))
                ////{
                ////  time = detectionSetting.TrialSequenceToStarttimeAssignments[currentSequence];
                ////}
                //// break;
                case TrialSequenceImportModes.UseAssignmentTable:
                case TrialSequenceImportModes.UseMSGLines:
                case TrialSequenceImportModes.UseImportColumn:

                    // Use the raw data timing
                    time = importRow.Time;
                    break;
            }

            // Create trial row
            var newTrialData = new TrialsData();
            newTrialData.SubjectName = subject;
            newTrialData.TrialSequence = currentSequence;
            newTrialData.TrialID = trialID;
            newTrialData.TrialName = image;
            newTrialData.Category = categorie;
            newTrialData.TrialStartTime = time;
            newTrialData.Duration = -1;
            TrialList.Add(newTrialData);

            lastSequence = currentSequence;
            trialCounter++;
            overallTrialCounter++;

            // Calculate trial duration for foregoing trial.
            if (trialCounter > 1)
            {
                TrialsData tempSubjetData = TrialList[overallTrialCounter - 2];
                int duration = 0;
                switch (detectionSetting.TrialImportMode)
                {
                    case TrialSequenceImportModes.UseAssignmentTable:

                        // Use the table timing
                        duration = (int)(time - tempSubjetData.TrialStartTime);
                        break;
                    case TrialSequenceImportModes.UseMSGLines:
                    case TrialSequenceImportModes.UseImportColumn:

                        // Use the raw data timing
                        duration = (int)(RawDataList[i].Time - tempSubjetData.TrialStartTime);
                        break;
                }

                //// If there is lot of time (>200ms) left between last and current trial
                //// don´t use the space in between for the duration value.
                ////if (rawDataList[i].Time - rawDataList[i - 1].Time > 200)
                ////{
                ////  duration = (int)(rawDataList[i - 1].Time - tempSubjetData.TrialStartTime);
                ////}
                tempSubjetData.Duration = duration;
                TrialList[overallTrialCounter - 2] = tempSubjetData;
            }
        }
    }

    // Reached end of rawdatalist, so add last trial duration value from last entry
    if (trialCounter >= 1)
    {
        TrialsData tempSubjetData = TrialList[overallTrialCounter - 1];
        tempSubjetData.Duration = (int)(RawDataList[RawDataList.Count - 1].Time - tempSubjetData.TrialStartTime);
        TrialList[overallTrialCounter - 1] = tempSubjetData;
    }
}

/// <summary>
///   This method writes the data that is written in the lists during
///   import to OGAMAs dataset.
///   If this could be successfully done the whole new data is
///   written to the database (.mdf).
/// </summary>
/// <returns>
///   <strong>True</strong> if successful, otherwise
///   <strong>false</strong>.
/// </returns>
private static bool SaveImportIntoTablesAndDB()
{
    //Dictionary<string, List<RawData>> rawDataBySubject = SplitRawDataListBySubjects(RawDataList);
    //Dictionary<string, List<TrialsData>> trialDataBySubject = SplitTrialDataListBySubjects(TrialList);
    int subjectErrorCounter = 0;
    try
    {
    //    foreach (SubjectsData subject in SubjectList)
      //  {
            SubjectsData subject = SubjectList[0];
            string testSub = subject.SubjectName;
            if (!Queries.ValidateSubjectName(ref testSub, false))
            {
                string message = testSub + " subject has unallowed names or "
                                                     + "their names already exists in the experiments database." + Environment.NewLine
                                                     + "Please modify your import file and change the subject name, or delete "
                                                     + "the existing database entry.";
                ExceptionMethods.ProcessMessage("Unallowed subject names", message);
                }

            //List<RawData> subjectRawData = rawDataBySubject[subject.SubjectName];
            //List<TrialsData> subjectTrialsData = trialDataBySubject[subject.SubjectName];
            if (!Queries.WriteRawDataListToDataSet(subject.SubjectName, RawDataList))
            {
                throw new DataException("The new raw data table could not be written into the dataset.");
            }

            // Creates an empty raw data table in the mdf database
            Queries.CreateRawDataTableInDB(subject.SubjectName);

            // Push changes to database
            Document.ActiveDocument.DocDataSet.AcceptChanges();

            // Write RawDataTable into File with Bulk Statement
            Queries.WriteRawDataWithBulkStatement(subject.SubjectName);

            // Save subject information to dataset
            if (!Queries.WriteSubjectToDataSet(subject))
            {
                throw new DataException("The new subject information could not be written into the dataset.");
            }

            // Save trial information to dataset
            if (!Queries.WriteTrialsDataListToDataSet(TrialList))
            {
                throw new DataException("The new trials table could not be written into the dataset.");
            }

            if (!Queries.WriteTrialEventsDataListToDataSet(EventList))
            {
                throw new DataException("The new trial events could not be written into the dataset.");
            }
        //}

        Document.ActiveDocument.DocDataSet.EnforceConstraints = false;

        // Update subjects and trials table in the mdf database
        int affectedRows = Document.ActiveDocument.DocDataSet.TrialsAdapter.Update(Document.ActiveDocument.DocDataSet.Trials);
        Console.WriteLine(affectedRows + "Trial updates written");
        affectedRows = Document.ActiveDocument.DocDataSet.SubjectsAdapter.Update(Document.ActiveDocument.DocDataSet.Subjects);
        Console.WriteLine(affectedRows + "Subject updates written");

        Document.ActiveDocument.DocDataSet.AcceptChanges();
        Document.ActiveDocument.DocDataSet.CreateRawDataAdapters();
    }
    catch (Exception ex)
    {
        ExceptionMethods.HandleException(ex);

        // CleanUp
        // First reject changes (remove trial and subject table modifications)
        Document.ActiveDocument.DocDataSet.RejectChanges();

        foreach (SubjectsData subject in SubjectList)
        {
            // Remove eventually added raw data table in dataset
            if (Document.ActiveDocument.DocDataSet.Tables.Contains(subject.SubjectName + "Rawdata"))
            {
                Document.ActiveDocument.DocDataSet.Tables.Remove(subject.SubjectName + "Rawdata");
            }

            // Remove raw data table in database file (.mdf)
            Queries.DeleteRawDataTableInDB(subject.SubjectName);
        }

        return false;
    }
    finally
    {
        Document.ActiveDocument.DocDataSet.EnforceConstraints = true;
    }

    return true;
}

/// <summary>
/// This method shows a dialog asking for saving the current
///   import settings to hard disk.
///   They are persited in xml format.
/// </summary>
private static void SaveImportSettings()
{
    var ofdSaveSettings = new SaveFileDialog
    {
        DefaultExt = "ois",
        FileName = "*.ois",
        FilterIndex = 1,
        Filter = "Ogama import settings files|*.ois",
        Title = "Please specify settings filename",
        InitialDirectory = Properties.Settings.Default.ImportSettingsPath
    };

    if (ofdSaveSettings.ShowDialog() == DialogResult.OK)
    {
        SerializeSettings(ofdSaveSettings.FileName);
    }
}

///////////////////////////////////////////////////////////////////////////////
// Small helping Methods                                                     //
///////////////////////////////////////////////////////////////////////////////

/// <summary>
/// Saves the current import setting to a OGAMA import settings file.
///   Extension ".ois"
/// </summary>
/// <param name="filePath">
/// A <see cref="string"/> with the path to the
///   OGAMA target import settings xml file.
/// </param>
private static void SerializeSettings(string filePath)
{
    try
    {
        using (TextWriter writer = new StreamWriter(filePath))
        {
            var settings = new MergedSettings { AsciiSetting = null, DetectionSetting = detectionSetting };

            var serializer = new XmlSerializer(typeof(MergedSettings));
            serializer.Serialize(writer, settings);
        }
    }
    catch (Exception ex)
    {
        ExceptionMethods.HandleException(ex);
    }
}

/// <summary>
/// This method splits the given raw data list into a
///   dictionary of raw data lists separated by subjects.
/// </summary>
/// <remarks>
/// This is done to enable writing a raw data table for each subject.
/// </remarks>
/// <param name="wholeRawDataList">
/// A <see cref="List{RawData}"/>
///   with all the imported samples.
/// </param>
/// <returns>
/// A Dictionary with the splitted input.
/// </returns>
private static Dictionary<string, List<RawData>> SplitRawDataListBySubjects(List<RawData> wholeRawDataList)
{
    // Create the return dictionary
    var rawDataBySubject = new Dictionary<string, List<RawData>>();

    // Get First subject name
    string lastSubjectName = wholeRawDataList[0].SubjectName;

    // Create list for current subject
    var currentList = new List<RawData>();

    // Iterate whole raw data list and add for each subject a 
    // new entry in the rawDataBySubject list.
    foreach (RawData data in wholeRawDataList)
    {
        if (data.SubjectName != lastSubjectName)
        {
            rawDataBySubject.Add(lastSubjectName, currentList);
            currentList = new List<RawData>();
            lastSubjectName = data.SubjectName;
        }

        currentList.Add(data);
    }

    // Add last subject
    rawDataBySubject.Add(lastSubjectName, currentList);

    // Return list.
    return rawDataBySubject;
}

        #endregion
    }
}
