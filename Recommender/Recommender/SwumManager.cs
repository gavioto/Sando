﻿using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using ABB.Swum;
using ABB.Swum.Nodes;
using ABB.SrcML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;


namespace Sando.Recommender {
    /// <summary>
    /// Builds SWUM for the methods and method calls in a srcML file.
    /// </summary>
    public class SwumManager {
        private static SwumManager instance;
        
        private const string SrcmlBinDir = "LIBS"; //relative to the PluginDirectory
        private const string SrcmlCSharpBinDir = @"LIBS\srcML-Win-cSharp"; //relative to the PluginDirectory
        private const string DefaultCacheFile = "swum-cache.txt";

        private readonly XName[] functionTypes = new XName[] { SRC.Function, SRC.Constructor, SRC.Destructor };
        private SwumBuilder builder;
        private Dictionary<string, SwumDataRecord> signaturesToSwum;

        /// <summary>
        /// Private constructor for a new SwumManager.
        /// </summary>
        private SwumManager() {
            PluginDirectory = ".";
            builder = new UnigramSwumBuilder { Splitter = new CamelIdSplitter() };
            signaturesToSwum = new Dictionary<string, SwumDataRecord>();
            CacheLoaded = false;
        }

        /// <summary>
        /// Gets the singleton instance of SwumManager.
        /// </summary>
        public static SwumManager Instance { 
            get {
                if(instance == null) {
                    instance = new SwumManager();
                }
                return instance;
            }
        }

        /// <summary>
        /// Gets or sets the SwumBuilder used to construct SWUM.
        /// </summary>
        public SwumBuilder Builder {
            get { return builder; }
            set { builder = value; }
        }

        /// <summary>
        /// The directory containing the Sando plugin files.
        /// </summary>
        public string PluginDirectory { get; set; }

        /// <summary>
        /// The path to the cache file on disk.
        /// </summary>
        public string CachePath { get; private set; }

        /// <summary>
        /// Indicates whether a cache file has been successfully loaded.
        /// </summary>
        public bool CacheLoaded { get; private set; }

        /// <summary>
        /// Initializes the SWUM data from the cache file in the given directory. Any previously constructed SWUMs will be deleted.
        /// </summary>
        /// <param name="pluginDirectory">The directory containing the Sando plugin files.</param>
        /// <param name="cacheDirectory">The path for the directory containing the SWUM cache file.</param>
        public void Initialize(string pluginDirectory, string cacheDirectory) {
            Initialize(pluginDirectory, cacheDirectory, true);
        }

        /// <summary>
        /// Initializes the SWUM data from the cache file in the given directory. Any previously constructed SWUMs will be deleted.
        /// </summary>
        /// <param name="pluginDirectory">The directory containing the Sando plugin files.</param>
        /// <param name="cacheDirectory">The path for the directory containing the SWUM cache file.</param>
        /// <param name="useCache">True to use the existing cache file, if any. False to not load any cache file.</param>
        public void Initialize(string pluginDirectory, string cacheDirectory, bool useCache) {
            Clear();
            PluginDirectory = pluginDirectory;
            CachePath = Path.Combine(cacheDirectory, DefaultCacheFile);

            if(useCache) {
                if(!File.Exists(CachePath)) {
                    Debug.WriteLine(string.Format("SwumManager.Initialize() - Cache file does not exist: {0}", CachePath));
                    return;
                }
                ReadSwumCache(CachePath);
                CacheLoaded = true;
            }
        }

        /// <summary>
        /// Generates SWUMs for the method definitions within the given source file.
        /// </summary>
        /// <param name="sourcePath">The path to the source file.</param>
        public void AddSourceFile(string sourcePath) {
            string fullPath = Path.GetFullPath(sourcePath);
            string fileExt = Path.GetExtension(fullPath);
            if(fileExt != null) {
                //remove the dot from the extension
                if(fileExt.Length > 1 && fileExt[0] == '.') {
                    fileExt = fileExt.Substring(1);
                } else if(fileExt.Length == 1 && fileExt[0] == '.') {
                    fileExt = string.Empty;
                }
            }

            Src2SrcMLRunner srcmlConverter;
            if(string.Compare(fileExt, "cs", StringComparison.InvariantCultureIgnoreCase) == 0) {
                srcmlConverter = new Src2SrcMLRunner(Path.Combine(PluginDirectory, SrcmlCSharpBinDir));
            } else {
                srcmlConverter = new Src2SrcMLRunner(Path.Combine(PluginDirectory, SrcmlBinDir));
                srcmlConverter.ExtensionMapping.WillReturnDefaultValues = true;
                if(!srcmlConverter.ExtensionMapping.Keys.Contains(fileExt)) {
                    //if this is an unsupported file type, return and do nothing
                    return;
                }
            }
            var tempSrcMLFile = srcmlConverter.GenerateSrcMLFromFile(fullPath, Path.GetTempFileName());
            try {
                AddSrcMLFile(tempSrcMLFile);
            } finally {
                File.Delete(tempSrcMLFile.FileName);
            }

        }

