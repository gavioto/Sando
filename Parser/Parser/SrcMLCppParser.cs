﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Sando.Core.Tools;
using Sando.ExtensionContracts.ParserContracts;
using Sando.ExtensionContracts.ProgramElementContracts;
using System.Diagnostics.Contracts;

namespace Sando.Parser
{
	public class SrcMLCppParser : IParser
	{
		private readonly SrcMLGenerator Generator;

		private static readonly XNamespace SourceNamespace = "http://www.sdml.info/srcML/src";
		private static readonly XNamespace PositionNamespace = "http://www.sdml.info/srcML/position";
		private static readonly int SnippetSize = 5;
	    public static readonly string StandardSrcMlLocation = Environment.CurrentDirectory + "\\..\\..\\LIBS\\srcML-Win";

	    public SrcMLCppParser()
		{
			//try to set this up automatically			
			Generator = new SrcMLGenerator(LanguageEnum.CPP);	        
	        Generator.SetSrcMLLocation(StandardSrcMlLocation);
		}

		public SrcMLCppParser(SrcMLGenerator gen)
		{
			Generator = gen;
			Contract.Requires(gen.Language == LanguageEnum.CPP, "SrcMLCppParser is asked to parse a language other than C++");
		}

		public ProgramElement[] Parse(string fileName)
		{
			var programElements = new List<ProgramElement>();
			string srcml = Generator.GenerateSrcML(fileName);

			if(srcml != String.Empty)
			{
				XElement sourceElements = XElement.Parse(srcml);

				//classes have to parsed first
				ParseClasses(programElements, sourceElements, fileName);

				SrcMLParsingUtils.ParseEnums(programElements, sourceElements, fileName, SnippetSize);
				SrcMLParsingUtils.ParseFields(programElements, sourceElements, fileName, SnippetSize);
				ParseConstructors(programElements, sourceElements, fileName);
				ParseFunctions(programElements, sourceElements, fileName);
				ParseCppFunctionPrototypes(programElements, sourceElements, fileName);
				ParseCppConstructorPrototypes(programElements, sourceElements, fileName);
			}

			return programElements.ToArray();
		}

		private void ParseCppFunctionPrototypes(List<ProgramElement> programElements, XElement sourceElements, string fileName)
		{
			IEnumerable<XElement> functions =
				from el in sourceElements.Descendants(SourceNamespace + "function_decl")
				select el;
			ParseCppFunctionPrototype(programElements, functions, fileName, false);
		}

		private void ParseCppConstructorPrototypes(List<ProgramElement> programElements, XElement sourceElements, string fileName)
		{
			IEnumerable<XElement> functions =
				from el in sourceElements.Descendants(SourceNamespace + "constructor_decl")
				select el;
			ParseCppFunctionPrototype(programElements, functions, fileName, true);
		}

		private void ParseCppFunctionPrototype(List<ProgramElement> programElements, IEnumerable<XElement> functions, 
														string fileName, bool isConstructor)
		{
			foreach(XElement function in functions)
			{
				string name = String.Empty;
				int definitionLineNumber = 0;
				string returnType = String.Empty;

				SrcMLParsingUtils.ParseName(function, out name, out definitionLineNumber);
				AccessLevel accessLevel = RetrieveCppAccessLevel(function);
				XElement type = function.Element(SourceNamespace + "type");
				if(type != null)
				{
					XElement typeName = type.Element(SourceNamespace + "name");
					returnType = typeName.Value;
				}

				XElement paramlist = function.Element(SourceNamespace + "parameter_list");
				IEnumerable<XElement> argumentElements =
					from el in paramlist.Descendants(SourceNamespace + "name")
					select el;
				string arguments = String.Empty;
				foreach(XElement elem in argumentElements)
				{
					arguments += elem.Value + " ";
				}
				arguments = arguments.TrimEnd();

				string fullFilePath = System.IO.Path.GetFullPath(fileName);
				string snippet = SrcMLParsingUtils.RetrieveSnippet(fileName, definitionLineNumber, SnippetSize);

				programElements.Add(new MethodPrototypeElement(name, definitionLineNumber, returnType, accessLevel, arguments, fullFilePath, snippet, isConstructor));
			}
		}


