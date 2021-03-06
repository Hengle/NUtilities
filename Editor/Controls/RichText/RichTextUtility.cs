﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Vertx.Extensions;
using static Vertx.RichTextParser;

namespace Vertx.Extensions
{
	public static class RichTextUtility
	{
		/// <summary>
		/// Gets content surrounded by the appropriate button tag
		/// </summary>
		/// <param name="type">The type to link to (IButtonRegistry provided to AddRichText must contain the type's FullName as a key)</param>
		/// <param name="label">The label of the button</param>
		/// <returns>a rich text string describing the provided button</returns>
		public static string GetButtonString(Type type, string label) => $"<button={type.FullName}>{label}</button>";
		
		/// <summary>
		/// Gets content surrounded by the appropriate button tag
		/// </summary>
		/// <param name="key">The key to link to (IButtonRegistry provided to AddRichText must contain key)</param>
		/// <param name="label">The label of the button</param>
		/// <returns>a rich text string describing the provided button</returns>
		public static string GetButtonString(string key, string label) => $"<button={key}>{label}</button>";
		
		/// <summary>
		/// Gets text content 
		/// </summary>
		/// <param name="content"></param>
		/// <param name="colour"></param>
		/// <returns></returns>
		public static string GetColouredString(string content, Color colour) => $"<color=#{ColorUtility.ToHtmlStringRGBA(colour)}>{content}</color>";
		public static string GetColoredString(string content, Color color) => GetColouredString(content, color);
		public static string GetBoldItalicsString(string content) => $"<i><b>{content}</b></i>";

		/// <summary>
		/// Adds VisualElements corresponding to the provided rich text to a root.
		/// </summary>
		/// <param name="text">The rich text to parse</param>
		/// <param name="buttonRegistry">A registry that can be queried for keys if there are any button tags.</param>
		/// <param name="root">Visual Element to append the rich text UI to.</param>
		/// <returns>A list of all immediate children added to the root.</returns>
		public static List<VisualElement> AddRichText(string text, IButtonRegistry buttonRegistry, VisualElement root) => AddRichText(text, buttonRegistry, root, false);

