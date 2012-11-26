﻿using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Sando.ExtensionContracts.ResultsReordererContracts;
using System.Threading;
using Sando.Indexer.Searching.Criteria;
using Sando.UI.View;
using Sando.ExtensionContracts.ProgramElementContracts;

namespace Sando.UI.InterleavingExperiment
{
    public static class LexSearch
    {
        public static int NumRawResults;

        private static DTE2 _dte = null;
        private static readonly AutoResetEvent _auto = new AutoResetEvent(false);
        private static string _selectionText = String.Empty;

        //has to be at class level, http://support.microsoft.com/kb/555430
        private static FindEvents _findEvents = null;

        public static List<CodeSearchResult> GetResults(string query)
        {
            InitDte2();

            _findEvents = _dte.Events.FindEvents;
            _findEvents.FindDone += LexSearch.OnFindDone;
            Find objFind = _dte.Find;
            objFind.FindReplace(vsFindAction.vsFindActionFindAll, query, 0, "",
                                vsFindTarget.vsFindTargetSolution, "", "",
                                vsFindResultsLocation.vsFindResults1);

            _auto.WaitOne();
            _findEvents.FindDone -= OnFindDone;
      
            return ParseFindInFilesText(_selectionText);
        }

        private static List<CodeSearchResult> ParseFindInFilesText(string text)
        {
        	const int maxSearches = 100;
        	int searchCount = 0;
			var relevantMethods = new List<CodeSearchResult>();
            var searcher = SearchManager.GetCurrentSearcher();            
            if(searcher!=null)
            {
                _selectionText = text;                
                var lines = text.Split('\n');
                var resultLines = lines.Skip(1).Take(lines.Length - 2);
                NumRawResults = resultLines.Count();
                foreach (var line in resultLines)
                {
                    var searchCriteria = GetCriteria(line);
                    if (searchCriteria != null)
                    {
                        var results = searcher.UnalteredSearch(searchCriteria.Item1);
                        if (results != null && results.Count > 0)
                        {
							if(searchCount > maxSearches) break;
							var closest = FindSandoMatch(results, searchCriteria);
                            if (closest != null)
                                relevantMethods.Add(closest);
                        }
                    }
                	searchCount++;
                }
            }
            return relevantMethods.Distinct().ToList();
        }

        private static CodeSearchResult FindSandoMatch(List<CodeSearchResult> results, Tuple<SearchCriteria, int> searchCriteria)
        {
            List<CodeSearchResult> containerTypeResults = new List<CodeSearchResult>();
            foreach (var current in results)
            {
                if (searchCriteria.Item2 == current.Element.DefinitionLineNumber) return current;
                if (current.Element is MethodElement || current.Element is ClassElement) containerTypeResults.Add(current);
            }

            if (containerTypeResults.Count > 0)
            {
                return FindClosestMatch(containerTypeResults, searchCriteria);
            }

            return null;
        }

        private static CodeSearchResult FindClosestMatch(List<CodeSearchResult> containerTypeResults, Tuple<SearchCriteria, int> searchCriteria)
        {
            var closest = InitializeClosest(searchCriteria, containerTypeResults);
            if (closest != null)
            {
                foreach (var current in containerTypeResults)
                {
                    var distanceClosest =
                        Math.Abs(closest.Element.DefinitionLineNumber - searchCriteria.Item2);
                    var distanceCurrent =
                        Math.Abs(current.Element.DefinitionLineNumber - searchCriteria.Item2);
                    if (distanceClosest > distanceCurrent &&
                        current.Element.DefinitionLineNumber <= searchCriteria.Item2)
                    {
                        closest = current;
                    }
                }
            }
            return closest;
        }

        private static CodeSearchResult InitializeClosest(Tuple<SearchCriteria, int> searchCriteria, List<CodeSearchResult> results)
        {
            CodeSearchResult closest = null;
            foreach (var current in results)
            {
                if (current.Element.DefinitionLineNumber <= searchCriteria.Item2)
                {
                    closest = current;
                    break;
                }
            }
            return closest;
        }

        //Ex: C:\Users\USDASHE1\Documents\VsProjects\Sando-clone\Indexer\Indexer\IndexState\CppHeaderElementResolver.cs(20):			//first parse all the included header files. they are the same in all the unresolved elements
        //public for testing
        public static Tuple<SearchCriteria,int> GetCriteria(string line)
        {
            if(line.Contains(')')&& line.Contains('(')&&line.Contains("):"))
            {
                var seperators = new char[]{'(', ')'};
                var splitLine = line.Split(seperators);            
                if(splitLine.Count()>=3)
                {
                    return GetCriteria(splitLine);
                }
            }
            return null;
        }

        private static Tuple<SearchCriteria, int> GetCriteria(string[] splitLine)
        {
            var file = splitLine[0];
            var lineNumber = int.Parse(splitLine[1]);
            var criteria = new SimpleSearchCriteria();
            criteria.SearchByProgramElementType = true;
            foreach (var aType in Enum.GetValues(typeof(ProgramElementType)).Cast<ProgramElementType>().ToList())
            {
                criteria.ProgramElementTypes.Add(aType);
            }     
            criteria.SearchByLocation = true;
            criteria.Locations.Add(file.Trim());
            return Tuple.Create(criteria as SearchCriteria, lineNumber);
        }

        private static void OnFindDone(vsFindResult result, bool cancelled) 
        {
            if (result == vsFindResult.vsFindResultFound)
            {
				string vsWindowKindFindResults1 = "{0F887920-C2B6-11D2-9375-0080C747D9A0}";
                EnvDTE.Window resultsWin = _dte.Windows.Item(vsWindowKindFindResults1);                
                var selection = resultsWin.Selection as TextSelection;
                _selectionText = String.Empty;
                if (selection != null)
                {
                    selection.SelectAll();
                    _selectionText = selection.Text;
                }
				resultsWin.Visible = false;
            }
            _auto.Set();
        }
        
        private static void InitDte2()
        {
            if (_dte == null)
            {
                _dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            }
        }
    }
}