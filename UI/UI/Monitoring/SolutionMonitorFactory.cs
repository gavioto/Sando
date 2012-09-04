using System;
using System.Diagnostics.Contracts;
using System.IO;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Sando.Core;
using Sando.Indexer;

namespace Sando.UI.Monitoring
{
	class SolutionMonitorFactory
	{
		private const string Lucene = "\\lucene";

	    public static string LuceneDirectory { get; set; }


		public static SolutionMonitor CreateMonitor(bool isIndexRecreationRequired)
		{
			var openSolution = UIPackage.GetOpenSolution();
			return CreateMonitor(openSolution, isIndexRecreationRequired);
		}

		private static SolutionMonitor CreateMonitor(Solution openSolution, bool isIndexRecreationRequired)
		{
			Contract.Requires(openSolution != null, "A solution must be open");

			//TODO if solution is reopen - the guid should be read from file - future change
			SolutionKey solutionKey = new SolutionKey(Guid.NewGuid(), openSolution.FileName, GetLuceneDirectoryForSolution(openSolution));
			var currentIndexer = DocumentIndexerFactory.CreateIndexer(solutionKey, AnalyzerType.Snowball);
			if(isIndexRecreationRequired)
			{
				currentIndexer.DeleteDocuments("*");
				currentIndexer.CommitChanges();
			}
			var currentMonitor = new SolutionMonitor(SolutionWrapper.Create(openSolution), solutionKey, currentIndexer, isIndexRecreationRequired);			
			return currentMonitor;
		}

		private static string CreateLuceneFolder()
		{
            Contract.Requires(LuceneDirectory != null, "Please set the LuceneDirectory before calling this method");
			return CreateFolder(Lucene, LuceneDirectory);
		}

		private static string CreateFolder(string folderName, string parentDirectory)
		{
			if (!File.Exists(parentDirectory + folderName))
			{
				var directoryInfo = Directory.CreateDirectory(parentDirectory + folderName);
				return directoryInfo.FullName;
			}
			else
			{
                return parentDirectory + folderName;
			}
		}

		private static string GetName(Solution openSolution)
		{
			var fullName = openSolution.FullName;
			var split = fullName.Split('\\');
			return split[split.Length - 1]+fullName.GetHashCode();
		}



		private static string GetLuceneDirectoryForSolution(Solution openSolution)
		{
		    var luceneFolder = CreateLuceneFolder();
            CreateFolder(GetName(openSolution), luceneFolder + "\\");
			return luceneFolder + "\\" + GetName(openSolution);
		}
	}
}