﻿
namespace Sando.SearchEngine.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using NUnit.Framework;
    using Sando.SearchEngine;
    using Lucene.Net.Analysis;
    using Sando.Core;
    using Sando.Indexer;
    using Sando.Indexer.Documents;
    using Lucene.Net.Search;
    [TestFixture]
    public class CodeSearcherFixture
    {
		private DocumentIndexer Indexer;
    	private string IndexerPath;


    	[Test]
        public void TestCreateCodeSearcher()
        {
            SimpleAnalyzer analyzer = new SimpleAnalyzer();
    		var indexer = DocumentIndexerFactory.CreateIndexer(System.IO.Path.GetTempPath() + "luceneindexer", AnalyzerType.Standard);
			//TODO - How do we get an instance of IIndexerSearcher?
            Assert.DoesNotThrow(() => new CodeSearcher( null ));
        }

        [Test]     
        public void PerformBasicSearch()
        {        	
			//TODO - get an IIndexerSearcher from the Indexer project
        	CodeSearcher cs = new CodeSearcher(null);            
            List<CodeSearchResult> result = cs.Search("SimpleName");
            Assert.AreEqual(2, result.Count);                                 
        }

		[TestFixtureSetUp]
    	public void CreateIndexer()
    	{

			Indexer = DocumentIndexerFactory.CreateIndexer(System.IO.Path.GetTempPath() + "luceneindexer", AnalyzerType.Standard);
    		ClassElement classElement = new ClassElement()
    		                            	{
    		                            		AccessLevel = Core.AccessLevel.Public,
    		                            		DefinitionLineNumber = 11,
    		                            		ExtendedClasses = "SimpleClassBase",
    		                            		FileName = "SimpleClass.cs",
    		                            		FullFilePath = "C:/Projects/SimpleClass.cs",
    		                            		Id = Guid.NewGuid(),
    		                            		ImplementedInterfaces = "IDisposable",
    		                            		Name = "SimpleName",
    		                            		Namespace = "Sanod.Indexer.UnitTests"
    		                            	};
    		SandoDocument sandoDocument = ClassDocument.Create(classElement);
    		Indexer.AddDocument(sandoDocument);
    		MethodElement methodElement = new MethodElement()
    		                              	{
    		                              		AccessLevel = Core.AccessLevel.Protected,
    		                              		Name = "SimpleName",
    		                              		Id = Guid.NewGuid(),
    		                              		ReturnType = "Void",
    		                              		ClassId = Guid.NewGuid()
    		                              	};
    		sandoDocument = MethodDocument.Create(methodElement);
    		Indexer.AddDocument(sandoDocument);
    		Indexer.CommitChanges();    		
    	}

		[TestFixtureTearDown]
    	public void ShutdownIndexer()
    	{
			Indexer.Dispose();   
    	}
    }
}
