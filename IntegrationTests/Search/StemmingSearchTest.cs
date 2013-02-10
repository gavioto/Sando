﻿using System;
using System.Collections.Generic;
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Snowball;
using NUnit.Framework;
using Sando.Core;
using Sando.Core.Extensions;
using Sando.Core.Tools;
using Sando.DependencyInjection;
using Sando.ExtensionContracts.ProgramElementContracts;
using Sando.ExtensionContracts.ResultsReordererContracts;
using Sando.Indexer;
using Sando.Indexer.Searching;
using Sando.Parser;
using Sando.SearchEngine;
using Sando.UI.Monitoring;
using Sando.Recommender;
using UnitTestHelpers;

namespace Sando.IntegrationTests.Search
{
	[TestFixture]
	public class StemmingSearchTest
	{
		[Test]
		public void SearchIsUsingStemming()
		{
            var codeSearcher = new CodeSearcher(new IndexerSearcher());
			string keywords = "name";
			List<CodeSearchResult> codeSearchResults = codeSearcher.Search(keywords);
			Assert.AreEqual(codeSearchResults.Count, 4, "Invalid results number");
            var classSearchResult = codeSearchResults.Find(el => el.Element.ProgramElementType == ProgramElementType.Class && el.Element.Name == "FileNameTemplate");
			if(classSearchResult == null)
			{
				Assert.Fail("Failed to find relevant search result for search: " + keywords);
			}
			var classElement = classSearchResult.Element as ClassElement;
			Assert.AreEqual(classElement.AccessLevel, AccessLevel.Public, "Class access level differs!");
			Assert.AreEqual(classElement.ExtendedClasses, String.Empty, "Class extended classes differs!");
			Assert.AreEqual(classElement.DefinitionLineNumber, 10, "Class definition line number differs!");
			Assert.True(classElement.FullFilePath.EndsWith("\\TestFiles\\StemmingTestFiles\\FileNameTemplate.cs"), "Class full file path is invalid!");
			Assert.AreEqual(classElement.Name, "FileNameTemplate", "Class name differs!");
			Assert.AreEqual(classElement.ProgramElementType, ProgramElementType.Class, "Program element type differs!");
			Assert.AreEqual(classElement.ImplementedInterfaces, String.Empty, "Class implemented interfaces differs!");
			Assert.False(String.IsNullOrWhiteSpace(classElement.RawSource), "Class snippet is invalid!");

			var methodSearchResult = codeSearchResults.Find(el => el.Element.ProgramElementType == ProgramElementType.Method && el.Element.Name == "Parse");
			if(methodSearchResult == null)
			{
				Assert.Fail("Failed to find relevant search result for search: " + keywords);
			}
			var methodElement = methodSearchResult.Element as MethodElement;
			Assert.AreEqual(methodElement.AccessLevel, AccessLevel.Public, "Method access level differs!");
			Assert.AreEqual(methodElement.Arguments, "string extension", "Method arguments differs!");
			Assert.NotNull(methodElement.Body, "Method body is null!");
			Assert.True(methodElement.ClassId != null && methodElement.ClassId != Guid.Empty, "Class id is invalid!");
			Assert.AreEqual(methodElement.ClassName, "FileNameTemplate", "Method class name differs!");
			Assert.AreEqual(methodElement.DefinitionLineNumber, 17, "Method definition line number differs!");
			Assert.True(methodElement.FullFilePath.EndsWith("\\TestFiles\\StemmingTestFiles\\FileNameTemplate.cs"), "Method full file path is invalid!");
			Assert.AreEqual(methodElement.Name, "Parse", "Method name differs!");
			Assert.AreEqual(methodElement.ProgramElementType, ProgramElementType.Method, "Program element type differs!");
			Assert.AreEqual(methodElement.ReturnType, "ImagePairNames", "Method return type differs!");
			Assert.False(String.IsNullOrWhiteSpace(methodElement.RawSource), "Method snippet is invalid!");

			methodSearchResult = codeSearchResults.Find(el => el.Element.ProgramElementType == ProgramElementType.Method && el.Element.Name == "TryAddTemplatePrompt");
			if(methodSearchResult == null)
			{
				Assert.Fail("Failed to find relevant search result for search: " + keywords);
			}
			methodElement = methodSearchResult.Element as MethodElement;
			Assert.AreEqual(methodElement.AccessLevel, AccessLevel.Private, "Method access level differs!");
			Assert.AreEqual(methodElement.Arguments, "ImagePairNames startNames", "Method arguments differs!");
			Assert.NotNull(methodElement.Body, "Method body is null!");
			Assert.True(methodElement.ClassId != null && methodElement.ClassId != Guid.Empty, "Class id is invalid!");
            Assert.AreEqual(methodElement.ClassName, "FileNameTemplate", "Method class name differs!");
			Assert.AreEqual(methodElement.DefinitionLineNumber, 53, "Method definition line number differs!");
			Assert.True(methodElement.FullFilePath.EndsWith("\\TestFiles\\StemmingTestFiles\\FileNameTemplate.cs"), "Method full file path is invalid!");
			Assert.AreEqual(methodElement.Name, "TryAddTemplatePrompt", "Method name differs!");
			Assert.AreEqual(methodElement.ProgramElementType, ProgramElementType.Method, "Program element type differs!");
			//Assert.AreEqual(methodElement.ReturnType, "ImagePairNames", "Method return type differs!");
			Assert.False(String.IsNullOrWhiteSpace(methodElement.RawSource), "Method snippet is invalid!");

			var fieldSearchResult = codeSearchResults.Find(el => el.Element.ProgramElementType == ProgramElementType.Field && el.Element.Name == "fileName");
			if(fieldSearchResult == null)
			{
				Assert.Fail("Failed to find relevant search result for search: " + keywords);
			}
			var fieldElement = fieldSearchResult.Element as FieldElement;
			Assert.AreEqual(fieldElement.AccessLevel, AccessLevel.Private, "Field access level differs!");
			Assert.True(fieldElement.ClassId != null && methodElement.ClassId != Guid.Empty, "Class id is invalid!");
            Assert.AreEqual(fieldElement.ClassName, "FileNameTemplate", "Field class name differs!");
			Assert.AreEqual(fieldElement.DefinitionLineNumber, 12, "Field definition line number differs!");
			Assert.True(fieldElement.FullFilePath.EndsWith("\\TestFiles\\StemmingTestFiles\\FileNameTemplate.cs"), "Field full file path is invalid!");
			Assert.AreEqual(fieldElement.Name, "fileName", "Field name differs!");
			Assert.AreEqual(fieldElement.ProgramElementType, ProgramElementType.Field, "Program element type differs!");
			Assert.AreEqual(fieldElement.FieldType, "string", "Field return type differs!");
			Assert.False(String.IsNullOrWhiteSpace(methodElement.RawSource), "Field snippet is invalid!");
		}

