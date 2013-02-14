﻿using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using Lucene.Net.Analysis;
using Lucene.Net.Search;
using NUnit.Framework;
using Sando.Core;
using Sando.DependencyInjection;
using Sando.ExtensionContracts.ProgramElementContracts;
using Sando.Indexer.Documents;
using Sando.UnitTestHelpers;
using UnitTestHelpers;

namespace Sando.Indexer.UnitTests
{
    [TestFixture]
	public class DocumentIndexerTest
	{
    	[Test]
		public void DocumentIndexer_ConstructorDoesNotThrowWhenValidData()
		{
			try
			{
				_documentIndexer = new DocumentIndexer();
			}
			catch(Exception ex)
			{
				Assert.Fail(ex.Message + ". " + ex.StackTrace);
			}
		}

		[Test]
		public void DocumentIndexer_AddDocumentDoesNotThrowWhenValidData()
		{
			try
			{
                _documentIndexer = new DocumentIndexer();
				ClassElement classElement = SampleProgramElementFactory.GetSampleClassElement();
				SandoDocument sandoDocument = DocumentFactory.Create(classElement);
				Assert.NotNull(sandoDocument);
				Assert.NotNull(sandoDocument.GetDocument());
				_documentIndexer.AddDocument(sandoDocument);
			}
			catch(Exception ex)
			{
				Assert.Fail(ex.Message + ". " + ex.StackTrace);
			}
		}

		[Test]
		public void DocumentIndexer_AddDocumentThrowsWhenProgramElementIsNull()
		{
			_documentIndexer = new DocumentIndexer();
			try
			{
				_documentIndexer.AddDocument(null);
			}
			catch
			{
				//contract exception catched here
			}
			Assert.True(_contractFailed, "Contract should fail!");
		}

        [Test]
        public void DocumentIndexer_DeleteDocuments()
        {
            try
            {                                
                TestUtils.ClearDirectory(_luceneTempIndexesDirectory);
                _documentIndexer = new DocumentIndexer(TimeSpan.FromSeconds(1));
                MethodElement sampleMethodElement = SampleProgramElementFactory.GetSampleMethodElement();
                _documentIndexer.AddDocument(DocumentFactory.Create(sampleMethodElement));
                int numDocs = _documentIndexer.GetNumberOfIndexedDocuments();
                Assert.IsTrue(numDocs == 1);
                _documentIndexer.DeleteDocuments(sampleMethodElement.FullFilePath);
                int docs = _documentIndexer.GetNumberOfIndexedDocuments();
                Assert.IsTrue(docs == 0);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message + ". " + ex.StackTrace);
            }
        }

        [Test]
        public void GIVEN_DocumentIndexer_WHEN_IndexSearcherIsClosed_AND_SearchFailed_THEN_IndexSearcherIsRecreated_AND_SearchReturnsResults()
        {
            try
            {
                TestUtils.ClearDirectory(_luceneTempIndexesDirectory);
                _documentIndexer = new DocumentIndexer(TimeSpan.FromMilliseconds(500));
                var sampleMethodElement = SampleProgramElementFactory.GetSampleMethodElement();
                _documentIndexer.AddDocument(DocumentFactory.Create(sampleMethodElement));
                const string searchQueryString = "body: sth";
			    var query = _documentIndexer.QueryParser.Parse(searchQueryString);
			    const int hitsPerPage = 20;
                var collector = TopScoreDocCollector.create(hitsPerPage, true);
                _documentIndexer.NUnit_CloseIndexSearcher();
                _documentIndexer.Search(query, collector);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message + ". " + ex.StackTrace);
            }
        }

        [Test]
        public void GIVEN_DocumentIndexer_WHEN_IndexSearcherIsClosed_THEN_IndexSearcherIsRecreatedInBackgroundThread_AND_SearchReturnsResults()
        {
            try
            {
                TestUtils.ClearDirectory(_luceneTempIndexesDirectory);
                _documentIndexer = new DocumentIndexer(TimeSpan.FromMilliseconds(100));
                var sampleMethodElement = SampleProgramElementFactory.GetSampleMethodElement();
                _documentIndexer.AddDocument(DocumentFactory.Create(sampleMethodElement));
                const string searchQueryString = "body: sth";
                var query = _documentIndexer.QueryParser.Parse(searchQueryString);
                const int hitsPerPage = 20;
                var collector = TopScoreDocCollector.create(hitsPerPage, true);
                _documentIndexer.NUnit_CloseIndexSearcher();
                Thread.Sleep(600);
                _documentIndexer.Search(query, collector);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message + ". " + ex.StackTrace);
            }
        }

        [SetUp]
        public void ResetContract()
        {
            _contractFailed = false;
            Contract.ContractFailed += (sender, e) =>
            {
                e.SetHandled();
                e.SetUnwind();
                _contractFailed = true;
            };
            ServiceLocator.RegisterType<Analyzer, SimpleAnalyzer>();
        }

		[TestFixtureSetUp]
		public void SetUp()
		{
		    _luceneTempIndexesDirectory = Path.Combine(Path.GetTempPath(), "basic");
		    if (!Directory.Exists(_luceneTempIndexesDirectory))
		        Directory.CreateDirectory(_luceneTempIndexesDirectory);
            TestUtils.InitializeDefaultExtensionPoints();
		    var solutionKey = ServiceLocator.Resolve<SolutionKey>();
		    var newSolutionKey = new SolutionKey(solutionKey.SolutionId, solutionKey.SolutionPath, _luceneTempIndexesDirectory);
		    ServiceLocator.RegisterInstance(newSolutionKey);
		    ServiceLocator.RegisterType<Analyzer, SimpleAnalyzer>();
		}

		[TearDown]
		public void CloseDocumentIndexer()
		{
			if(_documentIndexer != null)
                _documentIndexer.Dispose(true);
		}

        [TestFixtureTearDown]
        public void TearDown()
        {
            if (Directory.Exists(_luceneTempIndexesDirectory))
                Directory.Delete(_luceneTempIndexesDirectory, true);
        }
        
        private string _luceneTempIndexesDirectory;
        private bool _contractFailed;
		private DocumentIndexer _documentIndexer;
	}
}
