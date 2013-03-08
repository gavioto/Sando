﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Threading;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Sando.Core;
using Sando.Core.Extensions.Logging;
using Sando.DependencyInjection;
using Sando.Indexer.Documents;
using Sando.Indexer.Exceptions;
using Sando.Translation;
using System.Linq;
using Sando.Indexer.Documents.Converters;
using ABB.SrcML.VisualStudio.SolutionMonitor;
using Sando.Core.Tools;

namespace Sando.Indexer
{
	public class DocumentIndexer : IDisposable
	{
        public DocumentIndexer(TimeSpan? refreshIndexSearcherThreadInterval = null, TimeSpan? commitChangesThreadInterval = null )
		{
			try
			{
                var solutionKey = ServiceLocator.Resolve<SolutionKey>();			
                var directoryInfo = new System.IO.DirectoryInfo(PathManager.Instance.GetIndexPath(solutionKey));
				LuceneIndexesDirectory = FSDirectory.Open(directoryInfo);
				Analyzer = ServiceLocator.Resolve<Analyzer>();
                IndexWriter = new IndexWriter(LuceneIndexesDirectory, Analyzer, IndexWriter.MaxFieldLength.LIMITED);
			    var indexReader = IndexWriter.GetReader();
				_indexSearcher = new IndexSearcher(indexReader);
                QueryParser = new QueryParser(Lucene.Net.Util.Version.LUCENE_29, Configuration.Configuration.GetValue("DefaultSearchFieldName"), Analyzer);

			    if (!refreshIndexSearcherThreadInterval.HasValue) 
                    refreshIndexSearcherThreadInterval = TimeSpan.FromSeconds(10);
			    var refreshIndexSearcherBackgroundWorker = new BackgroundWorker {WorkerReportsProgress = false, WorkerSupportsCancellation = false};
                refreshIndexSearcherBackgroundWorker.DoWork += PeriodicallyRefreshIndexSearcherIfNeeded;
                refreshIndexSearcherBackgroundWorker.RunWorkerAsync(refreshIndexSearcherThreadInterval);

                if (commitChangesThreadInterval.HasValue)
			    {
			        var commitChangesBackgroundWorker = new BackgroundWorker
			            {
			                WorkerReportsProgress = false,
			                WorkerSupportsCancellation = false
			            };
			        commitChangesBackgroundWorker.DoWork += PeriodicallyCommitChangesIfNeeded;
			        commitChangesBackgroundWorker.RunWorkerAsync(commitChangesThreadInterval);
			    }
                else
                {
                    _synchronousCommits = true;
                }
			}
			catch(CorruptIndexException corruptIndexEx)
			{
                FileLogger.DefaultLogger.Error(ExceptionFormatter.CreateMessage(corruptIndexEx));
				throw new IndexerException(TranslationCode.Exception_Indexer_LuceneIndexIsCorrupt, corruptIndexEx);
			}
			catch(LockObtainFailedException lockObtainFailedEx)
			{
                FileLogger.DefaultLogger.Error(ExceptionFormatter.CreateMessage(lockObtainFailedEx));
				throw new IndexerException(TranslationCode.Exception_Indexer_LuceneIndexAlreadyOpened, lockObtainFailedEx);
			}
			catch(System.IO.IOException ioEx)
			{
                FileLogger.DefaultLogger.Error(ExceptionFormatter.CreateMessage(ioEx));
				throw new IndexerException(TranslationCode.Exception_General_IOException, ioEx, ioEx.Message);
			}
		}

        public virtual void AddDocument(SandoDocument sandoDocument)
		{
			Contract.Requires(sandoDocument != null, "DocumentIndexer:AddDocument - sandoDocument cannot be null!");

            lock (_lock)
            {
                IndexWriter.AddDocument(sandoDocument.GetDocument());
                if(_synchronousCommits)
                    CommitChanges();
                else
                    _hasIndexChanged = true;
            }
		}

        public virtual void DeleteDocuments(string fullFilePath, bool commitImmediately = false)
        {
            if (String.IsNullOrWhiteSpace(fullFilePath))
                return;
            var term = new Term("FullFilePath", ConverterFromHitToProgramElement.StandardizeFilePath(fullFilePath));
            lock (_lock)
            {
                IndexWriter.DeleteDocuments(new TermQuery(term));
                if (_synchronousCommits || commitImmediately)
                    CommitChanges();
                else
                    _hasIndexChanged = true;
            }
        }

