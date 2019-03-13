﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Markdig.Parsers;
using MarkdownMonster.Windows;
using MarkdownMonster.Windows.DocumentOutlineSidebar;
using Westwind.Utilities;

namespace MarkdownMonster
{
    /// <summary>
    /// Class that handles display and execution of the editors
    /// context menu.
    /// </summary>
    public class EditorContextMenu
    {
        private ContextMenu ContextMenu;
        private AppModel Model;

        public EditorContextMenu()
        {
            Model = mmApp.Model;
            ContextMenu = Model.Window.EditorContextMenu;
            ContextMenu.Closed += ContextMenu_Closed;
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            Model.ActiveEditor?.SetEditorFocus();
        }

        /// <summary>
        /// Clears all items off the menu
        /// </summary>
        public void ClearMenu()
        {
            ContextMenu?.Items.Clear();
        }

        public void Show()
        {

            if (ContextMenu != null)
            {
                Model.ActiveEditor.SetMarkdownMonsterWindowFocus();

                ContextMenu.PlacementTarget = Model.ActiveEditor.WebBrowser;
                ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;

                ContextMenu.Focus();
                ContextMenu.IsOpen = true;

                var item = ContextMenu.Items[0] as MenuItem;
                item.Focus();
            }
        }

        public void ShowContextMenuAll(IEnumerable<string> suggestions, object range)
        {
            ClearMenu();
            var model = Model;

            SpellcheckSuggestions(suggestions, range);
            AddEditorContext();

            AddUndoRedo();
            AddCopyPaste();
            
            Show();
        }


        /// <summary>
        /// Adds spell check words to the context menu.
        /// </summary>
        /// <param name="suggestions"></param>
        /// <param name="range"></param>
        public void SpellcheckSuggestions(IEnumerable<string> suggestions, object range)
        {
            if (suggestions == null || range == null)
                return;

            var model = Model;

            bool hasSuggestions = false;
            foreach (var sg in suggestions)
            {
                var mi = new MenuItem
                {
                    Header = sg,
                    FontWeight = FontWeights.Bold
                };
                mi.Click += (o, args) =>
                {
                    model.ActiveEditor.AceEditor.replaceSpellRange(range, sg);
                    model.ActiveEditor.IsDirty();
                    model.ActiveEditor.SpellCheckDocument();
                };
                ContextMenu.Items.Add(mi);
                hasSuggestions = true;
            }

            if (hasSuggestions)
                ContextMenu.Items.Add(new Separator());

            var mi2 = new MenuItem()
            {
                Header = "Add to dictionary",
                HorizontalContentAlignment = HorizontalAlignment.Right
            };

            
            mi2.Click += (o, args) =>
            {
                if (range == DBNull.Value)
                {
                    model.Window.ShowStatusError("No misspelled word selected. Word wasn't added to dictionary.");
                    return;
                }

                var text = ((dynamic) range).misspelled as string;
                model.ActiveEditor.AddWordToDictionary(text);
                model.ActiveEditor.SpellCheckDocument();
                model.Window.ShowStatus("Word added to dictionary.", mmApp.Configuration.StatusMessageTimeout);
            };
            ContextMenu.Items.Add(mi2);
            ContextMenu.Items.Add(new Separator());

        }

        /// <summary>
        /// Adds Copy/Cut/Paste Context menu options
        /// </summary>
        public void AddCopyPaste()
        {
            var selText = Model.ActiveEditor?.AceEditor?.getselection(false);
            var model = Model;

            var miCut = new MenuItem { Header = "Cut", InputGestureText="Ctrl-X" };
            miCut.Click += (o, args) => model.ActiveEditor.SetSelection("");
            ContextMenu.Items.Add(miCut);

            var miCopy = new MenuItem() { Header = "Copy", InputGestureText="Ctrl-C" };
            miCopy.Click += (o, args) =>
            {
                ClipboardHelper.SetText(selText, true);
            };
            ContextMenu.Items.Add(miCopy);

            var miCopyHtml = new MenuItem() { Header = "Copy As Html", InputGestureText=Model.Commands.CopyAsHtmlCommand.KeyboardShortcut };
            miCopyHtml.Command = Model.Commands.CopyAsHtmlCommand;
            ContextMenu.Items.Add(miCopyHtml);

            bool hasClipboardData = false;
            var miPaste = new MenuItem() { Header = "Paste", InputGestureText = "Ctrl-V" };
            if (ClipboardHelper.ContainsImage())
            {
                hasClipboardData = true;
                miPaste.Header = "Paste Image";
                miPaste.Click += (o, args) => model.ActiveEditor?.PasteOperation();                
            }
            else
            {
                hasClipboardData = ClipboardHelper.ContainsText();
                miPaste.Click += (s,e) => Model.ActiveEditor.PasteOperation();
            }

            miPaste.IsEnabled = hasClipboardData;

            ContextMenu.Items.Add(miPaste);

            if (string.IsNullOrEmpty(selText))
            {
                miCopy.IsEnabled = false;
                miCopyHtml.IsEnabled = false;
                miCut.IsEnabled = false;
            }
            else { 
                if (Model.ActiveEditor?.EditorSyntax != "markdown")
                    miCopyHtml.IsEnabled = false;
                else
                {
                    ContextMenu.Items.Add(new Separator());
                    var miRemoveFormatting = new MenuItem
                    {
                        Header = "Remove Markdown Formatting",
                        InputGestureText = Model.Commands.RemoveMarkdownFormattingCommand.KeyboardShortcut
                    };
                    miRemoveFormatting.Command = Model.Commands.RemoveMarkdownFormattingCommand;
                    ContextMenu.Items.Add(miRemoveFormatting);
                }
            }

            
        }
        
