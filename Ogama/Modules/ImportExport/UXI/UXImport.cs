using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FFmpeg.NET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ogama.DataSet;

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
        private static readonly String[] VALIDITY_WHITELIST = {BOTH, LEFT, RIGHT};
        private static readonly String[] EVENTTYPES_WHITELIST = {BUTTON_DOWN, BUTTON_UP, MOVE};

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
        private static TrialsData newTrialData;
        private static SQLiteOgamaDataSet.RawdataDataTable subjectRawDataTable;

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
            get { return asciiSetting; }
        }

        /// <summary>
        ///   Gets the specialized settings used during this import session.
        /// </summary>
        /// <value>A <see cref="DetectionSettings" />.</value>
        /// <seealso cref="DetectionSettings" />
        public static DetectionSettings DetectionSetting
        {
            get { return detectionSetting; }
        }

        #endregion

        #region Public Methods and Operators

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
                    {Modified = true, MouseCursorVisible = true};

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

                var newTrial = new Trial(filename, trialID) {Name = filename};

                newTrial.Add(newSlide);

                if (trialNames.Contains(filename) ||
                    (filename == string.Empty && trialNames.Contains("No stimulus detected")))
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
            if (!Document.ActiveDocument.SaveSettingsToFile(Document.ActiveDocument.ExperimentSettings.DocumentFilename)
            )
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
                MessageBox.Show("No projects have been found in selected folder and folders inside it.",
                    "Project not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        public static Task ImportVideo(string input, string output, CancellationToken token)
        {
            var inputFile = new MediaFile(input);
            var outputFile = new MediaFile(output);
            var e = new Engine("ffmpeg/ffmpeg.exe");
            return e.ConvertAsync(inputFile, outputFile, token);
        }

        public static void Run(List<String> values, IProgress<int> progress, CancellationToken token,
            String preferredEye, bool importVideo, bool mouseMovement, bool mouseEvents, bool keyboardEvents)
        {
            int p = 0;
            foreach (String value in values)
            {
                PrepareRun(value);
                Task videoTask = null;
                if (importVideo)
                {
                    if (File.Exists(asciiSetting.GetScreenVideoAVIPath()))
                    {
                        File.Copy(asciiSetting.GetScreenVideoAVIPath(), Path.Combine(Document.ActiveDocument.ExperimentSettings.ThumbsPath,
                            detectionSetting.SubjectName + "-" + "0.avi"));
                    }
                    else
                    {
                        videoTask = ImportVideo(asciiSetting.GetScreenVideoMP4Path(),
                            Path.Combine(Document.ActiveDocument.ExperimentSettings.ThumbsPath,
                                detectionSetting.SubjectName + "-" + "0.avi"), token);
                    }
                }

                asciiSetting.PreferredEye = preferredEye;
                asciiSetting.ImportMouseMovement = mouseMovement;
                asciiSetting.ImportMouseEvents = mouseEvents;
                asciiSetting.ImportKeyboardEvets = keyboardEvents;
                Run(value);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (videoTask != null)
                {
                    videoTask.Wait();
                }

                progress.Report(++p);
            }

            string message = "Import data was successfull." + Environment.NewLine
                                                                             + "Please don´t forget to save data in database module." +
                                                            Environment.NewLine
                                                                             + "Also do not forget that some modules (for example export to AVI) requires defined screen recording slide, that can be created in Design module.";
            ExceptionMethods.ProcessMessage("Success", message);
        }

        public static void PrepareRun(Object dir)
        {
            Application.DoEvents();

            asciiSetting = new UXISettings();
            detectionSetting = new DetectionSettings();
            asciiSetting.Folder = (string) dir;

            RawDataList.Clear();
            EventList.Clear();

            string currentSubjectName = Path.GetFileName(asciiSetting.Folder);

            if (!Char.IsLetter(currentSubjectName[0]))
            {
                currentSubjectName = "I" + currentSubjectName;
            }

            Regex rgx = new Regex("[^a-zA-Z0-9]");
            currentSubjectName = rgx.Replace(currentSubjectName, "");

            DateTime started = GetStartTime();
            DateTimeOffset startedOffset = new DateTimeOffset(started.ToUniversalTime());
            long startedmilis = startedOffset.ToUnixTimeMilliseconds();

            detectionSetting.SubjectName = currentSubjectName;
            detectionSetting.TimeFactor = 1;
            detectionSetting.ImportType = ImportTypes.Rawdata;
            asciiSetting.StartTime = startedmilis;
            SubjectsData data = new SubjectsData();
            data.SubjectName = currentSubjectName;
            SubjectList.Add(data);
            newTrialData = new TrialsData();
            newTrialData.SubjectName = currentSubjectName;
            newTrialData.TrialSequence = 0;
            newTrialData.TrialID = 1;
            newTrialData.TrialStartTime = 0;

            subjectRawDataTable = new SQLiteOgamaDataSet.RawdataDataTable();

            // Give it correct name
            subjectRawDataTable.TableName = currentSubjectName + "Rawdata";
        }

        public static void Run(Object dir)
        {
            try
            {
                GenerateOgamaRawDataList(-1);
                GenerateMouseDataList();
                if (asciiSetting.ImportKeyboardEvets)
                {
                    GenerateKeyboardEventList();
                }

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
            }
            catch (Exception ex)
            {
                string message = "Something failed during import." + Environment.NewLine
                                                                   + "Please try again with other settings. " +
                                                                   Environment.NewLine + "Error: " + ex.Message
                                                                   + Environment.NewLine + ex.StackTrace;
                ExceptionMethods.ProcessErrorMessage(message);
                if (asciiSetting.WaitingSplash.IsBusy)
                {
                    asciiSetting.WaitingSplash.CancelAsync();
                }
            }
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
                newEventData.EventID = EventList.Count + 1;
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
                DataTable trialsTable =
                    Document.ActiveDocument.DocDataSet.TrialsAdapter.GetDataBySubject(subject.SubjectName);

                // Calculate fixations
                var calculationObject = new FixationCalculation();
                calculationObject.CalcFixations(SampleType.Gaze, subject.SubjectName, trialsTable, null, null);
                calculationObject.CalcFixations(SampleType.Mouse, subject.SubjectName, trialsTable, null, null);
            }

            mainWindow.StatusLabel.Text = "Fixation calculation done ...";
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


            double lastTimeInFileTime = -1;

            // Retrieve existing slideshow trials (to check matching filenames for 
            // correct trial ID numbering
            List<Trial> trials = Document.ActiveDocument.ExperimentSettings.SlideShow.Trials;
            List<string> trialNames = Document.ActiveDocument.ExperimentSettings.SlideShow.GetTrialNames();

            int counter = 0;
            int trialCounter = 0;
            bool isLastTrial = false;
            string lastSubjectName = "#";
            SortedList<int, long> trial2Time = detectionSetting.TrialSequenceToStarttimeAssignments;
            int currentTrialSequence = 0;
            if (trial2Time.Count > 0)
            {
                currentTrialSequence = trial2Time.Keys[0];
            }

            string currentSubjectName = detectionSetting.SubjectName;
            var startedmilis = asciiSetting.StartTime;

            using (FileStream fs = new FileStream(asciiSetting.GetETDataPath(), FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                while (jsonTextReader.Read())
                {
                    if (jsonTextReader.TokenType == JsonToken.StartObject)
                    {
                        JObject record = JObject.Load(jsonTextReader);
                        if (Array.IndexOf(VALIDITY_WHITELIST, record["Validity"].ToString()) == -1)
                        {
                            continue;
                        }

                        var newRawData = new RawData();
                        newRawData.SubjectName = currentSubjectName;
                        dynamic gazeData = record["RightEye"];
                        if (record["Validity"].ToString() == BOTH)
                        {
                            if (asciiSetting.PreferredEye == "Average")
                            {
                                gazeData["GazePoint2D"]["X"] =
                                    ((double) record["RightEye"]["GazePoint2D"]["X"] +
                                     (double) record["LeftEye"]["GazePoint2D"]["X"]) / 2;
                                gazeData["GazePoint2D"]["Y"] =
                                    ((double) record["RightEye"]["GazePoint2D"]["Y"] +
                                     (double) record["LeftEye"]["GazePoint2D"]["Y"]) / 2;
                            }
                            else
                            {
                                gazeData = record[asciiSetting.PreferredEye + "Eye"];
                            }
                        }
                        else
                        {
                            gazeData = record[record["Validity"] + "Eye"];
                        }

                        newRawData.GazePosX = (float) gazeData["GazePoint2D"]["X"] *
                                              Document.ActiveDocument.ExperimentSettings.WidthStimulusScreen;
                        newRawData.GazePosY = (float) gazeData["GazePoint2D"]["Y"] *
                                              Document.ActiveDocument.ExperimentSettings.HeightStimulusScreen;
                        newRawData.PupilDiaX = (float) gazeData["PupilDiameter"];
                        newRawData.PupilDiaY = (float) gazeData["PupilDiameter"];
                        DateTime time = DateTime.Parse(record["Timestamp"].ToString());
                        DateTimeOffset timeOffset = new DateTimeOffset(time);
                        newRawData.Time = timeOffset.ToUnixTimeMilliseconds() - startedmilis;
                        counter++;
                        if (RawDataList.Count > 10000)
                        {
                            Queries.SaveDataToTable(RawDataList.ToArray(), subjectRawDataTable);
                            RawDataList.Clear();
                        }

                        RawDataList.Add(newRawData);
                    }
                }
            }


            newTrialData.Duration = (int) RawDataList[RawDataList.Count - 1].Time;
            TrialList.Add(newTrialData);
            //ETDataFile.Close();
            Document.ActiveDocument.DocDataSet.Tables.Add(subjectRawDataTable);
            Queries.SaveDataToTable(RawDataList.ToArray(), subjectRawDataTable);
            RawDataList.Clear();

        }

        private static void GenerateMouseDataList()
        {
            var currentTrialSequence = 0;
            string currentSubjectName = detectionSetting.SubjectName;
            var startedmilis = asciiSetting.StartTime;

            using (FileStream fs = new FileStream(asciiSetting.GetMEDataPath(), FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                while (jsonTextReader.Read())
                {
                    if (jsonTextReader.TokenType == JsonToken.StartObject)
                    {
                        JObject record = JObject.Load(jsonTextReader);
                        if (Array.IndexOf(EVENTTYPES_WHITELIST, record["EventType"].ToString()) == -1)
                        {
                            continue;
                        }

                        if (record["EventType"].ToString() == MOVE)
                        {
                            if (!asciiSetting.ImportMouseMovement)
                            {
                                continue;
                            }

                            var newRawData = new RawData();
                            newRawData.SubjectName = currentSubjectName;
                            DateTime time = DateTime.Parse(record["Timestamp"].ToString());
                            DateTimeOffset timeOffset = new DateTimeOffset(time);
                            newRawData.Time = timeOffset.ToUnixTimeMilliseconds() - startedmilis;
                            newRawData.MousePosX = record["X"].ToObject<int>();
                            newRawData.MousePosY = record["Y"].ToObject<int>();
                            RawDataList.Add(newRawData);
                        }
                        else
                        {
                            if (!asciiSetting.ImportMouseEvents)
                            {
                                continue;
                            }

                            var newEventData = new TrialEventsData();
                            newEventData.SubjectName = currentSubjectName;
                            newEventData.TrialSequence = currentTrialSequence;
                            DateTime time = DateTime.Parse(record["Timestamp"].ToString());
                            DateTimeOffset timeOffset = new DateTimeOffset(time);
                            newEventData.EventTime = timeOffset.ToUnixTimeMilliseconds() - startedmilis;
                            newEventData.EventType = "Mouse";
                            if (record["EventType"].ToString() == BUTTON_UP)
                            {
                                newEventData.EventTask = "Up";
                            }

                            if (record["EventType"].ToString() == BUTTON_DOWN)
                            {
                                newEventData.EventTask = "Down";
                            }

                            newEventData.EventParam =
                                String.Format("Mouse: {0} ({1},{2})", record["Button"], record["X"], record["Y"]);
                            newEventData.EventID = EventList.Count;
                            EventList.Add(newEventData);
                        }
                    }
                }
            }

            Queries.SaveDataToTable(RawDataList.ToArray(), subjectRawDataTable);
            RawDataList.Clear();
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
                        tempSubjetData.Duration = (int) (RawDataList[i - 1].Time - tempSubjetData.TrialStartTime);
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
                    int trialID = 1;

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
                                duration = (int) (time - tempSubjetData.TrialStartTime);
                                break;
                            case TrialSequenceImportModes.UseMSGLines:
                            case TrialSequenceImportModes.UseImportColumn:

                                // Use the raw data timing
                                duration = (int) (RawDataList[i].Time - tempSubjetData.TrialStartTime);
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
                tempSubjetData.Duration =
                    (int) (RawDataList[RawDataList.Count - 1].Time - tempSubjetData.TrialStartTime);
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
            int subjectErrorCounter = 0;
            try
            {
                SubjectsData subject = SubjectList[0];
                string testSub = subject.SubjectName;
                if (!Queries.ValidateSubjectName(ref testSub, false))
                {
                    string message = testSub + " subject has unallowed names or "
                                             + "their names already exists in the experiments database." +
                                             Environment.NewLine
                                             + "Please modify your import file and change the subject name, or delete "
                                             + "the existing database entry.";
                    ExceptionMethods.ProcessMessage("Unallowed subject names", message);
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

                Document.ActiveDocument.DocDataSet.EnforceConstraints = false;

                Document.ActiveDocument.DocDataSet.TrialEventsAdapter.Update(Document.ActiveDocument.DocDataSet
                    .TrialEvents);

                // Update subjects and trials table in the mdf database
                int affectedRows =
                    Document.ActiveDocument.DocDataSet.TrialsAdapter.Update(Document.ActiveDocument.DocDataSet.Trials);
                Console.WriteLine(affectedRows + "Trial updates written");
                affectedRows =
                    Document.ActiveDocument.DocDataSet.SubjectsAdapter.Update(Document.ActiveDocument.DocDataSet
                        .Subjects);
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
                    var settings = new MergedSettings {AsciiSetting = null, DetectionSetting = detectionSetting};

                    var serializer = new XmlSerializer(typeof(MergedSettings));
                    serializer.Serialize(writer, settings);
                }
            }
            catch (Exception ex)
            {
                ExceptionMethods.HandleException(ex);
            }
        }

        #endregion
    }
}