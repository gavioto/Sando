﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using ABB.SrcML;
using ABB.SrcML.VisualStudio.SrcMLService;
using Configuration.OptionsPages;
using EnvDTE;
using EnvDTE80;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Snowball;
using Sando.Core.QueryRefomers;
using Sando.DependencyInjection;
using Sando.Indexer;
using Sando.Indexer.IndexFiltering;
using Sando.UI.Options;
using Microsoft.VisualStudio.Shell;
using Sando.Core.Extensions;
using Sando.Core.Extensions.Configuration;
using Sando.Core.Tools;
using Sando.Indexer.Searching;
using Sando.Parser;
using Sando.SearchEngine;
using Sando.UI.Monitoring;
using Sando.UI.View;
using Sando.Indexer.IndexState;
using Sando.Recommender;
using System.Reflection;
using System.Threading.Tasks;
using Sando.Indexer.Documents;
using Lucene.Net.Analysis.Standard;
using Sando.Core.Logging;
using Sando.Core.Logging.Events;
using Sando.Core.Logging.Persistence;
using Sando.UI.Service;
using System.Diagnostics;
using Sando.ExtensionContracts.ServiceContracts;
using Microsoft.VisualStudio;
using System.Linq;
using System.Windows.Threading;
using System.Collections.ObjectModel;



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

    /// <summary>
    /// Add the ProvideServiceAttribute to the VSPackage that provides the global service.
    /// ProvideServiceAttribute registers SSandoGlobalService with Visual Studio. 
    /// Only the global service must be registered.
    /// </summary>
    [ProvideService(typeof(SSandoGlobalService))]

    public sealed class UIPackage : Package, IToolWindowFinder
    {
        // JZ: SrcMLService Integration
        //private ABB.SrcML.VisualStudio.SolutionMonitor.SolutionMonitor _currentMonitor;
        private ISrcMLArchive _srcMLArchive;
        private ISrcMLGlobalService srcMLService;
        // End of code changes

    	private SolutionEvents _solutionEvents;
		private ExtensionPointsConfiguration _extensionPointsConfiguration;
        private DTEEvents _dteEvents;
        private ViewManager _viewManager;		
        private WindowEvents _windowEvents;
        private bool SetupHandlers = false;
        private bool WindowActivated = false;

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public UIPackage()
        {            
            PathManager.Create(Assembly.GetAssembly(typeof(UIPackage)).Location);
            SandoLogManager.StartDefaultLogging(PathManager.Instance.GetExtensionRoot());

            // Add callback methods to the service container to create the services.
            // Here we update the list of the provided services with the ones specific for this package.
            // Notice that we set to true the boolean flag about the service promotion for the global:
            // to promote the service is actually to proffer it globally using the SProfferService service.
            // For performance reasons we don’t want to instantiate the services now, but only when and 
            // if some client asks for them, so we here define only the type of the service and a function
            // that will be called the first time the package will receive a request for the service. 
            // This callback function is the one responsible for creating the instance of the service 
            // object.
            IServiceContainer serviceContainer = this as IServiceContainer;
            ServiceCreatorCallback callback = new ServiceCreatorCallback(CreateService);
            serviceContainer.AddService(typeof(SSandoGlobalService), callback, true);
            serviceContainer.AddService(typeof(SSandoLocalService), callback);
        }

        public SandoDialogPage GetSandoDialogPage()
        {
            return (GetDialogPage(typeof(SandoDialogPage)) as SandoDialogPage);
        }

        public void OpenSandoOptions()
        {
            try
            {
                string sandoDialogPageGuid = "B0002DC2-56EE-4931-93F7-70D6E9863940";
                var command = new CommandID(
                    VSConstants.GUID_VSStandardCommandSet97,
                    VSConstants.cmdidToolsOptions);
                var mcs = GetService(typeof(IMenuCommandService))
                    as MenuCommandService;
                mcs.GlobalInvoke(command, sandoDialogPageGuid);
            }
            catch (Exception e)
            {
                LogEvents.UIGenericError(this, e);
            }
        }

        /// <summary>
        /// Implement the callback method.
        /// This is the function that will create a new instance of the services the first time a client
        /// will ask for a specific service type. 
        /// It is called by the base class's implementation of IServiceProvider.
        /// </summary>
        /// <param name="container">The IServiceContainer that needs a new instance of the service.
        ///                         This must be this package.</param>
        /// <param name="serviceType">The type of service to create.</param>
        /// <returns>The instance of the service.</returns>
        private object CreateService(IServiceContainer container, Type serviceType)
        {
            Trace.WriteLine("    SandoServicePackage.CreateService()");
            //todo: write it to log file

            // Check if the IServiceContainer is this package.
            if (container != this)
            {
                Trace.WriteLine("ServicesPackage.CreateService called from an unexpected service container.");
                return null;
            }

            // Find the type of the requested service and create it.
            if (typeof(SSandoGlobalService) == serviceType)
            {
                // Build the global service using this package as its service provider.
                return new SandoGlobalService(this);
            }
            if (typeof(SSandoLocalService) == serviceType)
            {
                // Build the local service using this package as its service provider.
                return new SandoLocalService(this);
            }

            // If we are here the service type is unknown, so write a message on the debug output
            // and return null.
            //Trace.WriteLine("ServicesPackage.CreateService called for an unknown service type.");
            return null;
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
                base.Initialize();
                LogEvents.UISandoBeginInitialization(this);                

                SetupDependencyInjectionObjects();

                _viewManager = ServiceLocator.Resolve<ViewManager>();
                AddCommand();                
                SetUpLifeCycleEvents();
            }
            catch(Exception e)
            {
                LogEvents.UISandoInitializationError(this, e);
            }            
        }


        private void SetUpLifeCycleEvents()
        {
            var dte = ServiceLocator.Resolve<DTE2>();
            _dteEvents = dte.Events.DTEEvents;
            _dteEvents.OnBeginShutdown += DteEventsOnOnBeginShutdown;
            _dteEvents.OnStartupComplete += StartupCompleted;
            _windowEvents = dte.Events.WindowEvents;
            _windowEvents.WindowActivated += SandoWindowActivated;
            
        }

        private void SandoWindowActivated(Window gotFocus, Window lostFocus)
        {
            try
            {
                if (gotFocus.ObjectKind.Equals("{AC71D0B7-7613-4EDD-95CC-9BE31C0A993A}"))
                {
                    var window = FindToolWindow(typeof(SearchToolWindow), 0, true);
                    if ((null == window) || (null == window.Frame))
                    {
                        throw new NotSupportedException(Resources.CanNotCreateWindow);
                    }
                    var stw = window as SearchToolWindow;
                    if (stw != null)
                    {
                        stw.GetSearchViewControl().FocusOnText();
                        WindowActivated = true;
                    }
                }
            }
            catch (Exception e)
            {
                LogEvents.UISandoWindowActivationError(this, e);
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
        
        public void StartupCompleted()
        {
            try
            {
                if (_viewManager.ShouldShow())
                {
                    _viewManager.ShowSando();
                    try
                    {
                        //will fail during testing in VS IDE host
                        _viewManager.ShowToolbar();
                    }
                    catch (Exception e)
                    {
                        //ignore
                    }
                }

                if (ServiceLocator.Resolve<DTE2>().Version.StartsWith("10"))
                {
                    //only need to do this in VS2010, and it breaks things in VS2012
                    Solution openSolution = ServiceLocator.Resolve<DTE2>().Solution;

                    // JZ: SrcMLService Integration
                    ////if(openSolution != null && !String.IsNullOrWhiteSpace(openSolution.FullName) && _currentMonitor == null)
                    if (openSolution != null && !String.IsNullOrWhiteSpace(openSolution.FullName))
                    // End of code changes
                    {
                        SolutionHasBeenOpened();
                    }
                }

                RegisterSolutionEvents();
            }
            catch (Exception e)
            {
                LogEvents.UIGenericError(this, e);
            }
        }  

        public void RegisterSolutionEvents()
        {
            var dte = ServiceLocator.Resolve<DTE2>();
            if (dte != null)
            {
                _solutionEvents = dte.Events.SolutionEvents;                
                _solutionEvents.Opened += SolutionHasBeenOpened;
                _solutionEvents.BeforeClosing += SolutionAboutToClose;
            }

        }

         

        private void DteEventsOnOnBeginShutdown()
        {
            if (_extensionPointsConfiguration != null)
            {                                
                ExtensionPointsConfigurationFileReader.WriteConfiguration(_extensionPointsConfiguration);
                //After writing the extension points configuration file, the index state file on disk is out of date; so it needs to be rewritten
                IndexStateManager.SaveCurrentIndexState();
            }
            //TODO - kill file processing threads
        }

        private void RegisterExtensionPoints()
        {
            var extensionPointsRepository = ExtensionPointsRepository.Instance;

            // JZ: SrcMLService Integration
            extensionPointsRepository.RegisterParserImplementation(new List<string> { ".cs" }, new SrcMLCSharpParser());
            extensionPointsRepository.RegisterParserImplementation(new List<string> { ".h", ".cpp", ".cxx", ".c" }, new SrcMLCppParser(srcMLService));
            ////extensionPointsRepository.RegisterParserImplementation(new List<string> { ".cs" }, new SrcMLCSharpParser(_srcMLArchive));
            ////extensionPointsRepository.RegisterParserImplementation(new List<string> { ".h", ".cpp", ".cxx", ".c" }, new SrcMLCppParser(_srcMLArchive));
            // JZ: End of code changes
            extensionPointsRepository.RegisterParserImplementation(new List<string> { ".xaml" }, new XAMLFileParser());
			//extensionPointsRepository.RegisterParserImplementation(new List<string> { ".txt" },
																  // new TextFileParser());

            extensionPointsRepository.RegisterWordSplitterImplementation(new WordSplitter()); 	
            extensionPointsRepository.RegisterResultsReordererImplementation(new SortByScoreResultsReorderer());
 	        extensionPointsRepository.RegisterQueryWeightsSupplierImplementation(new QueryWeightsSupplier());
 	        extensionPointsRepository.RegisterQueryRewriterImplementation(new DefaultQueryRewriter());
            extensionPointsRepository.RegisterIndexFilterManagerImplementation(new IndexFilterManager());


            var sandoOptions = ServiceLocator.Resolve<ISandoOptionsProvider>().GetSandoOptions();

            var loggerPath = Path.Combine(sandoOptions.ExtensionPointsPluginDirectoryPath, "ExtensionPointsLogger.log");
            var logger = FileLogger.CreateFileLogger("ExtensionPointsLogger", loggerPath);
            _extensionPointsConfiguration = ExtensionPointsConfigurationFileReader.ReadAndValidate(logger);

            if(_extensionPointsConfiguration != null)
			{
                _extensionPointsConfiguration.PluginDirectoryPath = sandoOptions.ExtensionPointsPluginDirectoryPath;
				ExtensionPointsConfigurationAnalyzer.FindAndRegisterValidExtensionPoints(_extensionPointsConfiguration, logger);
			}

            var csParser = extensionPointsRepository.GetParserImplementation(".cs") as SrcMLCSharpParser;
            // JZ: SrcMLService Integration
            ////if(csParser != null) {
            ////    csParser.Archive = _srcMLArchive;
            ////}
            // End of code changes
            var cppParser = extensionPointsRepository.GetParserImplementation(".cpp") as SrcMLCppParser;
            // JZ: SrcMLService Integration
            ////if(cppParser != null) {
            ////    cppParser.Archive = _srcMLArchive;
            ////}
            // End of code changes

        }

        private void SolutionAboutToClose()
		{
			try
            {
                // JZ: SrcMLService Integration
                //srcMLService.StopMonitoring();
                // TODO: DocumentIndexer.CommitChanges(); DocumentIndexer.Dispose(false);
                // End of code changes

                
                
                if(_srcMLArchive != null)
                {
                    _srcMLArchive.Dispose();
                    _srcMLArchive = null;
                    ServiceLocator.Resolve<IndexFilterManager>().Dispose();
                    ServiceLocator.Resolve<DocumentIndexer>().Dispose();
                }
                // XiGe: dispose the dictionary.
                ServiceLocator.Resolve<DictionaryBasedSplitter>().Dispose();
                ServiceLocator.Resolve<SearchHistory>().Dispose();
                var control = ServiceLocator.Resolve<SearchViewControl>();
                control.Dispatcher.Invoke((Action)(() => UpdateDirectory(SearchViewControl.DefaultOpenSolutionMessage, control)));                                                                    
            }
            catch (Exception e)
            {
                LogEvents.UISolutionClosingError(this, e);
            }
		}

		private void SolutionHasBeenOpened()
		{            
            var bw = new BackgroundWorker {WorkerReportsProgress = false, WorkerSupportsCancellation = false};
		    bw.DoWork += RespondToSolutionOpened;
		    bw.RunWorkerAsync();
		}


        public void HandleIndexingStateChange(bool ready)
        {            
            CallShowProgressBar(!ready);
            if (ready)
                ServiceLocator.Resolve<InitialIndexingWatcher>().InitialIndexingCompleted();
            UpdateIndexingFilesList();
        }

        public void UpdateIndexingFilesListIfEmpty()
        {   
            if (!updatedForThisSolution)
            {
                UpdateIndexingFilesList();
            }             
        }

        public void UpdateIndexingFilesList()
        {
            if(srcMLService != null && srcMLService.MonitoredDirectories != null && WindowActivated)
            {
                var path = GetDisplayPathMonitoredFiles(srcMLService, this);
                try {
                    var control = ServiceLocator.Resolve<SearchViewControl>();
                    control.Dispatcher.Invoke((Action) (() => UpdateDirectory(path, control)));
                    if(srcMLService.MonitoredDirectories.Count > 0)
                        updatedForThisSolution = true;
                } catch(InvalidOperationException notInited) {
                    //OK, window not inited so can't update it
                }
            }
        }

        public static string GetDisplayPathMonitoredFiles(ISrcMLGlobalService service, object callingObject )
        {
            if (service.MonitoredDirectories.Count > 0)
            {
                var fullpath = service.MonitoredDirectories.First();
                var path = Path.GetFileName(fullpath);
                try
                {
                    var parent = Path.GetFileName(Path.GetDirectoryName(fullpath));
                    path += " in folder " + parent;
                }
                catch (Exception e)
                {
                    LogEvents.UIIndexUpdateError(callingObject, e);
                }
                return path;
            }
            else
            {
                return SearchViewControl.PleaseAddDirectoriesMessage;
            }
        }

        private void UpdateDirectory(string path, SearchViewControl control)
        {
            if (control != null)
            {
                control.OpenSolutionPaths = path;
            }
        }


        private void CallShowProgressBar(bool show)
        {
            if(WindowActivated)
            {
                try {
                    var control = ServiceLocator.Resolve<SearchViewControl>();
                    if(null != control) {
                        control.Dispatcher.BeginInvoke((Action) (() => control.ShowProgressBar(show)));
                    }
                } catch(TargetInvocationException e) {
                    FileLogger.DefaultLogger.Error(e);
                }
            }
        }

        /// <summary>
        /// Respond to solution opening.
        /// Still use Sando's SolutionMonitorFactory because Sando's SolutionMonitorFactory has too much indexer code which is specific with Sando.
        /// </summary>
        private void RespondToSolutionOpened(object sender, DoWorkEventArgs ee)
        {
            try
            {
                updatedForThisSolution = false;
                //TODO if solution is reopen - the guid should be read from file - future change
                var solutionId = Guid.NewGuid();
                var openSolution = ServiceLocator.Resolve<DTE2>().Solution;
                var solutionPath = openSolution.FileName;
                var key = new SolutionKey(solutionId, solutionPath);              
                ServiceLocator.RegisterInstance(key);

                var sandoOptions = ServiceLocator.Resolve<ISandoOptionsProvider>().GetSandoOptions();                
                bool isIndexRecreationRequired = IndexStateManager.IsIndexRecreationRequired();
                isIndexRecreationRequired = isIndexRecreationRequired || !PathManager.Instance.IndexPathExists(key);
                
                ServiceLocator.RegisterInstance(new IndexFilterManager());                

                ServiceLocator.RegisterInstance<Analyzer>(GetAnalyzer());
                SrcMLArchiveEventsHandlers srcMLArchiveEventsHandlers = ServiceLocator.Resolve<SrcMLArchiveEventsHandlers>();
                var currentIndexer = new DocumentIndexer(srcMLArchiveEventsHandlers, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
                ServiceLocator.RegisterInstance(currentIndexer);

                ServiceLocator.RegisterInstance(new IndexUpdateManager());

                if (isIndexRecreationRequired)
                {
                    currentIndexer.ClearIndex();
                }

                ServiceLocator.Resolve<InitialIndexingWatcher>().InitialIndexingStarted();

                // JZ: SrcMLService Integration
                // Get the SrcML Service.
                srcMLService = GetService(typeof(SSrcMLGlobalService)) as ISrcMLGlobalService;
                if(null == srcMLService) {
                    throw new Exception("Can not get the SrcML global service.");
                }
                else
                {
                    ServiceLocator.RegisterInstance(srcMLService);
                }                                               
                // Register all types of events from the SrcML Service.
                
                if (!SetupHandlers)
                {
                    SetupHandlers = true;
                    srcMLService.SourceFileChanged += srcMLArchiveEventsHandlers.SourceFileChanged;
                    srcMLService.MonitoringStopped += srcMLArchiveEventsHandlers.MonitoringStopped;
                }

                //This is done here because some extension points require data that isn't set until the solution is opened, e.g. the solution key or the srcml archive
                //However, registration must happen before file monitoring begins below.
                RegisterExtensionPoints();

                SwumManager.Instance.Initialize(PathManager.Instance.GetIndexPath(ServiceLocator.Resolve<SolutionKey>()), !isIndexRecreationRequired);
                //SwumManager.Instance.Archive = _srcMLArchive;

                ////XQ: for testing
                //ISandoGlobalService sandoService = GetService(typeof(SSandoGlobalService)) as ISandoGlobalService;
                //var res = sandoService.GetSearchResults("Monster");

                // xige
                var dictionary = new DictionaryBasedSplitter();
                dictionary.Initialize(PathManager.Instance.GetIndexPath(ServiceLocator.Resolve<SolutionKey>()));
                ServiceLocator.Resolve<IndexUpdateManager>().indexUpdated += 
                    dictionary.UpdateProgramElement;
                ServiceLocator.RegisterInstance(dictionary);

                var reformer = new QueryReformerManager(dictionary);
                reformer.Initialize(null);
                ServiceLocator.RegisterInstance(reformer);

                var history = new SearchHistory();
                history.Initialize(PathManager.Instance.GetIndexPath
                    (ServiceLocator.Resolve<SolutionKey>()));
                ServiceLocator.RegisterInstance(history);
                                

                // End of code changes

                if (sandoOptions.AllowDataCollectionLogging)
                {
                    SandoLogManager.StartDataCollectionLogging(PathManager.Instance.GetExtensionRoot());
                }
                else
                {
                    SandoLogManager.StopDataCollectionLogging();
                }
				LogEvents.SolutionOpened(this, Path.GetFileName(solutionPath));

                if (isIndexRecreationRequired)
                {
                    //just recreate the whole index
                    var indexingTask = System.Threading.Tasks.Task.Factory.StartNew(() =>
                        {
                            Collection<string> files = null;
                            while (files == null)
                            {
                                try
                                {
                                    files = srcMLService.GetSrcMLArchive().GetFiles();                                    
                                }
                                catch (NullReferenceException ne)
                                {
                                    System.Threading.Thread.Sleep(3000);
                                }
                            }                            
                            foreach (var file in srcMLService.GetSrcMLArchive().FileUnits)
                            {                                
                                var fileName = ABB.SrcML.SrcMLElement.GetFileNameForUnit(file);
                                srcMLArchiveEventsHandlers.SourceFileChanged(srcMLService, new FileEventRaisedArgs(FileEventType.FileAdded, fileName));
                            }
                            srcMLArchiveEventsHandlers.WaitForIndexing();
                        });
                }
                else
                {
                    //make sure you're not missing any files 
                    var indexingTask = System.Threading.Tasks.Task.Factory.StartNew(() =>
                    {
                        Collection<string> files = null;
                        while (files == null)
                        {
                            try
                            {
                                files = srcMLService.GetSrcMLArchive().GetFiles();
                            }
                            catch (NullReferenceException ne)
                            {
                                System.Threading.Thread.Sleep(3000);
                            }
                        }
                        foreach (var fileName in srcMLService.GetSrcMLArchive().GetFiles())
                        {                            
                            srcMLArchiveEventsHandlers.SourceFileChanged(srcMLService, new FileEventRaisedArgs(FileEventType.FileRenamed, fileName));
                        }
                        srcMLArchiveEventsHandlers.WaitForIndexing();
                    });                    
                }                
            }
            catch (Exception e)
            {
                LogEvents.UIRespondToSolutionOpeningError(this, e);
            }
            var updateListedFoldersTask = System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                bool done = false;
                while (!done)
                {
                    System.Threading.Thread.Sleep(2000);
                    if (srcMLService != null)
                    {
                        if (srcMLService.MonitoredDirectories != null)
                        {
                            UpdateIndexingFilesList();
                            done = true;
                        }
                    }
                }
            });
        }

        Action progressAction;
        private bool updatedForThisSolution = false;
 
        private Analyzer GetAnalyzer()
        {
            PerFieldAnalyzerWrapper analyzer = new PerFieldAnalyzerWrapper(new SnowballAnalyzer("English"));
            analyzer.AddAnalyzer(SandoField.Source.ToString(), new KeywordAnalyzer());
            analyzer.AddAnalyzer(SandoField.AccessLevel.ToString(), new KeywordAnalyzer());
            analyzer.AddAnalyzer(SandoField.ProgramElementType.ToString(), new KeywordAnalyzer());
            return analyzer;
        }  

        #endregion




        private void SetupDependencyInjectionObjects()
        {
            ServiceLocator.RegisterInstance(GetService(typeof (DTE)) as DTE2);
            ServiceLocator.RegisterInstance(this);
            ServiceLocator.RegisterInstance(new ViewManager(this));
            ServiceLocator.RegisterInstance<ISandoOptionsProvider>(new SandoOptionsProvider());
            ServiceLocator.RegisterInstance(new SrcMLArchiveEventsHandlers());
            ServiceLocator.RegisterInstance(new InitialIndexingWatcher());
            ServiceLocator.RegisterType<IIndexerSearcher, IndexerSearcher>();
        }
    }
}
