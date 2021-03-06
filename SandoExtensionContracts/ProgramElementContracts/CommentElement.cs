﻿using System;
using System.Diagnostics.Contracts;

namespace Sando.ExtensionContracts.ProgramElementContracts
{
	public class CommentElement : ProgramElement
	{
        public CommentElement(string name, int definitionLineNumber, int definitionColumnNumber, string fullFilePath, string snippet, string body) 
			: base(name, definitionLineNumber, definitionColumnNumber,  fullFilePath, snippet)
		{
			Contract.Requires(!String.IsNullOrWhiteSpace(body), "CommentElement:Constructor - body cannot be null or an empty string!");
			
			Body = body;
		}

		public virtual string Body { get; private set; }
		public override ProgramElementType ProgramElementType { get { return ProgramElementType.Comment; } }
	}
}
