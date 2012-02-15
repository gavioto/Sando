﻿using System.IO;

namespace Sando.SearchEngine
{
    using Sando.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;    

    /// <summary>
    /// Class defined to create return result from Lucene indexer
    /// </summary>
   public class CodeSearchResult
   {
       #region Public Properties
       /// <summary>
       /// Gets or sets the score.
       /// </summary>
       /// <value>
       /// The search score.
       /// </value>
       public double Score { get; private set; }

       /// <summary>
       /// Gets or sets the element.
       /// </summary>
       /// <value>
       /// Sando Program Element.
       /// </value>
       public ProgramElement Element
       {
           get;
           private set;
       }

    	public string FileName
    	{
    		get
    		{
    			var fileName = Path.GetFileName(Element.FullFilePath);
    			return fileName;
    		}    		
    	}

    	public string Parent
    	{
    		get
    		{
    			var method = Element as MethodElement;
				if(method !=null)
				{
					return method.ClassId.ToString().Substring(0,5);
				}else
				{
					return "";
				}

    		}
    	}
       #endregion
       #region Constructor
       /// <summary>
        /// Initializes a new instance of the <see cref="CodeSearchResult"/> class.
        /// </summary>
        /// <param name="programElement">program element.</param>
        /// <param name="score">search score.</param>
	   public CodeSearchResult(ProgramElement programElement, double score)
	   {
		   this.Element = programElement;
		   this.Score = score;
	   }
       #endregion
       
    }
}
