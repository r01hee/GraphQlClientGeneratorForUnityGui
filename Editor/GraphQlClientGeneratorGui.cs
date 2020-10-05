using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;

using GraphQlClientGenerator;
using GraphQlClientUnity;

namespace GraphQlClientGeneratorGui
{
	public class GraphQlClientGeneratorGui : EditorWindow
	{
		static private readonly string aboutThisText = @"
 GraphQL Client Generator for Unity GUI
----------------------------------------

This tool is open-source-software released under the MIT License.

Project Page: https://github.com/r01hee/GraphQlClientGeneratorForUnityGui

---

This tool makes use of the following library.

# GraphQL C# client generator (https://github.com/Husqvik/GraphQlClientGenerator) v0.7.2

Copyright (c) 2017-2020 Jan Hucka

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the ""Software""), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
";

		static private string urlInput = "https://";

		static private string headerInput = "{\n  \"Content-Type\": \"application/json\"\n}";
		static private string namespaceInput = "GraphQlClient";
		static private string fileNameInput = "GraphQlClient";

		static private bool aboutThisFolding = false;

		private static IEnumerator enumeratorForGenerate = null;

		private Vector2 scrollPosition;

		private bool isGenerating
		{
			get
			{
				return enumeratorForGenerate != null;
			}
		}

		[MenuItem("Assets/Create/GraphQlClient C# Script", priority = 82)]
		private static void MenuItemGenerator()
		{
			var window = GetWindow<GraphQlClientGeneratorGui>();
			EditorApplication.update += Update;
			window.titleContent = new GUIContent("GraphQL Client Generator");
		}

		private void OnEnable()
		{
			EditorApplication.update += Update;
		}

		private static void Update()
		{
			if (enumeratorForGenerate != null)
			{
				try
				{
					if (!enumeratorForGenerate.MoveNext())
					{
						enumeratorForGenerate = null;
					}
				}
				catch (Exception ex)
				{
					enumeratorForGenerate = null;
					EditorUtility.DisplayDialog("Error", ex.ToString(), "OK");
					throw;
				}
			}
		}

		void OnGUI()
		{
			EditorGUI.BeginDisabledGroup(isGenerating);

			GUILayout.BeginHorizontal();
			GUILayout.Label("URL");
			urlInput = GUILayout.TextField(urlInput);
			GUILayout.EndHorizontal();

			GUILayout.Space(8);

			EditorGUILayout.LabelField("Header");
			headerInput = EditorGUILayout.TextArea(headerInput);
			var headers = ParseHeader(headerInput);

			EditorGUILayout.BeginVertical(GUI.skin.box);
			foreach (var h in headers)
			{
				EditorGUI.indentLevel++;
				GUILayout.BeginHorizontal();
				GUILayout.Label(h.Key);
				GUILayout.Label(h.Value);
				GUILayout.EndHorizontal();
			}
			EditorGUILayout.EndVertical();

			GUILayout.Space(8);

			GUILayout.BeginHorizontal();
			GUILayout.Label("Namespace");
			namespaceInput = GUILayout.TextField(namespaceInput);
			GUILayout.EndHorizontal();

			GUILayout.Space(8);

			GUILayout.BeginHorizontal();
			GUILayout.Label("File Name");
			fileNameInput = GUILayout.TextField(fileNameInput);
			GUILayout.Label(".cs", GUILayout.MaxWidth(20));
			GUILayout.EndHorizontal();

			GUILayout.Space(8);

			if (GUILayout.Button("Generate"))
			{

				var path = Application.dataPath;
				var selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
				if (selectedPath.Length != 0)
				{
					if (!IsFolder(selectedPath))
					{
						selectedPath = selectedPath.Substring(0, selectedPath.LastIndexOf("/", StringComparison.CurrentCulture));
					}
					path = path.Remove(path.Length - "Assets".Length, "Assets".Length);
					path += selectedPath;
				}

				path = Path.Combine(path, fileNameInput + ".cs");
				if (File.Exists(path) && !EditorUtility.DisplayDialog("Replace?", $"\"{path}\" already exists. Do you want to replace it?" + path, "Replace", "Cancel"))
				{
					return;
				}

				if (!string.IsNullOrEmpty(path))
				{
					StartToGenerate(urlInput, headers, namespaceInput, path);
				}
			}
			EditorGUI.EndDisabledGroup();

			GUILayout.Space(16);

			if (aboutThisFolding = EditorGUILayout.Foldout(aboutThisFolding, "About This Tool"))
			{
				scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
				GUILayout.TextArea(aboutThisText);
				EditorGUILayout.EndScrollView();
			}
		}

		private string MakeQueryJson(string query)
		{
			var espaced = query.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Replace("\"", "\\\"");
			return $"{{\"query\": \"{espaced}\"}}";
		}

		private void StartToGenerate(string url, Dictionary<string, string> headers, string @namespace, string path)
		{
			enumeratorForGenerate = Generate(url, headers, @namespace, path);
		}

		private IEnumerator Generate(string url, Dictionary<string, string> headers, string @namespace, string path)
		{
			return GraphQlClientUtils.Send(url, headers, MakeQueryJson(IntrospectionQuery.Text), json =>
			{
				Debug.Log(json);
				var schema = GraphQlGenerator.DeserializeGraphQlSchema(json);
				var csharpCode = new GraphQlGenerator().GenerateFullClientCSharpFile(schema, @namespace);
				File.WriteAllText(path, csharpCode);
				AssetDatabase.Refresh();

				EditorUtility.DisplayDialog("Sucess", $"Generated \"{path}\"", "OK");
			});
		}

		private static readonly Regex RegexParseHeader = new Regex(@"""(.+)""\s*:\s*""(.+)""");
		Dictionary<string, string> ParseHeader(string headerString)
		{
			var ret = new Dictionary<string, string>();
			var matches = RegexParseHeader.Matches(headerString.Replace(',', '\n'));
			foreach (Match m in matches)
			{
				var key = m.Groups[1].ToString();
				var value = m.Groups[2].ToString();
				ret[key] = value;
			}

			return ret;
		}

		private static bool IsFolder(string path)
		{
			try
			{
				return File.GetAttributes(path).Equals(FileAttributes.Directory);
			}
			catch (Exception ex)
			{
				if (ex.GetType() == typeof(FileNotFoundException))
				{
					return false;
				}
				throw;
			}
		}
	}
}