﻿using System;
using Lucene.Net.Documents;
using Sando.Core;

namespace Sando.Indexer.Documents
{
	public class PropertyDocument : SandoDocument
	{
		public static PropertyDocument Create(PropertyElement propertyElement)
		{
			if(propertyElement == null)
				throw new ArgumentException("PropertyElement cannot be null");
			if(propertyElement.ClassId == null)
				throw new ArgumentException("Class id cannot be null");
			if(propertyElement.Id == null)
				throw new ArgumentException("Property id cannot be null");
			if(String.IsNullOrWhiteSpace(propertyElement.Name))
				throw new ArgumentException("Property name cannot be null");
			if(String.IsNullOrWhiteSpace(propertyElement.PropertyType))
				throw new ArgumentException("Property type cannot be null");

			return new PropertyDocument(propertyElement);
		}

		protected override void CreateDocument()
		{
			document = new Document();
			PropertyElement propertyElement = (PropertyElement) programElement;
			document.Add(new Field("Id", propertyElement.Id.ToString(), Field.Store.YES, Field.Index.NO));
			document.Add(new Field("Name", propertyElement.Name, Field.Store.NO, Field.Index.ANALYZED));
			document.Add(new Field("AccessLevel", propertyElement.AccessLevel.ToString(), Field.Store.NO, Field.Index.NOT_ANALYZED));
			document.Add(new Field("DefinitionLineNumber", propertyElement.DefinitionLineNumber.ToString(), Field.Store.YES, Field.Index.NO));
			document.Add(new Field("Body", propertyElement.Body ?? String.Empty, Field.Store.NO, Field.Index.ANALYZED));
			document.Add(new Field("PropertyType", propertyElement.PropertyType, Field.Store.NO, Field.Index.ANALYZED));
			document.Add(new Field("ClassId", propertyElement.ClassId.ToString(), Field.Store.NO, Field.Index.NOT_ANALYZED));
		}

		private PropertyDocument(PropertyElement propertyElement)
			: base(propertyElement)
		{	
		}
	}
}