        public void AddUndoRedo()
        {

            var editor = Model.ActiveEditor;

            var undoManager = editor.AceEditor.editor.session.getUndoManager(false);
            bool hasUndo = undoManager.hasUndo(false);
            bool hasRedo = undoManager.hasRedo(false);

            var miUndo = new MenuItem { Header = "Undo", InputGestureText = "Ctrl-Z" };
            if (!hasUndo)
                miUndo.IsEnabled = false;
            miUndo.Click += (o, args) => Model.ActiveEditor.AceEditor.editor.undo(false);
            ContextMenu.Items.Add(miUndo);

            var miRedo = new MenuItem() { Header = "Redo", InputGestureText = "Ctrl-Y" };
            if (!hasRedo)
                miRedo.IsEnabled = false;
            miRedo.Click += (o, args) => Model.ActiveEditor.AceEditor.editor.redo(false);            
            ContextMenu.Items.Add(miRedo);
           
            ContextMenu.Items.Add(new Separator());
        }

        /// <summary>
        /// Adds context sensitive links for Image Links, Hyper Links
        /// </summary>
        public void AddEditorContext()
        {
            var model = Model;
            var lineText = Model.ActiveEditor?.GetCurrentLine();

            var pos = Model.ActiveEditor.GetCursorPosition();
            if (pos.row == -1)
                return;

            var contextCount = ContextMenu.Items.Count;

            if (!string.IsNullOrEmpty(lineText))
            {
                CheckForImageLink(lineText, pos);
                CheckForHyperLink(lineText, pos);
                CheckForTable(lineText, pos);
            }

            if (ContextMenu.Items.Count > contextCount)
                ContextMenu.Items.Add(new Separator());
        }


        static readonly Regex HrefRegex = new Regex(@"(?<!\!)\[.*?\]\(.*?\)", RegexOptions.IgnoreCase);