		private string[] ParseCppIncludes(XElement sourceElements)
		{
			List<string> includeFileNames = new List<string>();
			XNamespace CppNamespace = "http://www.sdml.info/srcML/cpp";
			IEnumerable<XElement> includeStatements =
				from el in sourceElements.Descendants(CppNamespace + "include")
				select el;

			foreach(XElement include in includeStatements)
			{
				string filename = include.Element(CppNamespace + "file").Value;
				if(filename.Substring(0, 1) == "<") continue; //ignore includes of system files -> they start with a bracket
				filename = filename.Substring(1, filename.Length - 2);	//remove quotes	
				includeFileNames.Add(filename);
			}

			return includeFileNames.ToArray();
		}

		private void ParseClasses(List<ProgramElement> programElements, XElement elements, string fileName)
		{
			IEnumerable<XElement> classes =
				from el in elements.Descendants(SourceNamespace + "class")
				select el;
			foreach(XElement cls in classes)
			{
				programElements.Add(ParseClass(cls, fileName));
			}
		}

		private ClassElement ParseClass(XElement cls, string fileName)
		{
			string name;
			int definitionLineNumber;
			SrcMLParsingUtils.ParseName(cls, out name, out definitionLineNumber);

			AccessLevel accessLevel = AccessLevel.Public; //default
			XElement accessElement = cls.Element(SourceNamespace + "specifier");
			if(accessElement != null)
			{
				accessLevel = SrcMLParsingUtils.StrToAccessLevel(accessElement.Value);
			}

			//parse namespace
			IEnumerable<XElement> ownerNamespaces =
				from el in cls.Ancestors(SourceNamespace + "decl")
				where el.Element(SourceNamespace + "type").Element(SourceNamespace + "name").Value == "namespace"
				select el;
			string namespaceName = String.Empty;
			foreach(XElement ownerNamespace in ownerNamespaces)
			{
				foreach(XElement spc in ownerNamespace.Elements(SourceNamespace + "name"))
				{
					namespaceName += spc.Value + " ";
				}
			}
			namespaceName = namespaceName.TrimEnd();

			//parse extended classes and implemented interfaces (interfaces are treated as extended classes in SrcML for now)
			string extendedClasses = String.Empty;
			XElement super = cls.Element(SourceNamespace + "super");
			if(super != null)
			{
				XElement implements = super.Element(SourceNamespace + "implements");
				if(implements != null)
				{
					IEnumerable<XElement> impNames =
						from el in implements.Descendants(SourceNamespace + "name")
						select el;
					foreach(XElement impName in impNames)
					{
						extendedClasses += impName.Value + " ";
					}
					extendedClasses = extendedClasses.TrimEnd();
				}
			}
			//interfaces are treated as extended classes in SrcML for now
			string implementedInterfaces = String.Empty;

			string fullFilePath = System.IO.Path.GetFullPath(fileName);
			string snippet = SrcMLParsingUtils.RetrieveSnippet(fileName, definitionLineNumber, SnippetSize);

			return new ClassElement(name, definitionLineNumber, fullFilePath, snippet, accessLevel, namespaceName, extendedClasses, implementedInterfaces, String.Empty);
		}

		private void ParseConstructors(List<ProgramElement> programElements, XElement elements, string fileName)
		{
			string[] includedFiles = ParseCppIncludes(elements);
			IEnumerable<XElement> constructors =
				from el in elements.Descendants(SourceNamespace + "constructor")
				select el;
			foreach(XElement cons in constructors)
			{
				MethodElement methodElement = null;
				methodElement = ParseCppFunction(cons, programElements, fileName, includedFiles, true);
				programElements.Add(methodElement);
				DocCommentElement methodCommentsElement = SrcMLParsingUtils.ParseFunctionComments(cons, methodElement);
				if(methodCommentsElement != null)
				{
					programElements.Add(methodCommentsElement);
				}
			}
		}

