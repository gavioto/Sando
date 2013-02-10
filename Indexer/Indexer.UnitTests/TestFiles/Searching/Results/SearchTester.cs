﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Snowball;
using NUnit.Framework;
using Sando.Core;
using Sando.DependencyInjection;
using Sando.ExtensionContracts.ProgramElementContracts;
using Sando.Indexer.Documents;
using Sando.Indexer.Searching;
using Sando.Indexer.Searching.Criteria;
using Sando.Parser;
using UnitTestHelpers;

namespace Sando.Indexer.UnitTests.TestFiles.Searching.Results
{
    public class SearchTester
    {
        private readonly SrcMLCSharpParser _parser;
        private readonly string _luceneTempIndexesDirectory;
        private readonly string _sandoAssemblyDirectoryPath;
        
        private DocumentIndexer _indexer;

        public static SearchTester Create()
        {
            return new SearchTester();
        }

        private SearchTester()
        {
            //set up generator
            _parser = new SrcMLCSharpParser(new ABB.SrcML.SrcMLGenerator(@"LIBS\SrcML"));
            _luceneTempIndexesDirectory = Path.Combine(Path.GetTempPath(), "basic");
            _sandoAssemblyDirectoryPath = _luceneTempIndexesDirectory;
            Directory.CreateDirectory(_luceneTempIndexesDirectory);
            TestUtils.ClearDirectory(_luceneTempIndexesDirectory);
        }

        public void CheckFolderForExpectedResults(string searchString, string methodNameToFind, string solutionPath)
        {
            var key = new SolutionKey(Guid.NewGuid(), solutionPath, _luceneTempIndexesDirectory, _sandoAssemblyDirectoryPath);
            ServiceLocator.RegisterInstance(key);
            ServiceLocator.RegisterInstance<Analyzer>(new SnowballAnalyzer("English"));
            _indexer = new DocumentIndexer();
            ServiceLocator.RegisterInstance(_indexer);

            try
            {
                IndexFilesInDirectory(solutionPath);
                var results = GetResults(searchString, key);
                Assert.IsTrue(HasResults(methodNameToFind, results), "Can't find expected results");
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message + ". " + ex.StackTrace);
            }
            finally
            {
                if (_indexer != null)
                    _indexer.Dispose(true);
            }
        }

        private void IndexFilesInDirectory(string solutionPath)
        {

            var files = Directory.GetFiles(solutionPath);
            foreach (var file in files)
            {
                string fullPath = Path.GetFullPath(file);
                var srcMl = _parser.Parse(fullPath);
                foreach (var programElement in srcMl)
                {
                    _indexer.AddDocument(DocumentFactory.Create(programElement));
                }
            }
            _indexer.CommitChanges();
        }

        private IEnumerable<Tuple<ProgramElement, float>> GetResults(string searchString, SolutionKey key)
        {
            var searcher = new IndexerSearcher();
            var criteria = new SimpleSearchCriteria();
            criteria.SearchTerms = new SortedSet<string>(searchString.Split(' ').ToList());
            var results = searcher.Search(criteria);
            return results;
        }


        private bool HasResults(string methodNameToFind, IEnumerable<Tuple<ProgramElement, float>> results)
        {
            return results.Select(result => result.Item1).OfType<MethodElement>().Any(method => method.Name.Equals(methodNameToFind));
        }
    }
}