		[TestFixtureSetUp]
		public void Setup()
		{
            TestUtils.InitializeDefaultExtensionPoints();
            ExtensionPointsRepository extensionPointsRepository = ExtensionPointsRepository.Instance;
            extensionPointsRepository.RegisterWordSplitterImplementation(new WordSplitter());
            extensionPointsRepository.RegisterQueryWeightsSupplierImplementation(new QueryWeightsSupplier());
            extensionPointsRepository.RegisterQueryRewriterImplementation(new DefaultQueryRewriter());
            var generator = new ABB.SrcML.SrcMLGenerator(@"LIBS\SrcML");
            extensionPointsRepository.RegisterParserImplementation(new List<string>() {".cs"}, new SrcMLCSharpParser(generator));
            extensionPointsRepository.RegisterParserImplementation(new List<string>() {".h", ".cpp", ".cxx"}, new SrcMLCppParser(generator));


			indexPath = Path.Combine(Path.GetTempPath(), "NamesWithNumbersSearchTest");
            assemblyPath = Path.Combine(Path.GetTempPath(), "assembly");
			Directory.CreateDirectory(indexPath);
            key = new SolutionKey(Guid.NewGuid(), "..\\..\\IntegrationTests\\TestFiles\\StemmingTestFiles", indexPath, assemblyPath);
            ServiceLocator.RegisterInstance(key);

            ServiceLocator.RegisterInstance<Analyzer>(new SnowballAnalyzer("English"));

            var indexer = new DocumentIndexer();
            ServiceLocator.RegisterInstance(indexer);
			monitor = new SolutionMonitor(new SolutionWrapper(), indexer, false);
			string[] files = Directory.GetFiles("..\\..\\IntegrationTests\\TestFiles\\StemmingTestFiles");

            SwumManager.Instance.Initialize(key.IndexPath, true);
            SwumManager.Instance.Generator = new ABB.SrcML.SrcMLGenerator("LIBS\\SrcML"); ;

			foreach(var file in files)
			{
				string fullPath = Path.GetFullPath(file);
				monitor.ProcessFileForTesting(fullPath);
			}
            monitor.UpdateAfterAdditions();
		}

		[TestFixtureTearDown]
		public void TearDown()
		{
            monitor.StopMonitoring(true);
			Directory.Delete(indexPath, true);
            //Directory.Delete(assemblyPath, true);
		}

		private string indexPath;
        private string assemblyPath;
		private static SolutionMonitor monitor;
		private static SolutionKey key;
	}
}