		public void ClearIndex()
		{
		    lock (_lock)
		    {
		        IndexWriter.GetDirectory().EnsureOpen();
		        IndexWriter.DeleteAll();
                CommitChanges();
		    }
		}

        public void ForceReaderRefresh()
        {
            lock (_lock)
            {
                UpdateSearcher();
            }
        }

        public List<Tuple<Document, float>> Search(Query query, TopScoreDocCollector collector)
        {
            lock (_lock)
            {
                try
                {
                    return RunSearch(query, collector);
                }
                catch (AlreadyClosedException)
                {
                    UpdateSearcher();
                    return RunSearch(query, collector);
                }
            }
        }

        public int GetNumberOfIndexedDocuments()
        {
            return _indexSearcher.GetIndexReader().NumDocs();
        }

	    private List<Tuple<Document, float>> RunSearch(Query query, TopScoreDocCollector collector)
	    {
	        _indexSearcher.Search(query, collector);

	        var hits = collector.TopDocs().ScoreDocs;
	        var documents =
	            hits.AsEnumerable().Select(h => new Tuple<Document, float>(_indexSearcher.Doc(h.doc), h.score)).ToList();
	        return documents;
	    }

        private void CommitChanges()
        {
            IndexWriter.Commit();
            UpdateSearcher();
            _hasIndexChanged = false;
        }

		private void UpdateSearcher()
		{
		    try
		    {
		        var oldReader = _indexSearcher.GetIndexReader();
		        var newReader = oldReader.Reopen(true);
		        if (newReader != oldReader)
		        {
		            //_indexSearcher.Close(); - don't need this, because we create IndexSearcher by passing the IndexReader to it, so Close do nothing
		            oldReader.Close();
		            _indexSearcher = new IndexSearcher(newReader);
		        }
		    }
            catch (AlreadyClosedException)
		    {
		        var indexReader = IndexWriter.GetReader();
                _indexSearcher = new IndexSearcher(indexReader);
		    }
		}

        private void PeriodicallyCommitChangesIfNeeded(object sender, DoWorkEventArgs args)
        {
            var backgroundThreadInterval = args.Argument;	        
            while (!_disposed)
            {
                lock (_lock)
                {
                    if (_hasIndexChanged)
                        CommitChanges();
                }
                Thread.Sleep(Convert.ToInt32(((TimeSpan)backgroundThreadInterval).TotalMilliseconds));
            }
        }

	    private void PeriodicallyRefreshIndexSearcherIfNeeded(object sender, DoWorkEventArgs args)
	    {
            var backgroundThreadInterval = args.Argument;	        
	        while (!_disposed)
	        {
	            lock (_lock)
	            {
	                if (!IsUsable())
	                {
	                    UpdateSearcher();
	                }
	            }
                Thread.Sleep(Convert.ToInt32(((TimeSpan)backgroundThreadInterval).TotalMilliseconds));
	        }
	    }

	    private bool IsUsable()
        {
            try
            {
                _indexSearcher.Search(new TermQuery(new Term("asdf")), 1);
            }
            catch (AlreadyClosedException)
            {
                return false;
            }
            return true;
        }

        public void NUnit_CloseIndexSearcher()
        {
            _indexSearcher.GetIndexReader().Close();
        }

        public void Dispose()
        {
            Dispose(false);
        }


		public void Dispose(bool killReaders)
        {
		    lock (_lock)
		    {
                try
                {
                    CommitChanges();
                }
                catch (AlreadyClosedException)
                {
                    //This is expected in some cases
                }
		        Dispose(true, killReaders);
		        GC.SuppressFinalize(this);
		    }
        }
		
		protected virtual void Dispose(bool disposing, bool killReaders)
        {
            if(!_disposed)
            {
                if(disposing)
                {
                    IndexWriter.Close();
					IndexReader indexReader = _indexSearcher.GetIndexReader();
                    if(indexReader != null)
                        indexReader.Close();
					_indexSearcher.Close();
					LuceneIndexesDirectory.Close();
                    try
                    {
                        Analyzer.Close();
                    }
                    catch (NullReferenceException)
                    {
                        //already closed, ignore
                    }
                }

                _disposed = true;
            }
        }

	    public Directory LuceneIndexesDirectory { get; set; }
		public QueryParser QueryParser { get; protected set; }
		protected Analyzer Analyzer { get; set; }
		protected IndexWriter IndexWriter { get; set; }

        private bool _hasIndexChanged;
        private bool _disposed;
	    private IndexSearcher _indexSearcher;
        private readonly bool _synchronousCommits;
	    private readonly object _lock = new object();
	}
}