        /// <summary>
        /// Generates SWUMs for the method definitions within the given SrcML file
        /// </summary>
        /// <param name="srcmlFile">A SrcML file.</param>
        public void AddSrcMLFile(SrcMLFile srcmlFile) {
            AddSwumForMethodDefinitions(srcmlFile);
        }

        /// <summary>
        /// Regenerates SWUMs for the methods in the given source file. Any previously-generated SWUMs for the file will first be removed.
        /// </summary>
        /// <param name="sourcePath">The path of the file to update.</param>
        public void UpdateSourceFile(string sourcePath) {
            RemoveSourceFile(sourcePath);
            AddSourceFile(sourcePath);
        }

        /// <summary>
        /// Removes any SWUMs that were generated from the given source file.
        /// </summary>
        /// <param name="sourcePath">The path of the file to remove.</param>
        public void RemoveSourceFile(string sourcePath) {
            var fullPath = Path.GetFullPath(sourcePath);
            var sigsToRemove = new HashSet<string>();
            lock(signaturesToSwum) {
                foreach(var sig in signaturesToSwum.Keys) {
                    var sdr = signaturesToSwum[sig];
                    if(sdr.FileNames.Contains(fullPath)) {
                        sdr.FileNames.Remove(fullPath);
                        if(!sdr.FileNames.Any()) {
                            sigsToRemove.Add(sig);
                        }
                    }
                }

                //remove signatures that no longer have any file names
                //(This is separate from the above loop because you can't delete keys while you're enumerating them.)
                foreach(var sig in sigsToRemove) {
                    signaturesToSwum.Remove(sig);
                }
            }
        }

        /// <summary>
        /// Clears any constructed SWUMs.
        /// </summary>
        public void Clear() {
            lock(signaturesToSwum) {
                signaturesToSwum.Clear();
            }
        }

        /// <summary>
        /// Prints the SWUM cache to the file specified in CachePath.
        /// </summary>
        public void PrintSwumCache() {
            PrintSwumCache(string.IsNullOrWhiteSpace(CachePath) ? DefaultCacheFile : CachePath);
        }

        /// <summary>
        /// Prints the SWUM cache to the specified file.
        /// </summary>
        /// <param name="path">The path to print the SWUM cache to.</param>
        public void PrintSwumCache(string path) {
            if(string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Path is empty or null.", "path");
            }
            using(StreamWriter sw = new StreamWriter(path)) {
                PrintSwumCache(sw);
            }
        }

        /// <summary>
        /// Prints the SWUM cache to the specified output stream.
        /// </summary>
        /// <param name="output">A TextWriter to print the SWUM cache to.</param>
        public void PrintSwumCache(TextWriter output) {
            if(output == null) {
                throw new ArgumentNullException("output");
            }
            lock(signaturesToSwum) {
                foreach(var kvp in signaturesToSwum) {
                    output.WriteLine("{0}|{1}", kvp.Key, kvp.Value.ToString());
                }
            }
        }

