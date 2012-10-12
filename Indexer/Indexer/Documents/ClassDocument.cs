﻿using System;
using Lucene.Net.Documents;
using Sando.ExtensionContracts.ProgramElementContracts;

namespace Sando.Indexer.Documents
{
	public class ClassDocument : SandoDocument
	{
		public ClassDocument(ClassElement classElement)
			: base(classElement)
		{
		}

		public ClassDocument(Document document)
			: base(document)
		{
		}

		protected override void AddDocumentFields()
		{
			ClassElement classElement = (ClassElement) programElement;
			document.Add(new Field(SandoField.Namespace.ToString(), classElement.Namespace.ToSandoSearchable(), Field.Store.YES, Field.Index.ANALYZED));
            document.Add(new Field(SandoField.Body.ToString(), classElement.Body.ToSandoSearchable(), Field.Store.NO, Field.Index.ANALYZED));
			document.Add(new Field(SandoField.AccessLevel.ToString(), classElement.AccessLevel.ToString().ToLower(), Field.Store.YES, Field.Index.NOT_ANALYZED));
			document.Add(new Field(SandoField.ExtendedClasses.ToString(), classElement.ExtendedClasses.ToSandoSearchable(), Field.Store.YES, Field.Index.ANALYZED));
			document.Add(new Field(SandoField.ImplementedInterfaces.ToString(), classElement.ImplementedInterfaces.ToSandoSearchable(), Field.Store.YES, Field.Index.ANALYZED));
			document.Add(new Field(SandoField.Modifiers.ToString(), classElement.Modifiers, Field.Store.YES, Field.Index.ANALYZED));
		}

		protected override ProgramElement ReadProgramElementFromDocument(string name, ProgramElementType programElementType, string fullFilePath, int definitionLineNumber, string snippet, Document document)
		{
			string namespaceName = document.GetField(SandoField.Namespace.ToString()).StringValue().ToSandoDisplayable();
			AccessLevel accessLevel = (AccessLevel)Enum.Parse(typeof(AccessLevel), document.GetField(SandoField.AccessLevel.ToString()).StringValue(), true);
			string extendedClasses = document.GetField(SandoField.ExtendedClasses.ToString()).StringValue().ToSandoDisplayable();
			string implementedInterfaces = document.GetField(SandoField.ImplementedInterfaces.ToString()).StringValue().ToSandoDisplayable();
			string modifiers = document.GetField(SandoField.Modifiers.ToString()).StringValue();
            return base.ReadProgramElementFromDocument(GetMyType(), new object[]{name, definitionLineNumber, fullFilePath, snippet, accessLevel, namespaceName, extendedClasses, implementedInterfaces, modifiers, ""});
		}
	}
}
