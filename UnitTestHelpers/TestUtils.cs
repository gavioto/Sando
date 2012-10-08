﻿using System.Collections.Generic;
using System.IO;
using Sando.Core.Extensions;
using Sando.Core.Tools;
using Sando.Indexer.Searching;
using Sando.Parser;
using Sando.SearchEngine;

namespace UnitTestHelpers
{
    public class TestUtils
    {
        public static void ClearDirectory(string dir)
        {
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }

		public static void InitializeDefaultExtensionPoints()
		{
			ExtensionPointsRepository extensionPointsRepository = ExtensionPointsRepository.GetInstance();
			extensionPointsRepository.RegisterParserImplementation(new List<string>() { ".cs" }, new SrcMLCSharpParser());
			extensionPointsRepository.RegisterParserImplementation(new List<string>() { ".h", ".cpp", ".cxx" }, new SrcMLCppParser());

			extensionPointsRepository.RegisterWordSplitterImplementation(new WordSplitter());

			extensionPointsRepository.RegisterResultsReordererImplementation(new SortByScoreResultsReorderer());

			extensionPointsRepository.RegisterQueryWeightsSupplierImplementation(new QueryWeightsSupplier());

			extensionPointsRepository.RegisterQueryRewriterImplementation(new DefaultQueryRewriter());
		}
    }
}