        /// <summary>
        /// Initializes the cache of SWUM data from a file. Any existing SWUM data will be cleared before reading the file.
        /// </summary>
        /// <param name="path">The path to the SWUM cache file.</param>
        public void ReadSwumCache(string path) {
            using(var cacheFile = new StreamReader(path)) {
                lock(signaturesToSwum) {
                    //clear any existing SWUMs
                    signaturesToSwum.Clear();

                    //read each SWUM entry from the cache file
                    string entry;
                    while((entry = cacheFile.ReadLine()) != null) {
                        //the expected format is <signature>|<SwumDataRecord.ToString()>
                        string[] fields = entry.Split(new[] {'|'}, 2);
                        if(fields.Length != 2) {
                            Debug.WriteLine(string.Format("Too few fields in SWUM cache entry: {0}", entry));
                            continue;
                        }
                        try {
                            string sig = fields[0].Trim();
                            string data = fields[1].Trim();
                            signaturesToSwum[sig] = SwumDataRecord.Parse(data);
                        } catch(FormatException fe) {
                            Debug.WriteLine(string.Format("Improperly formatted SwumDataRecord in Swum cache entry: {0}", entry));
                            Debug.WriteLine(fe.Message);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Returns the SWUM data for the given method signature.
        /// </summary>
        /// <param name="methodSignature">The method signature to get SWUM data about.</param>
        /// <returns>A SwumDataRecord containing the SWUM data about the given method, or null if no data is found.</returns>
        public SwumDataRecord GetSwumForSignature(string methodSignature) {
            if(methodSignature == null) { throw new ArgumentNullException("methodSignature"); }

            SwumDataRecord result = null;
            lock(signaturesToSwum) {
                if(signaturesToSwum.ContainsKey(methodSignature)) {
                    result = signaturesToSwum[methodSignature];
                } 
            }
            return result;
        }

        /// <summary>
        /// Returns a dictionary mapping method signatures to their SWUM data.
        /// </summary>
        public Dictionary<string,SwumDataRecord> GetSwumData() {
            var currentSwum = new Dictionary<string, SwumDataRecord>();
            lock(signaturesToSwum) {
                foreach(var entry in signaturesToSwum) {
                    currentSwum[entry.Key] = entry.Value;
                }
            }
            return currentSwum;
        } 

        #region Protected methods
        /// <summary>
        /// Constructs SWUMs for each of the methods defined in <paramref name="srcFile"/> and adds them to the cache.
        /// </summary>
        /// <param name="srcFile">The srcML file containing the method definitions.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="srcFile"/> is null.</exception>
        protected void AddSwumForMethodDefinitions(SrcMLFile srcFile) {
            if(srcFile == null) { throw new ArgumentNullException("srcFile"); }

            //iterate over each method definition in the SrcML file
            foreach(XElement file in srcFile.FileUnits) {
                var fileAttribute = file.Attribute("filename");
                string filePath = fileAttribute != null ? fileAttribute.Value : srcFile.FileName;
                var functions = from func in file.Descendants()
                                where functionTypes.Contains(func.Name) && !func.Ancestors(SRC.Declaration).Any()
                                select func;
                foreach(XElement func in functions) {
                    //construct SWUM on the function (if necessary)
                    string sig = SrcMLElement.GetMethodSignature(func);
                    lock(signaturesToSwum) {
                        if(signaturesToSwum.ContainsKey(sig)) {
                            //update the SwumDataRecord with the filename of the duplicate method
                            signaturesToSwum[sig].FileNames.Add(filePath);
                        } else {
                            MethodDeclarationNode mdn = ConstructSwumFromMethodElement(func);
                            var swumData = ProcessSwumNode(mdn);
                            swumData.FileNames.Add(filePath);
                            signaturesToSwum[sig] = swumData;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Constructs SWUM on the given srcML method element. 
        /// </summary>
        /// <param name="methodElement">The srcML element to use. This can be either a Function, Constructor or Destructor.</param>
        /// <returns>A MethodDeclarationNode with SWUM rules applied to it.</returns>
        protected MethodDeclarationNode ConstructSwumFromMethodElement(XElement methodElement) {
            return ConstructSwumFromMethodElement(methodElement, null);
        }

        /// <summary>
        /// Constructs SWUM on the given srcML method element. 
        /// </summary>
        /// <param name="methodElement">The srcML element to use. This can be either a Function, Constructor or Destructor.</param>
        /// <param name="className">The class on which this method is declared.</param>
        /// <returns>A MethodDeclarationNode with SWUM rules applied to it.</returns>
        protected MethodDeclarationNode ConstructSwumFromMethodElement(XElement methodElement, string className) {
            if(!functionTypes.Contains(methodElement.Name)) {
                throw new ArgumentException("Element not a valid method type.", "methodElement");
            }

            string funcName = SrcMLHelper.GetNameForMethod(methodElement).Value;
            MethodContext mc = ContextBuilder.BuildMethodContext(methodElement);
            //set the declaring class name, if it's been passed in
            //this is necessary because the xml from the database for inline methods won't have the surrounding class xml
            if(string.IsNullOrEmpty(mc.DeclaringClass) && !string.IsNullOrEmpty(className)) {
                mc.DeclaringClass = className;
            }

            MethodDeclarationNode mdn = new MethodDeclarationNode(funcName, mc);
            builder.ApplyRules(mdn);
            return mdn;
        }

        /// <summary>
        /// Constructs a method signature based on a method call.
        /// </summary>
        /// <param name="name">The name of the method being called.</param>
        /// <param name="mc">A MethodContext object populated with data from the method call.</param>
        /// <returns>A method signature.</returns>
        protected string GetMethodSignatureFromCall(string name, MethodContext mc) {
            if(name == null) { throw new ArgumentNullException("name"); }
            if(name == string.Empty) { throw new ArgumentException("The method name must be non-empty.", "name"); }
            if(mc == null) { throw new ArgumentNullException("mc"); }
            
            StringBuilder sig = new StringBuilder();
            if(mc.IsStatic) {
                sig.Append("static");
            }
            if(!string.IsNullOrEmpty(mc.IdType)) {
                sig.AppendFormat(" {0}", mc.IdType);
            }
            //add method name
            if(!string.IsNullOrEmpty(mc.DeclaringClass)) {
                sig.AppendFormat(" {0}::{1}(", mc.DeclaringClass, name);
            } else {
                sig.AppendFormat(" {0}(", name);
            }
            //add method parameters
            if(mc.FormalParameters.Count > 0) {
                for(int i = 0; i < mc.FormalParameters.Count - 1; i++) {
                    sig.AppendFormat("{0}, ", mc.FormalParameters[i].ParameterType);
                }
                sig.Append(mc.FormalParameters.Last().ParameterType);
            }
            sig.Append(")");
            return sig.ToString().TrimStart(' ');
        }

        /// <summary>
        /// Performs additional processing on a MethodDeclarationNode to put the data in the right format for the Comment Generator.
        /// </summary>
        /// <param name="swumNode">The MethodDeclarationNode from SWUM to process.</param>
        /// <returns>A SwumDataRecord containing <paramref name="swumNode"/> and various data extracted from it.</returns>
        protected SwumDataRecord ProcessSwumNode(MethodDeclarationNode swumNode) {
            var record = new SwumDataRecord();
            record.SwumNode = swumNode;
            //set Action
            if(swumNode.Action != null) {
                record.Action = swumNode.Action.ToPlainString();
                record.ParsedAction = swumNode.Action.GetParse();
            }
            //TODO: action is not lowercased. Should it be?

            //set Theme
            if(swumNode.Theme != null) {
                if(swumNode.Theme is EquivalenceNode && ((EquivalenceNode)swumNode.Theme).EquivalentNodes.Any()) {
                    var firstNode = ((EquivalenceNode)swumNode.Theme).EquivalentNodes[0];
                    record.Theme = firstNode.ToPlainString().ToLower();
                    record.ParsedTheme = firstNode.GetParse();
                } else {
                    record.Theme = swumNode.Theme.ToPlainString().ToLower();
                    record.ParsedTheme = swumNode.Theme.GetParse();
                }
            }

            //set Indirect Object
            if(string.Compare(record.Action, "set", StringComparison.InvariantCultureIgnoreCase) == 0) {
                //special handling for setter methods?
                //TODO: should this set the IO to the declaring class? will that work correctly for sando?
                
            } else {
                if(swumNode.SecondaryArguments != null && swumNode.SecondaryArguments.Any()) {
                    var IONode = swumNode.SecondaryArguments.First();
                    if(IONode.Argument is EquivalenceNode && ((EquivalenceNode)IONode.Argument).EquivalentNodes.Any()) {
                        var firstNode = ((EquivalenceNode)IONode.Argument).EquivalentNodes[0];
                        record.IndirectObject = firstNode.ToPlainString().ToLower();
                        record.ParsedIndirectObject = firstNode.GetParse();
                    } else {
                        record.IndirectObject = IONode.Argument.ToPlainString().ToLower();
                        record.ParsedIndirectObject = IONode.Argument.GetParse();
                    }
                } 
            }

            return record;
        }
        #endregion Protected methods
    }

 

    ///XXX: Temporary until I figure out where SrcMLElement went within the SrcML.NET project.  Should just use that one.
    /// 
    /// <summary>
    /// Contains static utility methods that act upon srcML XElements.
    /// </summary>
    public static class SrcMLElement
    {
        /// <summary>
        /// Gets the method signature from the method definition srcML element.
        /// </summary>
        /// <param name="methodElement">The srcML method element to extract the signature from.</param>
        /// <returns>The method signature</returns>
        public static string GetMethodSignature(XElement methodElement)
        {
            if(methodElement == null)
            {
                throw new ArgumentNullException("methodElement");
            }
            if(!(new[] {SRC.Function, SRC.Constructor, SRC.Destructor}).Contains(methodElement.Name))
            {
                throw new ArgumentException(string.Format("Not a valid method element: {0}", methodElement.Name), "methodElement");
            }

            var sig = new StringBuilder();
            var paramListElement = methodElement.Element(SRC.ParameterList);
            //add all the text and whitespace prior to the parameter list
            foreach(var n in paramListElement.NodesBeforeSelf())
            {
                if(n.NodeType == XmlNodeType.Element)
                {
                    sig.Append(((XElement)n).Value);
                }
                else if(n.NodeType == XmlNodeType.Text || n.NodeType == XmlNodeType.Whitespace || n.NodeType == XmlNodeType.SignificantWhitespace)
                {
                    sig.Append(((XText)n).Value);
                }
            }
            //add the parameter list
            sig.Append(paramListElement.Value);

            //convert whitespace chars to spaces and condense any consecutive whitespaces.
            return Regex.Replace(sig.ToString().Trim(), @"\s+", " ");
        }
    }
    
}