        private bool CheckForHyperLink(string line, AcePosition pos)
        {
            var matches = HrefRegex.Matches(line);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    string val = match.Value;
                    if (match.Index <= pos.column && match.Index + val.Length > pos.column)
                    {
                        var mi2 = new MenuItem
                        {
                            Header = "Edit Hyperlink"
                        };
                        mi2.Click += (o, args) =>
                        {
                            Model.ActiveEditor.AceEditor.SetSelectionRange(pos.row, match.Index, pos.row,
                                match.Index + val.Length, pos);
                            Model.ActiveEditor.EditorSelectionOperation("hyperlink", val);
                        };
                        ContextMenu.Items.Add(mi2);

                        if (val.Contains("](#"))
                        {
                            mi2 = new MenuItem
                            {
                                Header = "Jump to Anchor"
                            };
                            mi2.Click += (o, args) =>
                            {
                                var anchor = StringUtils.ExtractString(val, "](#", ")");
                                
                                var docModel = new DocumentOutlineModel();
                                int lineNo =docModel.FindHeaderHeadline(Model.ActiveEditor?.GetMarkdown(), anchor);

                                if (lineNo != -1)
                                    Model.ActiveEditor.GotoLine(lineNo);
                            };
                            ContextMenu.Items.Add(mi2);
                        }

                        var mi = new MenuItem
                        {
                            Header = "Remove Hyperlink"
                        };
                        mi.Click += (o, args) =>
                        {
                            Model.ActiveEditor.AceEditor.SetSelectionRange(pos.row, match.Index, pos.row,
                                match.Index + val.Length, pos);
                            string text = StringUtils.ExtractString(val, "[", "]");
                            Model.ActiveEditor.SetSelection(text);
                        };
                        ContextMenu.Items.Add(mi);

                        return true;
                    }
                }
            }
            return false;
        }

        static readonly Regex ImageRegex = new Regex(@"!\[.*?\]\(.*?\)", RegexOptions.IgnoreCase);

        private bool CheckForImageLink(string line, AcePosition pos)
        {
            // Check for images ![](imageUrl)
            var matches = ImageRegex.Matches(line);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    string val = match.Value;
                    if (match.Index <= pos.column && match.Index + val.Length > pos.column)
                    {
                        var mi = new MenuItem
                        {
                            Header = "Edit in Image Editor"
                        };
                        mi.Click += (o, args) =>
                        {
                            var image = StringUtils.ExtractString(val, "(", ")");
                            image = mmFileUtils.NormalizeFilenameWithBasePath(image,
                                Path.GetDirectoryName(Model.ActiveDocument.Filename));
                            mmFileUtils.OpenImageInImageEditor(image);
                        };
                        ContextMenu.Items.Add(mi);

                        var mi2 = new MenuItem
                        {
                            Header = "Edit Image Link"
                        };
                        mi2.Click += (o, args) =>
                        {
                            Model.ActiveEditor.AceEditor.SetSelectionRange(pos.row, match.Index, pos.row,
                                match.Index + val.Length, pos);
                            Model.ActiveEditor.EditorSelectionOperation("image", val);
                        };
                        ContextMenu.Items.Add(mi2);

                        return true;
                    }

                }
            }

            return false;
        }

        private bool CheckForTable(string line, AcePosition pos)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            if (line.Contains("|")  ||
                line.Trim().StartsWith("+-") && line.Trim().EndsWith("-+"))

            {
                var mi = new MenuItem
                {
                    Header = "Edit Table"
                };
                mi.Click += (o, args) =>
                {
                    var editor = Model.ActiveEditor;
                  
                    var lineText = editor?.GetCurrentLine();
                    if (string.IsNullOrEmpty(lineText) ||
                        !(lineText.Contains("|") &&
                        !(lineText.Trim().StartsWith("+") && lineText.Trim().EndsWith("+") )))
                        return;

                    var startPos = editor.GetCursorPosition();
                    var row = startPos.row;
                    var startRow = row;
                    for (int i = row - 1; i > -1; i--)
                    {
                        lineText = editor.GetLine(i);
                        if (!lineText.Contains("|")  &&
                           !(lineText.Trim().StartsWith("+") && lineText.Trim().EndsWith("+")))
                        {
                            startRow = i +1;
                            break;
                        }

                        if (i == 0)
                        {
                            startRow = 0;
                            break;
                        }
                    }
                    
                    var endRow = startPos.row;
                    for (int i = row + 1; i < 99999999; i++)
                    {
                        lineText = editor.GetLine(i);                       
                        if (!lineText.Contains("|")  &&
                            !(lineText.Trim().StartsWith("+") && lineText.Trim().EndsWith("+")))
                        {
                            endRow = i -1;
                            break;
                        }
                    }

                    if (endRow == startRow || endRow < startRow + 2)
                        return;

                    StringBuilder sb = new StringBuilder();
                    for (int i = startRow; i <= endRow; i++)
                    {
                        sb.AppendLine(editor.GetLine(i));                        
                    }

                    // select the entire table
                    Model.ActiveEditor.AceEditor.SetSelectionRange(startRow , 0, endRow + 1,0, pos);
                                
                    Model.ActiveEditor.EditorSelectionOperation("table", sb.ToString());
                };
                ContextMenu.Items.Add(mi);
                return true;
            }            
            else if (line.Trim().StartsWith("<td ",StringComparison.InvariantCultureIgnoreCase)  ||
                     line.Trim().StartsWith("<tr ", StringComparison.InvariantCultureIgnoreCase) ||
                     line.Trim().StartsWith("<th ", StringComparison.InvariantCultureIgnoreCase) ||
                     line.Trim().StartsWith("<table>", StringComparison.InvariantCultureIgnoreCase) ||
                     line.Trim().StartsWith("<table ", StringComparison.InvariantCultureIgnoreCase) ||
                     line.Trim().Equals("<thead>", StringComparison.InvariantCultureIgnoreCase) ||
                     line.Trim().Equals("<tbody>", StringComparison.InvariantCultureIgnoreCase) )
            {
                var mi = new MenuItem
                {
                    Header = "Edit Table"
                };
                mi.Click += (o, args) =>
                {
                    var editor = Model.ActiveEditor;

                    var lineText = editor.GetCurrentLine();

                    var startPos = editor.GetCursorPosition();
                    var row = startPos.row;
                    var startRow = -1;
                    
                    for (int i = row - 1; i > -1; i--)
                    {
                        lineText = editor.GetLine(i);
                        if (lineText.Trim().StartsWith("<table", StringComparison.InvariantCultureIgnoreCase))                        {
                            startRow = i;
                            break;
                        }
                    }

                    if (startRow == -1)
                        return;

                    var endRow = startRow;
                    for (int i = row + 1; i < 99999999; i++)
                    {
                        lineText = editor.GetLine(i);
                        if (lineText.Trim().Equals("</table>", StringComparison.InvariantCultureIgnoreCase))
                        {
                            endRow = i;
                            break;
                        }
                    }

                    if (endRow == startRow)
                        return;

                    StringBuilder sb = new StringBuilder();
                    for (int i = startRow; i <= endRow; i++)
                    {
                        sb.AppendLine(editor.GetLine(i));
                    }
                    
                    // select the entire table
                    Model.ActiveEditor.AceEditor.SetSelectionRange(startRow - 1, 0, endRow + 1, 0, pos);

                    Model.ActiveEditor.EditorSelectionOperation("table", sb.ToString());
                };
                ContextMenu.Items.Add(mi);
                return true;
            }

            return false;
        }
    }
}
