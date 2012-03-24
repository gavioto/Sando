﻿using System;
using System.IO;
using NUnit.Framework;
using Sando.Core;
using Sando.Indexer;
using Sando.UI.Monitoring;
using System.Threading;
using System.ComponentModel;
using Sando.Indexer.IndexState;
using System.Collections.Generic;

namespace Sando.UI.UnitTests.Monitoring
{
	[TestFixture]
	public class IndexUpdateManagerTest
	{
		[Test]
		public void IndexUpdateManager_WorksNormallyWith50Threads()
		{
			IndexFilesStatesManager indexFilesStatesManager = new IndexFilesStatesManager(indexPath);
			indexFilesStatesManager.ReadIndexFilesStates();
			string filePath = null;
			for(int i = 0; i < nrOfDifferentFiles; ++i)
			{
				filePath = Path.Combine(solutionPath, "Class" + i + ".cs");
				Assert.IsNull(indexFilesStatesManager.GetIndexFileState(filePath), "File state is not null for Class" + i);
			}

			HashSet<int> randomNumbers = new HashSet<int>();
			BackgroundWorker backgroundWorker = null;
			int randomNumber = 0;
			for(int i = 0; i < nrOfWorkers; ++i)
			{
				backgroundWorker = new BackgroundWorker();
				backgroundWorker.DoWork += new DoWorkEventHandler(backgroundWorker_DoWork);
				randomNumber = new Random().Next(nrOfDifferentFiles);
				randomNumbers.Add(randomNumber);
				backgroundWorker.RunWorkerAsync(randomNumber);
			}
			do
			{
				Thread.Sleep(10);
			}
			while(executionCounter < nrOfWorkers || workerFailed);
			if(workerFailed)
				Assert.Fail("One of the workers failed");

			indexUpdateManager.SaveFileStates();

			indexFilesStatesManager = new IndexFilesStatesManager(indexPath);
			indexFilesStatesManager.ReadIndexFilesStates(); 
			foreach(int i in randomNumbers)
			{
				filePath = Path.Combine(solutionPath, "Class" + i + ".cs");
				Assert.NotNull(indexFilesStatesManager.GetIndexFileState(filePath), "File state is null for Class" + i);
			}
		}

		void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			try
			{
				Thread.Sleep(new Random().Next(100));
				string filePath = Path.Combine(solutionPath, "Class" + (int)e.Argument + ".cs");
				indexUpdateManager.UpdateFile(filePath);
				++executionCounter;
			}
			catch
			{
				workerFailed = true;
			}
		}

		[SetUp]
		public void PrepareIndexUpdateManager()
		{
			solutionPath = Path.Combine(Path.GetTempPath(), "solution");
			indexPath = Path.Combine(solutionPath, "luceneindex");
			PrepareFileSystemObjects();
			solutionKey = new SolutionKey(Guid.NewGuid(), solutionPath, indexPath);
			documentIndexer = DocumentIndexerFactory.CreateIndexer(solutionKey, AnalyzerType.Default);
			indexUpdateManager = new IndexUpdateManager(solutionKey, documentIndexer);
			executionCounter = 0;
		}

		private void PrepareFileSystemObjects()
		{
			if(!Directory.Exists(solutionPath))
				Directory.CreateDirectory(solutionPath);
			if(!Directory.Exists(indexPath))
				Directory.CreateDirectory(indexPath);
			string filePath = null;
			for(int i = 0; i < nrOfDifferentFiles; ++i)
			{
				filePath = Path.Combine(solutionPath, "Class" + i + ".cs");
				if(!File.Exists(filePath))
				{
					FileStream fileStream = File.Create(filePath);
					fileStream.Close();
				}
					
			}
		}

		[TearDown]
		public void ClearResources()
		{
			documentIndexer.Dispose();
			if(Directory.Exists(solutionPath))
				Directory.Delete(solutionPath, true);
		}

		private int executionCounter;
		private string solutionPath;
		private string indexPath;
		private SolutionKey solutionKey;
		private DocumentIndexer documentIndexer;
		private IndexUpdateManager indexUpdateManager;
		private bool workerFailed;
		private int nrOfWorkers = 50;
		private int nrOfDifferentFiles = 50;
	}
}