		public static List<VisualElement> AddRichText(string text, IButtonRegistry buttonRegistry, VisualElement root, bool isInsideCodeBlock)
		{
			List<VisualElement> results = new List<VisualElement>();
			IEnumerable<RichText> richTexts = ParseRichText(text, isInsideCodeBlock);
			//Parse rich texts to create paragraphs.
			List<List<RichText>> paragraphs = new List<List<RichText>> {new List<RichText>()};
			foreach (RichText richText in richTexts)
			{
				if (richText.richTextTag.tag == RichTextTag.Tag.button || richText.richTextTag.tag == RichTextTag.Tag.code)
				{
					paragraphs[paragraphs.Count - 1].Add(richText);
					continue;
				}

				string[] strings = richText.associatedText.Split('\n');
				for (int i = 0; i < strings.Length; i++)
				{
					if (i != 0)
						paragraphs.Add(new List<RichText>());
					//Split paragraph content (already split by tag) into individual words 
					string[] wordSplit = Regex.Split(strings[i], @"(?<=[ -])"); //Split but keep delimiters attached.
					foreach (var word in wordSplit)
					{
						if (!string.IsNullOrEmpty(word))
							paragraphs[paragraphs.Count - 1].Add(new RichText(richText.richTextTag, word));
					}
				}
			}

			foreach (List<RichText> paragraph in paragraphs)
			{
				//Add all the paragraphs
				VisualElement rootTemp = root;
				root = AddParagraphContainer(root);
				for (int i = 0; i < paragraph.Count; i++)
				{
					RichText word = paragraph[i];
					if (i < paragraph.Count - 1)
					{
						//If there are more words 
						RichText nextWord = paragraph[i + 1];
						string nextText = nextWord.associatedText;
						if (Regex.IsMatch(nextText, "^[^a-zA-Z] ?"))
						{
							VisualElement inlineGroup = new VisualElement();
							root.Add(inlineGroup);
							inlineGroup.AddToClassList("inline-text-group");
							AddRichTextInternal(word, inlineGroup);
							AddRichTextInternal(nextWord, inlineGroup);
							++i;
							continue;
						}
					}

					AddRichTextInternal(word, root);

					//Add all the words and style them.
					void AddRichTextInternal(RichText richText, VisualElement rootToAddTo)
					{
						RichTextTag tag = richText.richTextTag;
						TextElement inlineText = null;
						switch (tag.tag)
						{
							case RichTextTag.Tag.none:
								inlineText = AddInlineText(richText.associatedText, rootToAddTo);
								break;
							case RichTextTag.Tag.button:
								if (buttonRegistry == null)
								{
									Debug.LogWarning("There was no ButtonRegistry provided to AddRichText. Button tags will not function.");
									inlineText = AddInlineButton(() => Debug.LogWarning("There was no ButtonRegistry provided to AddRichText. Button tags will not function."), richText.associatedText, rootToAddTo);
									break;
								}
								if (!buttonRegistry.GetRegisteredButtonAction(tag.stringVariables, out Action action))
									return;
								inlineText = AddInlineButton(action, richText.associatedText, rootToAddTo);
								break;
							case RichTextTag.Tag.code:
								//Scroll
								ScrollView codeScroll = new ScrollView(ScrollViewMode.Horizontal);
								VisualElement contentContainer = codeScroll.contentContainer;
								codeScroll.contentViewport.style.flexDirection = FlexDirection.Column;
								codeScroll.contentViewport.style.alignItems = Align.Stretch;
								codeScroll.AddToClassList("code-scroll");
								root.Add(codeScroll);

								contentContainer.ClearClassList();
								contentContainer.AddToClassList("code-container");
								VisualElement codeContainer = contentContainer;
								
								CSharpHighlighter highlighter = new CSharpHighlighter
								{
									AddStyleDefinition = false
								};
								// To add code, we first use the CSharpHighlighter to construct rich text for us.
								string highlit = highlighter.Highlight(richText.associatedText);
								// After constructing new rich text we pass the text back recursively through this function with the new parent.
								AddRichText(highlit, buttonRegistry, codeContainer, true); // only parse spans because this is all the CSharpHighlighter parses.
								//Finalise content container
								foreach (VisualElement child in codeContainer.Children())
								{
									if (child.ClassListContains(paragraphContainerClass))
									{
										child.AddToClassList("code");
										if (child.childCount == 1)
											AddInlineText("", child);//This seems to be required to get layout to function properly.
									}
								}
								
								//Begin Hack
								FieldInfo m_inheritedStyle = typeof(VisualElement).GetField("inheritedStyle", BindingFlags.NonPublic | BindingFlags.Instance);
								if (m_inheritedStyle == null)
									m_inheritedStyle = typeof(VisualElement).GetField("m_InheritedStylesData", BindingFlags.NonPublic | BindingFlags.Instance);
								Type inheritedStylesData = Type.GetType("UnityEngine.UIElements.StyleSheets.InheritedStylesData,UnityEngine");
								FieldInfo font = inheritedStylesData.GetField("font", BindingFlags.Public | BindingFlags.Instance);
								FieldInfo fontSize = inheritedStylesData.GetField("fontSize", BindingFlags.Public | BindingFlags.Instance);
								Font consola = (Font) EditorGUIUtility.Load("consola");

								contentContainer.Query<Label>().ForEach(l =>
								{
									l.AddToClassList("code");
									
									
									//Hack to regenerate the font size as Rich Text tags are removed from the original calculation.
									object value = m_inheritedStyle.GetValue(l);
									StyleFont fontVar = (StyleFont)font.GetValue(value);
									fontVar.value = consola;
									font.SetValue(value, fontVar);
									StyleLength fontSizeVar = 12;// = (StyleLength) fontSize.GetValue(value); //This doesn't seem to work properly, hard coded for now.
									fontSize.SetValue(value, fontSizeVar);
									m_inheritedStyle.SetValue(l, value);
									Vector2 measuredTextSize = l.MeasureTextSize(l.text.Replace('>', ' '), 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
									l.style.width = measuredTextSize.x;
									l.style.height = measuredTextSize.y;
								});
								
								//Button
								Button codeCopyButtonButtonContainer = new Button(() =>
								{
									EditorGUIUtility.systemCopyBuffer = richText.associatedText;
									Debug.Log("Copied Code to Clipboard");
								});
								codeCopyButtonButtonContainer.ClearClassList();
								codeCopyButtonButtonContainer.AddToClassList("code-button");
								codeCopyButtonButtonContainer.StretchToParentSize();
								codeContainer.Add(codeCopyButtonButtonContainer);

								break;
							case RichTextTag.Tag.span:
								Label spanLabel = new Label
								{
									text = richText.associatedText
								};
								spanLabel.AddToClassList(tag.stringVariables);
								rootToAddTo.Add(spanLabel);
								break;
							case RichTextTag.Tag.image:
								throw new NotImplementedException();
							default:
								throw new ArgumentOutOfRangeException();
						}

						if (inlineText != null)
						{
							inlineText.style.unityFontStyleAndWeight = tag.fontStyle;
							if (tag.size > 0)
								inlineText.style.fontSize = tag.size;
							if (tag.color != Color.clear)
								inlineText.style.color = tag.color;
							results.Add(inlineText);
						}
					}
				}

				root = rootTemp;
			}

			return results;
			
			/*void RichTextDebug(string richText) => Debug.Log(GetRichTextCapableText(richText));
			string GetRichTextCapableText(string richText) => text.Replace("<", "<<b></b>");*/
		}
		
		

		#region Inline
		/// <summary>
		/// Adds a Label that displays inline with other inline content.
		/// </summary>
		/// <param name="text">Text displayed on the Label</param>
		/// <param name="root">Root to append the Label to</param>
		/// <returns>The inline Label</returns>
		public static Label AddInlineText(string text, VisualElement root)
		{
			Label inlineText = new Label
			{
				text = text
			};
			inlineText.AddToClassList("inline-text");
			root.Add(inlineText);
			return inlineText;
		}
		
		/// <summary>
		/// Adds a Button that displays inline with other inline content.
		/// </summary>
		/// <param name="action">Action to perform when the button is pressed</param>
		/// <param name="text">Text to display on the Button</param>
		/// <param name="root">Root to append the Button to</param>
		/// <returns>The inline Button</returns>
		public static Button AddInlineButton(Action action, string text, VisualElement root)
		{
			Button inlineButton = new Button(action)
			{
				text = text
			};
			inlineButton.ClearClassList();
			inlineButton.AddToClassList("inline-button");
			root.Add(inlineButton);
			return inlineButton;
		}

		#endregion

		#region Containers

		public const string paragraphContainerClass = "paragraph-container";

		private static VisualElement AddParagraphContainer(VisualElement root)
		{
			VisualElement paragraphContainer = new VisualElement();
			paragraphContainer.AddToClassList(paragraphContainerClass);
			root.Add(paragraphContainer);
			return paragraphContainer;
		}

		#endregion
	}
}