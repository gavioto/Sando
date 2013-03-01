﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Configuration.OptionsPages;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using Sando.Indexer.IndexFiltering;
using log4net; 
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Sando.Core;
using Sando.Core.Extensions;
using Sando.Core.Extensions.Configuration;
using Sando.Core.Extensions.Logging;
using Sando.Core.Tools;
using Sando.ExtensionContracts.ProgramElementContracts;
using Sando.ExtensionContracts.ResultsReordererContracts;
using Sando.Indexer.Searching;
using Sando.Parser;
using Sando.SearchEngine;
using Sando.Translation;
using Sando.UI.Monitoring;
using Sando.UI.View;
using Sando.Indexer.IndexState;

// Code changed by JZ: solution monitor integration
using System.Xml;
using System.Xml.Linq;

using ABB.SrcML.Utilities;
using ABB.SrcML;

// TODO: clarify where SolutionMonitorFactory (now in Sando), SolutionKey (now in Sando), ISolution (now in SrcML.NET) should be.
//using ABB.SrcML.VisualStudio.SolutionMonitor;
// End of code changes

namespace Sando.UI
{
    /// <summary> 
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // This attribute registers a tool window exposed by this package.
    [ProvideToolWindow(typeof(SearchToolWindow), Transient = false, MultiInstances = false, Style = VsDockStyle.Tabbed)]
    
    [Guid(GuidList.guidUIPkgString)]
	// This attribute starts up our extension early so that it can listen to solution events    
	[ProvideAutoLoad("ADFC4E64-0397-11D1-9F4E-00A0C911004F")]
    // Start when solution exists
    //[ProvideAutoLoad("f1536ef8-92ec-443c-9ed7-fdadf150da82")]    
	[ProvideOptionPage(typeof(SandoDialogPage), "Sando", "General", 1000, 1001, true)]
	[ProvideProfile(typeof(SandoDialogPage), "Sando", "General", 1002, 1003, true)]
    public sealed class UIPackage : Package, IToolWindowFinder
    {
        // Code changed by JZ: solution monitor integration
        /// <summary>
        /// Use SrcML.NET's SolutionMonitor, instead of Sando's SolutionMonitor
        /// </summary>
        private ABB.SrcML.VisualStudio.SolutionMonitor.SolutionMonitor _currentMonitor;
        private ABB.SrcML.SrcMLArchive _srcMLArchive;
        ////private SolutionMonitor _currentMonitor;
        // End of code changes

    	private SolutionEvents _solutionEvents;
		private ILog logger;
        private string pluginDirectory;        
        private ExtensionPointsConfiguration extensionPointsConfiguration;
        private DTEEvents _dteEvents;
        private ViewManager _viewManager;
		private SolutionReloadEventListener listener;
		private IVsUIShellDocumentWindowMgr winmgr;
        private WindowEvents _windowEvents;

        private static UIPackage MyPackage
		{
			get;
			set;
		}