		private void ParseFunctions(List<ProgramElement> programElements, XElement elements, string fileName)
		{
			string[] includedFiles = ParseCppIncludes(elements);
			IEnumerable<XElement> functions =
				from el in elements.Descendants(SourceNamespace + "function")
				select el;
			foreach(XElement func in functions)
			{
				MethodElement methodElement = null;
				methodElement = ParseCppFunction(func, programElements, fileName, includedFiles);
				programElements.Add(methodElement);
				DocCommentElement methodCommentsElement = SrcMLParsingUtils.ParseFunctionComments(func, methodElement);
				if(methodCommentsElement != null)
				{
					programElements.Add(methodCommentsElement);
				}
			}
		}

		private MethodElement ParseCppFunction(XElement function, List<ProgramElement> programElements, string fileName, 
												string[] includedFiles, bool isConstructor = false)
		{
			MethodElement methodElement = null;
			string snippet = String.Empty;
			int definitionLineNumber = 0;
			string returnType = String.Empty;

			XElement type = function.Element(SourceNamespace + "type");
			if(type != null)
			{
				XElement typeName = type.Element(SourceNamespace + "name");
				returnType = typeName.Value;
			}

			XElement paramlist = function.Element(SourceNamespace + "parameter_list");
			IEnumerable<XElement> argumentElements =
				from el in paramlist.Descendants(SourceNamespace + "name")
				select el;
			string arguments = String.Empty;
			foreach(XElement elem in argumentElements)
			{
				arguments += elem.Value + " ";
			}
			arguments = arguments.TrimEnd();

			string body = SrcMLParsingUtils.ParseBody(function);
			string fullFilePath = System.IO.Path.GetFullPath(fileName);


			XElement nameElement = function.Element(SourceNamespace + "name");
			string wholeName = nameElement.Value;
			if(wholeName.Contains("::"))
			{
				//class function
				string[] twonames = wholeName.Split("::".ToCharArray());
				string funcName = twonames[2];
				string className = twonames[0];
				definitionLineNumber = Int32.Parse(nameElement.Element(SourceNamespace + "name").Attribute(PositionNamespace + "line").Value);
				snippet = SrcMLParsingUtils.RetrieveSnippet(fileName, definitionLineNumber, SnippetSize);

				return new CppUnresolvedMethodElement(funcName, definitionLineNumber, fullFilePath, snippet, arguments, returnType, body, 
														className, isConstructor, includedFiles);
			}
			else
			{
				//regular C-type function, or an inlined class function
				string funcName = wholeName;
				definitionLineNumber = Int32.Parse(nameElement.Attribute(PositionNamespace + "line").Value);
				snippet = SrcMLParsingUtils.RetrieveSnippet(fileName, definitionLineNumber, SnippetSize);
				AccessLevel accessLevel = RetrieveCppAccessLevel(function);

				ClassElement classElement = SrcMLParsingUtils.RetrieveClassElement(function, programElements);
				Guid classId = classElement != null ? classElement.Id : Guid.Empty;
				string className = classElement != null ? classElement.Name : String.Empty;

				methodElement = new MethodElement(funcName, definitionLineNumber, fullFilePath, snippet, accessLevel, arguments, returnType, body, 
													classId, className, String.Empty, isConstructor);
			}

			return methodElement;
		}

		
		private AccessLevel RetrieveCppAccessLevel(XElement field)
		{
			AccessLevel accessLevel = AccessLevel.Protected;

			XElement parent = field.Parent;
			if(parent.Name == (SourceNamespace + "public"))
			{
				accessLevel = AccessLevel.Public;
			}
			else if(parent.Name == (SourceNamespace + "private"))
			{
				accessLevel = AccessLevel.Private;
			}

			return accessLevel;
		}
	}
}
