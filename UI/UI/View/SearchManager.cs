﻿using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Sando.Core.Extensions;
using Sando.Core.Extensions.Logging;
using Sando.ExtensionContracts.ResultsReordererContracts;
using Sando.Indexer.Searching;
using Sando.SearchEngine;
using Sando.Indexer.Searching.Criteria;
using Sando.Core.Tools;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Sando.UI.View
{
	public class SearchManager
	{

		private static CodeSearcher _currentSearcher;
		private string _currentDirectory = "";
		private bool _invalidated = true;
		private ISearchResultListener _myDaddy;

		public SearchManager(ISearchResultListener daddy)
		{
			_myDaddy = daddy;
		}

		public static CodeSearcher GetCurrentSearcher()
		{
			return _currentSearcher;
		}

		private CodeSearcher GetSearcher(UIPackage myPackage)
		{
			CodeSearcher codeSearcher = _currentSearcher;
			if(codeSearcher == null || !myPackage.GetCurrentDirectory().Equals(_currentDirectory) || _invalidated)
			{
				_invalidated = false;
				_currentDirectory = myPackage.GetCurrentDirectory();
				codeSearcher = new CodeSearcher(IndexerSearcherFactory.CreateSearcher(myPackage.GetCurrentSolutionKey()));
			}
			return codeSearcher;
		}
        
        [MethodImpl(MethodImplOptions.Synchronized)]
		public string Search(String searchString, BackgroundWorker worker = null, SimpleSearchCriteria searchCriteria = null, bool interactive = true)
		{
			try
			{
				var returnString = "";
				if(!string.IsNullOrEmpty(searchString))
				{
					_myDaddy.Update(new List<CodeSearchResult>().AsQueryable());
					var myPackage = UIPackage.GetInstance();
					if(myPackage.GetCurrentDirectory() != null)
					{
						_currentSearcher = GetSearcher(myPackage);
						bool searchStringContainedInvalidCharacters = false;
						if(worker != null) worker.ReportProgress(33);
						IQueryable<CodeSearchResult> results =
							_currentSearcher.Search(
								GetCriteria(searchString, out searchStringContainedInvalidCharacters, searchCriteria),
								GetSolutionName(myPackage)).AsQueryable();
						if(worker != null) worker.ReportProgress(66);
						IResultsReorderer resultsReorderer =
							ExtensionPointsRepository.Instance.GetResultsReordererImplementation();
						results = resultsReorderer.ReorderSearchResults(results);
						_myDaddy.Update(results);
						if(searchStringContainedInvalidCharacters)
						{
							_myDaddy.UpdateMessage(
								"Invalid Query String - only complete words or partial words followed by a '*' are accepted as input.");
							return null;
						}
						if(myPackage.IsPerformingInitialIndexing())
						{
							returnString +=
								"Sando is still performing its initial index of this project, results may be incomplete.";
						}
						if(!results.Any())
						{
							returnString = "No results found. " + returnString;
						}
						else if(returnString.Length == 0)
						{
							returnString = results.Count() + " results returned";
						}
						else
						{
							returnString = results.Count() + " results returned. " + returnString;
						}
						_myDaddy.UpdateMessage(returnString);
						return null;
					}
					else
					{
						_myDaddy.UpdateMessage(
							"Sando searches only the currently open Solution.  Please open a Solution and try again.");
						return null;
					}
				}
			}
			catch(Exception e)
			{
				FileLogger.DefaultLogger.Error("An unexpected exception occured in searcher");
				FileLogger.DefaultLogger.Error(e.StackTrace);
				_myDaddy.UpdateMessage(
					"Invalid Query String - only complete words or partial words followed by a '*' are accepted as input.");
			}
			return null;
		}

		private string GetSolutionName(UIPackage myPackage)
		{
			try
			{
				return Path.GetFileNameWithoutExtension(myPackage.GetCurrentSolutionKey().GetSolutionPath());
			}
			catch(Exception e)
			{
				return "";
			}
		}

		//public string SearchOnReturn(object sender, KeyEventArgs e, String searchString, SimpleSearchCriteria searchCriteria)
		//{
		//	if(e.Key == Key.Return)
		//	{
		//		return Search(searchString, searchCriteria);
		//	}
		//	return "";
		//}

		public void MarkInvalid()
		{
			_invalidated = true;
		}

		#region Private Mthods
		/// <summary>
		/// Gets the criteria.
		/// </summary>
		/// <param name="searchString">Search string.</param>
		/// <returns>search criteria</returns>
		private SearchCriteria GetCriteria(string searchString, out bool searchStringContainedInvalidCharacters, SimpleSearchCriteria searchCriteria = null)
		{
			if(searchCriteria == null)
				searchCriteria = new SimpleSearchCriteria();
			var criteria = searchCriteria;
			criteria.NumberOfSearchResultsReturned = UIPackage.GetSandoOptions(UIPackage.GetInstance()).NumberOfSearchResultsReturned;
			searchString = ExtensionPointsRepository.Instance.GetQueryRewriterImplementation().RewriteQuery(searchString);
			searchStringContainedInvalidCharacters = WordSplitter.InvalidCharactersFound(searchString);
			List<string> searchTerms = WordSplitter.ExtractSearchTerms(searchString);

			string[] splitTerms = searchString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			foreach(string term in splitTerms)
			{
				if(term.Any(Char.IsLower) && term.Any(Char.IsUpper) || term.Any(Char.IsLetter) && term.Any(Char.IsDigit))
				{
					searchTerms.Add(term);
					//add this because we know this will be a lexical type search
					//searchTerms.Add(term + "*");
				}
			}

			//if there is only one term we add a star to it to add partial matches
			//if(searchTerms.Count == 1)
			//{
			//	searchTerms.Add(searchTerms[0] + "*");
			//}

			criteria.SearchTerms = new SortedSet<string>(searchTerms);
			return criteria;
		}
		#endregion
	}

}