    	/// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public UIPackage()
        {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));    		
        }


        public static SandoOptions GetSandoOptions(UIPackage package)
        {
            return GetSandoOptions(null, 20,package);
        }

        public static SandoOptions GetSandoOptions(string defaultPluginDirectory, int defaultToReturn, UIPackage package)
		{
			SandoDialogPage sandoDialogPage = package.GetDialogPage(typeof(SandoDialogPage)) as SandoDialogPage;
            if(sandoDialogPage.ExtensionPointsPluginDirectoryPath==null&& defaultPluginDirectory!=null)
            {
                sandoDialogPage.ExtensionPointsPluginDirectoryPath = defaultPluginDirectory;
            }
            if(sandoDialogPage.NumberOfSearchResultsReturned==null)
            {
                sandoDialogPage.NumberOfSearchResultsReturned = defaultToReturn+"";
            }
            if (Directory.Exists(sandoDialogPage.ExtensionPointsPluginDirectoryPath) == false && defaultPluginDirectory != null)
		    {
		        sandoDialogPage.ExtensionPointsPluginDirectoryPath = defaultPluginDirectory;
		    }
			SandoOptions sandoOptions = new SandoOptions(sandoDialogPage.ExtensionPointsPluginDirectoryPath, sandoDialogPage.NumberOfSearchResultsReturned);
			return sandoOptions;
		}

        /////////////////////////////////////////////////////////////////////////////
        // Overriden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {

            try
            {
                Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}",
                                              this.ToString()));
                FileLogger.DefaultLogger.Info("Sando initialization started.");
                base.Initialize();
                SetUpLogger();
                _viewManager = new ViewManager(this);
                AddCommand();                
                RegisterExtensionPoints();
                SetUpLifeCycleEvents();
                MyPackage = this;                
            }catch(Exception e)
            {
                FileLogger.DefaultLogger.Error(ExceptionFormatter.CreateMessage(e));
            }
        }

        private void SetUpLifeCycleEvents()
        {
            var dte = GetDte();
            _dteEvents = dte.Events.DTEEvents;
            _dteEvents.OnBeginShutdown += DteEventsOnOnBeginShutdown;
            _dteEvents.OnStartupComplete += StartupCompleted;
            _windowEvents = dte.Events.WindowEvents;
            _windowEvents.WindowActivated += SandoWindowActivated;
            
        }

        private void SandoWindowActivated(Window GotFocus, Window LostFocus)
        {
            try
            {
                if (GotFocus.ObjectKind.Equals("{AC71D0B7-7613-4EDD-95CC-9BE31C0A993A}"))
                {
                    var window = this.FindToolWindow(typeof(SearchToolWindow), 0, true);
                    if ((null == window) || (null == window.Frame))
                    {
                        throw new NotSupportedException(Resources.CanNotCreateWindow);
                    }
                    var stw = window as SearchToolWindow;
                    if (stw != null)
                    {
                        stw.GetSearchViewControl().FocusOnText();
                    }
                }
            }
            catch (Exception e)
            {
                FileLogger.DefaultLogger.Error(e);
            }
            
        }

        private void AddCommand()
        {
            // Add our command handlers for menu (commands must exist in the .vsct file)
            var mcs = GetService(typeof (IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                // Create the command for the tool window
                var toolwndCommandID = new CommandID(GuidList.guidUICmdSet, (int) PkgCmdIDList.sandoSearch);
                var menuToolWin = new MenuCommand(_viewManager.ShowToolWindow, toolwndCommandID);
                mcs.AddCommand(menuToolWin);
            }
        }
        
        private void StartupCompleted()
        {
        	if (_viewManager.ShouldShow())
        	{
        		_viewManager.ShowSando();
        		_viewManager.ShowToolbar();
        	}

        	if (GetDte().Version.StartsWith("10"))
        	{
				//only need to do this in VS2010, and it breaks things in VS2012
        		Solution openSolution = GetOpenSolution();
        		if (openSolution != null && !String.IsNullOrWhiteSpace(openSolution.FullName) && _currentMonitor == null)
        		{
        			SolutionHasBeenOpened();
        		}
        	}

        	RegisterSolutionEvents();
        }  

        private void RegisterSolutionEvents()
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte != null)
            {
                _solutionEvents = dte.Events.SolutionEvents;                
                _solutionEvents.Opened += SolutionHasBeenOpened;
                _solutionEvents.BeforeClosing += SolutionAboutToClose;
            }

			listener = new SolutionReloadEventListener();
			winmgr = Package.GetGlobalService(typeof(IVsUIShellDocumentWindowMgr)) as IVsUIShellDocumentWindowMgr;
			listener.OnQueryUnloadProject += () =>
			{
				SolutionAboutToClose();
				SolutionHasBeenOpened();
			};
        }         

        private void DteEventsOnOnBeginShutdown()
        {
            if (extensionPointsConfiguration != null)
            {                                
                ExtensionPointsConfigurationFileReader.WriteConfiguration(GetExtensionPointsConfigurationFilePath(GetExtensionPointsConfigurationDirectory()), extensionPointsConfiguration);
                //After writing the extension points configuration file, the index state file on disk is out of date; so it needs to be rewritten
                IndexStateManager.SaveCurrentIndexState(GetExtensionPointsConfigurationDirectory());
            }
            //TODO - kill file processing threads
        }

        private void SetUpLogger()
        {
            //IVsExtensionManager extensionManager = ServiceProvider.GlobalProvider.GetService(typeof(SVsExtensionManager)) as IVsExtensionManager;
            //var directoryProvider = new ExtensionDirectoryProvider(extensionManager);
            //pluginDirectory = directoryProvider.GetExtensionDirectory();
        	pluginDirectory = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
            var logFilePath = Path.Combine(pluginDirectory, "UIPackage.log");
            logger = FileLogger.CreateFileLogger("UIPackageLogger", logFilePath);
            FileLogger.DefaultLogger.Info("pluginDir: "+pluginDirectory);
        }

        private void RegisterExtensionPoints()
        {
            var extensionPointsRepository = ExtensionPointsRepository.Instance;

            extensionPointsRepository.RegisterParserImplementation(new List<string>() { ".cs" }, new SrcMLCSharpParser(GetSrcMLDirectory()));
            extensionPointsRepository.RegisterParserImplementation(new List<string>() { ".h", ".cpp", ".cxx", ".c" },
                                                                   new SrcMLCppParser(GetSrcMLDirectory()));
            extensionPointsRepository.RegisterParserImplementation(new List<string>() { ".xaml", ".htm", ".html", ".xml", ".resx", ".aspx"},
                                                                   new XMLFileParser());
			extensionPointsRepository.RegisterParserImplementation(new List<string>() { ".txt" },
																   new TextFileParser());

            extensionPointsRepository.RegisterWordSplitterImplementation(new WordSplitter()); 	
            extensionPointsRepository.RegisterResultsReordererImplementation(new SortByScoreResultsReorderer());
 	        extensionPointsRepository.RegisterQueryWeightsSupplierImplementation(new QueryWeightsSupplier());
 	        extensionPointsRepository.RegisterQueryRewriterImplementation(new DefaultQueryRewriter());


            
            var extensionPointsConfigurationDirectoryPath = GetExtensionPointsConfigurationDirectory();
            string extensionPointsConfigurationFilePath = GetExtensionPointsConfigurationFilePath(extensionPointsConfigurationDirectoryPath);

            extensionPointsConfiguration = ExtensionPointsConfigurationFileReader.ReadAndValidate(extensionPointsConfigurationFilePath, logger);

            if(extensionPointsConfiguration != null)
			{
                extensionPointsConfiguration.PluginDirectoryPath = extensionPointsConfigurationDirectoryPath;
				ExtensionPointsConfigurationAnalyzer.FindAndRegisterValidExtensionPoints(extensionPointsConfiguration, logger);
			}

            var csParser = extensionPointsRepository.GetParserImplementation(".cs") as SrcMLCSharpParser;
            if(csParser!=null)
            {
                csParser.SetSrcMLPath(GetSrcMLDirectory());
            }
            var cppParser = extensionPointsRepository.GetParserImplementation(".cpp") as SrcMLCppParser;
            if (cppParser != null)
            {
                cppParser.SetSrcMLPath(GetSrcMLDirectory());
            }

        }

        private static string GetExtensionPointsConfigurationFilePath(string extensionPointsConfigurationDirectoryPath)
        {
            return Path.Combine(extensionPointsConfigurationDirectoryPath,
                                "ExtensionPointsConfiguration.xml");
        }

        private string GetExtensionPointsConfigurationDirectory()
        {
            string extensionPointsConfigurationDirectory =
                GetSandoOptions(pluginDirectory, 20, this).ExtensionPointsPluginDirectoryPath;
            if (extensionPointsConfigurationDirectory == null)
            {
                extensionPointsConfigurationDirectory = pluginDirectory;
            }
            return extensionPointsConfigurationDirectory;
        }

        private string GetSrcMLDirectory()
        {
            return pluginDirectory + "\\LIBS";
        }

        private void SolutionAboutToClose()
		{
		
			if(_currentMonitor != null)
			{
                try
                {
                    // Code changed by JZ: solution monitor integration
                    // Don't know if the update listener is still useful. 
                    // The following statement would cause an exception in ViewManager.cs (Line 42).
                    //SolutionMonitorFactory.RemoveUpdateListener(SearchViewControl.GetInstance());
                    ////_currentMonitor.RemoveUpdateListener(SearchViewControl.GetInstance());
                    // End of code changes
                }
                finally
                {
                    try
                    {
                        // Code changed by JZ: solution monitor integration
                        // Use SrcML.NET's StopMonitoring()
                        if (_srcMLArchive != null)
                        {
                            // SolutionMonitor.StopWatching() is called in SrcMLArchive.StopWatching()
                            _srcMLArchive.StopWatching();
                            _srcMLArchive = null;
                        }
                        ////_currentMonitor.Dispose();
                        ////_currentMonitor = null;
                        // End of code changes
                    }
                    catch (Exception e)
                    {
                        FileLogger.DefaultLogger.Error(e);
                    }
                }
			}
		}

        public Solution GetOpenSolution()
        {
        	var dte = GetDte();
            if (dte != null)
            {
                var openSolution = dte.Solution;
                return openSolution;
            }
            else
            {
                return null;
            }
        }

		private void SolutionHasBeenOpened()
		{
            BackgroundWorker bw = new BackgroundWorker();
            bw.WorkerReportsProgress = false;
            bw.WorkerSupportsCancellation = false;
            bw.DoWork += new DoWorkEventHandler(RespondToSolutionOpened);
		    bw.RunWorkerAsync();
		}

        // Code changed by JZ: solution monitor integration
        /// <summary>
        /// Respond to solution opening.
        /// Still use Sando's SolutionMonitorFactory because Sando's SolutionMonitorFactory has too much indexer code which is specific with Sando.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ee"></param>
        private void RespondToSolutionOpened(object sender, DoWorkEventArgs ee)
        {
            try
            {
                SolutionMonitorFactory.LuceneDirectory = pluginDirectory;
                string extensionPointsConfigurationDirectory =
                    GetSandoOptions(pluginDirectory, 20, this).ExtensionPointsPluginDirectoryPath;
                if (extensionPointsConfigurationDirectory == null || Directory.Exists(extensionPointsConfigurationDirectory) == false)
                {
                    extensionPointsConfigurationDirectory = pluginDirectory;
                }

                FileLogger.DefaultLogger.Info("extensionPointsDirectory: " + extensionPointsConfigurationDirectory);
                bool isIndexRecreationRequired =
                    IndexStateManager.IsIndexRecreationRequired(extensionPointsConfigurationDirectory);

                // Create a new instance of SrcML.NET's solution monitor
                _currentMonitor = SolutionMonitorFactory.CreateMonitor(isIndexRecreationRequired);
                //Create the default IndexFilterManager
                //This must happen after calling CreateMonitor, because that sets the Solution Key, but before subscribing to file events
                ExtensionPointsRepository.Instance.RegisterIndexFilterManagerImplementation(new IndexFilterManager(GetCurrentSolutionKey().GetIndexPath()));
                // Subscribe events from SrcML.NET's solution monitor
                _currentMonitor.FileEventRaised += RespondToSolutionMonitorEvent;

                // Create a new instance of SrcML.NET's SrcMLArchive
                string src2srcmlDir = Path.Combine(pluginDirectory, "LIBS");
                var generator = new ABB.SrcML.SrcMLGenerator(src2srcmlDir);
                generator.RegisterExecutable(Path.Combine(src2srcmlDir, "srcML-Win-cSharp"), new[] {ABB.SrcML.Language.CSharp});
                _srcMLArchive = new ABB.SrcML.SrcMLArchive(_currentMonitor, SolutionMonitorFactory.GetSrcMlArchiveFolder(GetOpenSolution()), generator);
                // Subscribe events from SrcML.NET's solution monitor
                _srcMLArchive.SourceFileChanged += RespondToSourceFileChangedEvent;
                _srcMLArchive.StartupCompleted += RespondToStartupCompletedEvent;
                _srcMLArchive.MonitoringStopped += RespondToMonitoringStoppedEvent;

                ConcurrentDictionary<string, string> results = _srcMLArchive.initialization();
                InitIndex(results);

                // SolutionMonitor.StartWatching() is called in SrcMLArchive.StartWatching()
                _srcMLArchive.StartWatching();

                // Don't know if AddUpdateListener() is still useful.
                SolutionMonitorFactory.AddUpdateListener(SearchViewControl.GetInstance());
                ////_currentMonitor.AddUpdateListener(SearchViewControl.GetInstance());
            }
            catch (Exception e)
            {
                FileLogger.DefaultLogger.Error(ExceptionFormatter.CreateMessage(e, "Problem responding to Solution Opened."));
            }    
        }

        private void InitIndex(ConcurrentDictionary<string, string> fileList){

            Stopwatch sw = new Stopwatch();
            sw.Start();

            //foreach(var element in fileList) {
            //    string sourceFilePath = element.Key;
            //    string xmlfileName = element.Value;
            //    SrcMLFile temp = new SrcMLFile(xmlfileName);
            //    XElement xelement = temp.FileUnits.FirstOrDefault();

            //    SolutionMonitorFactory.DeleteIndex(sourceFilePath); //"just to be safe!" from IndexUpdateManager.UpdateFile()
            //    SolutionMonitorFactory.UpdateIndex(sourceFilePath, xelement);
            //}

            //SolutionMonitorFactory.CommitIndexChanges();

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 2;

            Parallel.ForEach(fileList, options, currentPair => {
                string sourceFilePath = currentPair.Key;
                string xmlfileName = currentPair.Value;
                SrcMLFile temp = new SrcMLFile(xmlfileName);
                XElement xelement = temp.FileUnits.FirstOrDefault();

                SolutionMonitorFactory.DeleteIndex(sourceFilePath); //"just to be safe!" from IndexUpdateManager.UpdateFile()
                SolutionMonitorFactory.UpdateIndex(sourceFilePath, xelement);

            });

            System.Threading.Tasks.Task.WaitAll();
            SolutionMonitorFactory.CommitIndexChanges();

            sw.Stop();
            Trace.WriteLine("Updating Indexes takes " + sw.Elapsed);
        }
        /// <summary>
        /// Respond to the SourceFileChanged event from SrcML.NET's Solution Monitor.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void RespondToSolutionMonitorEvent(object sender, ABB.SrcML.FileEventRaisedArgs eventArgs)
        {
            writeLog("Sando: RespondToSolutionMonitorEvent(), File = " + eventArgs.SourceFilePath + ", EventType = " + eventArgs.EventType);
            //// Current design decision: 
            //// Ignore files that can be parsed by SrcML.NET. Those files are processed by RespondToSourceFileChangedEvent().

            //Shared by serial and parallel version
            if(!_srcMLArchive.IsValidFileExtension(eventArgs.SourceFilePath)) {
                HandleSrcMLDOTNETEvents(eventArgs);
             }
        }

        /// <summary>
        /// Respond to the SourceFileChanged event from SrcMLArchive.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void RespondToSourceFileChangedEvent(object sender, ABB.SrcML.FileEventRaisedArgs eventArgs)
        {
            writeLog( "Sando: RespondToSourceFileChangedEvent(), File = " + eventArgs.SourceFilePath + ", EventType = " + eventArgs.EventType);
            HandleSrcMLDOTNETEvents(eventArgs);

        }

        /// <summary>
        /// Zhao: Parallel
        /// Handle SrcML.NET events, either from SrcMLArchive or from SolutionMonitor.
        /// TODO: UpdateIndex(), DeleteIndex(), and CommitIndexChanges() might be refactored to another class.
        /// </summary>
        /// <param name="eventArgs"></param>
        private void HandleSrcMLDOTNETEvents(ABB.SrcML.FileEventRaisedArgs eventArgs)
        {
            // Ignore files that can not be indexed by Sando.
            //Zhao tracing the files
            Stopwatch sw = new Stopwatch();
            sw.Start();

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 6;

            System.Threading.Tasks.Task.Factory.StartNew(() => {
            //Parallel.Invoke(options, ()=> {
                string fileExtension = Path.GetExtension(eventArgs.SourceFilePath);
                if(fileExtension != null && !fileExtension.Equals(String.Empty)) {
                    if(ExtensionPointsRepository.Instance.GetParserImplementation(fileExtension) != null) {
                        string sourceFilePath = eventArgs.SourceFilePath;
                        string oldSourceFilePath = eventArgs.OldSourceFilePath;
                        XElement xelement = eventArgs.SrcMLXElement;

                        switch(eventArgs.EventType) {
                            case ABB.SrcML.FileEventType.FileAdded:
                                SolutionMonitorFactory.DeleteIndex(sourceFilePath); //"just to be safe!" from IndexUpdateManager.UpdateFile()
                                SolutionMonitorFactory.UpdateIndex(sourceFilePath, xelement);
                                break;
                            case ABB.SrcML.FileEventType.FileChanged:
                                SolutionMonitorFactory.DeleteIndex(sourceFilePath);
                                SolutionMonitorFactory.UpdateIndex(sourceFilePath, xelement);
                                break;
                            case ABB.SrcML.FileEventType.FileDeleted:
                                SolutionMonitorFactory.DeleteIndex(sourceFilePath);
                                break;
                            case ABB.SrcML.FileEventType.FileRenamed: // FileRenamed is actually never raised.
                                SolutionMonitorFactory.DeleteIndex(oldSourceFilePath);
                                SolutionMonitorFactory.UpdateIndex(sourceFilePath, xelement);
                                break;
                        }
                        SolutionMonitorFactory.CommitIndexChanges();
                    }
                }
            });

            sw.Stop();
            Trace.WriteLineIf((eventArgs.EventType == ABB.SrcML.FileEventType.FileChanged) || (eventArgs.EventType == ABB.SrcML.FileEventType.FileRenamed)
                || (eventArgs.EventType == ABB.SrcML.FileEventType.FileAdded), eventArgs.SourceFilePath.ToString().Substring(eventArgs.SourceFilePath.ToString().LastIndexOf("\\") + 1) + " " + sw.ElapsedMilliseconds);
            //Trace.WriteLine(eventArgs.SourceFilePath.ToString().Substring(eventArgs.SourceFilePath.ToString().LastIndexOf("\\") + 1) + " " + sw.ElapsedMilliseconds);

        }

        /// <summary>
        /// Respond to the StartupCompleted event from SrcMLArchive.
        /// TODO: StartupCompleted() might be refactored to another class.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void RespondToStartupCompletedEvent(object sender, EventArgs eventArgs)
        {
            SolutionMonitorFactory.StartupCompleted();
        }

        /// <summary>
        /// Respond to the MonitorStopped event from SrcMLArchive.
        /// TODO: MonitoringStopped() might be refactored to another class.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void RespondToMonitoringStoppedEvent(object sender, EventArgs eventArgs)
        {
            SolutionMonitorFactory.MonitoringStopped();
        }
        
        /* //// Original implementation
        private void RespondToSolutionOpened(object sender, DoWorkEventArgs ee)
        {
            try
            {
                SolutionMonitorFactory.LuceneDirectory = pluginDirectory;
                string extensionPointsConfigurationDirectory =
                    GetSandoOptions(pluginDirectory, 20, this).ExtensionPointsPluginDirectoryPath;
                if (extensionPointsConfigurationDirectory == null || Directory.Exists(extensionPointsConfigurationDirectory) == false)
                {
                    extensionPointsConfigurationDirectory = pluginDirectory;
                }

                FileLogger.DefaultLogger.Info("extensionPointsDirectory: " + extensionPointsConfigurationDirectory);
                bool isIndexRecreationRequired =
                    IndexStateManager.IsIndexRecreationRequired(extensionPointsConfigurationDirectory);
                _currentMonitor = SolutionMonitorFactory.CreateMonitor(isIndexRecreationRequired, GetOpenSolution());
                //SwumManager needs to be initialized after the current solution key is set, but before monitoring/indexing begins
                Recommender.SwumManager.Instance.Initialize(PluginDirectory(), this.GetCurrentSolutionKey().GetIndexPath(), !isIndexRecreationRequired);
                _currentMonitor.StartMonitoring();
                _currentMonitor.AddUpdateListener(SearchViewControl.GetInstance());
            }
            catch (Exception e)
            {
                FileLogger.DefaultLogger.Error(ExceptionFormatter.CreateMessage(e, "Problem responding to Solution Opened."));
            }
        }
        */
        // End of code changes

        public void AddNewParser(string qualifiedClassName, string dllName, List<string> supportedFileExtensions)
        {            
            extensionPointsConfiguration.ParsersConfiguration.Add(new ParserExtensionPointsConfiguration()
            {
                FullClassName = qualifiedClassName,
                LibraryFileRelativePath = dllName,
                SupportedFileExtensions = supportedFileExtensions,
                ProgramElementsConfiguration = new List<BaseExtensionPointConfiguration>()
							{
								new BaseExtensionPointConfiguration()
								{
									FullClassName = qualifiedClassName,
									LibraryFileRelativePath = dllName
								}
							}
            });
        }


        public static UIPackage GetInstance()
		{
			return MyPackage;
		}



    	#endregion

        // Code changed by JZ: solution monitor integration
        public string GetCurrentDirectory()
        {
            return SolutionMonitorFactory.GetCurrentDirectory();
        }

        public SolutionKey GetCurrentSolutionKey()
        {
            return SolutionMonitorFactory.GetSolutionKey();
        }

        public bool IsPerformingInitialIndexing()
        {
            return SolutionMonitorFactory.PerformingInitialIndexing();
        }

        /* //// original implementation
        public string GetCurrentDirectory()
        {
            if (_currentMonitor != null)
                return _currentMonitor.GetCurrentDirectory();
            else
                return null;
        }

        public SolutionKey GetCurrentSolutionKey()
        {
            if (_currentMonitor != null)
                return _currentMonitor.GetSolutionKey();
            else
                return null;
        }

        public bool IsPerformingInitialIndexing()
        {
            if(_currentMonitor!=null)
            {
                return _currentMonitor.PerformingInitialIndexing();
            }else
            {
                return false;
            }
        }
        */
        // End of code changes

    	#region Implementation of IIndexUpdateListener

    	public void NotifyAboutIndexUpdate()
    	{
    		throw new NotImplementedException();
    	}

    	#endregion

        public string PluginDirectory()
        {
            return pluginDirectory;
        }

        public DTE2 GetDte()
        {
            return GetService(typeof(DTE)) as DTE2;
        }

        public void EnsureViewExists()
        {
            _viewManager.EnsureViewExists();
        }


        // Code changed by JZ: solution monitor integration
        /// <summary>
        /// For debugging.
        /// </summary>
        /// <param name="logFile"></param>
        /// <param name="str"></param>
        private static void writeLog(string str)
        {
            FileLogger.DefaultLogger.Info(str);
        }
        // End of code changes

    }
}
