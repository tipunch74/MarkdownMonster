﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using MarkdownMonster.Windows;
using Microsoft.Win32;
using Westwind.Utilities;

namespace MarkdownMonster.Utilities
{

    /// <summary>
    /// Reusable functions to save various files to disk with prompts
    /// and save UI operations.
    /// </summary>
    public class FileSaver
    {

        #region Markdown File
        /// <summary>
        /// Gets a Markdown Save file name
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static string GetMarkdownSaveFilename(string filename = null, string folder = null)
        {
            if (filename == null)
                filename = "untitled";

            if (folder == null)
                folder = mmApp.Configuration.LastFolder;
            
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                folder = mmApp.Configuration.LastFolder;
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                    folder = KnownFolders.GetPath(KnownFolder.Libraries);
            }

            SaveFileDialog sd = new SaveFileDialog
            {
                FilterIndex = 1,
                InitialDirectory = folder,
                FileName = filename,
                CheckFileExists = false,
                OverwritePrompt = true,
                CheckPathExists = true,
                RestoreDirectory = true
            };

            
            sd.Filter =
                "Markdown files (*.md)|*.md|Markdown files (*.markdown)|*.markdown|All files (*.*)|*.*";

            bool? result = null;
            try
            {
                result = sd.ShowDialog();
            }
            catch (Exception ex)
            {
                mmApp.Log("Unable to save file: " + filename, ex);
                MessageBox.Show(
                    $@"Unable to open file:\r\n\r\n" + ex.Message,
                    "An error occurred trying to open a file",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            if (result != null && result.Value)
            {                
                mmApp.Configuration.LastFolder = Path.GetDirectoryName(sd.FileName);
                return sd.FileName;
            }

            return null;
        }


        /// <summary>
        /// Saves a Markdown Document to file with Save UI.
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="saveAsEncrypted"></param>
        /// <returns></returns>
        public static bool SaveMarkdownDocumentToFile(MarkdownDocument doc = null, bool saveAsEncrypted = false)
        {
            if (doc == null)
                doc = mmApp.Model.ActiveDocument;

            string filename = Path.GetFileName(doc.Filename);
            string folder = Path.GetDirectoryName(doc.Filename);

            if (filename == null)
                filename = "untitled";

            if (folder == null)
                folder = mmApp.Configuration.LastFolder;

            if (filename == "untitled")
            {
                folder = mmApp.Configuration.LastFolder;

                var match = Regex.Match(doc.CurrentText, @"^# (\ *)(?<Header>.+)", RegexOptions.Multiline);

                if (match.Success)
                {
                    filename = match.Groups["Header"].Value;
                    if (!string.IsNullOrEmpty(filename))
                        filename = FileUtils.SafeFilename(filename);
                }
            }

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                folder = mmApp.Configuration.LastFolder;
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                    folder = KnownFolders.GetPath(KnownFolder.Libraries);
            }


            SaveFileDialog sd = new SaveFileDialog
            {
                FilterIndex = 1,
                InitialDirectory = folder,
                FileName = filename,
                CheckFileExists = false,
                OverwritePrompt = true,
                CheckPathExists = true,
                RestoreDirectory = true
            };

          
            bool? result = null;
            try
            {
                result = sd.ShowDialog();
            }
            catch (Exception ex)
            {
                mmApp.Log("Unable to save file: " + doc.Filename, ex);
                MessageBox.Show(
                    $@"Unable to open file:\r\n\r\n" + ex.Message,
                    "An error occurred trying to open a file",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            if (!saveAsEncrypted)
                doc.Password = null;
            else
            {
                var pwdDialog = new FilePasswordDialog(doc, false)
                {
                    Owner = mmApp.Model.Window
                };
                bool? pwdResult = pwdDialog.ShowDialog();
            }

            if (result == null || !result.Value)
                return false;

            doc.Filename = sd.FileName;
                
            if (!doc.Save())
            {
                MessageBox.Show(mmApp.Model.Window,
                    $"{sd.FileName}\r\n\r\nThis document can't be saved in this location. The file is either locked or you don't have permissions to save it. Please choose another location to save the file.",
                    "Unable to save Document", MessageBoxButton.OK, MessageBoxImage.Warning);                    
                return false;
            }
            mmApp.Configuration.LastFolder = Path.GetDirectoryName(sd.FileName);
            return true;            
        }

        #endregion


        #region Markdown from Url


        /// <summary>
        /// Tries to fix up Markdown files for common doc and server
        /// platforms like Github, Gists, BitBucket and a few others
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string ParseMarkdownUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            var lurl = url.ToLower();

            string urlToOpen = url;

            
            if (lurl.Contains("gist.github.com"))
            {
                // Orig: https://gist.github.com/RickStrahl/2e485205f9a1a7f9c827c1da172e185b
                // Raw:  https://gist.github.com/RickStrahl/2e485205f9a1a7f9c827c1da172e185b/raw
                if (!lurl.Contains("/raw"))
                    urlToOpen = url + "/raw";
            }
            else if (lurl.Contains("docs.microsoft.com"))
            {
                // orig: https://docs.microsoft.com/en-us/dotnet/csharp/getting-started/
                // github: https://github.com/dotnet/docs/blob/master/docs/csharp/getting-started/index.md
                if (!lurl.Contains(".md"))
                {
                    try
                    {                        
                        string content = HttpUtils.HttpRequestString(url);

                        var doc = new HtmlAgilityPack.HtmlDocument();
                        doc.LoadHtml(content);
                        
                        var node = doc.DocumentNode.SelectSingleNode("//a[@data-original_content_git_url]");

                        // process for Github below
                        url = node.Attributes["href"].Value;
                        lurl = url.ToLower();
                    }
                    catch { }
                }
            }
            else if (lurl.Contains("/bitbucket.org/"))
            {
                // orig: https://bitbucket.org/RickStrahl/swfox_webbrowser/src/1fc23444c27cb691b47917663eabdf7ff9dec49e/Readme.md?at=master&fileviewer=file-view-default
                // raw: https://bitbucket.org/RickStrahl/swfox_webbrowser/raw/1fc23444c27cb691b47917663eabdf7ff9dec49e/Readme.md
                if (lurl.Contains("/src/"))
                    urlToOpen = url.Replace("/src/", "/raw/");
            }


            // Last as some other URLs translate to github urls
            if (lurl.Contains("/github.com/"))
            {
                if (!lurl.Contains(".md"))
                {
                    // Norm: https://github.com/RickStrahl/MarkdownMonster
                    // Conv: https://github.com/RickStrahl/MarkdownMonster/blob/master/Readme.md
                    if (!url.EndsWith("/"))
                        url += "/";
                    url = url + "blob/master/README.md";
                    lurl = url.ToLower();
                }
                if (lurl.Contains(".md"))
                {
                    urlToOpen = url.Replace("/blob/","/raw/");
                }

            }
            
            return urlToOpen;
        }

        public void SaveMarkdownFileFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;

            var urlToOpen = ParseMarkdownUrl(url);

            if (urlToOpen == null)
                return;

            string markdownText = null;
            var settings = new HttpRequestSettings {Url = urlToOpen};
            
            while (true)
            {
                try
                {
                    markdownText = HttpUtils.HttpRequestString(settings);
                    break;
                }
                catch
                {
                    if (settings.Url.Contains("README.md"))                    
                        settings.Url = settings.Url.Replace("README.md", "readme.md");
                    else if (settings.Url.Contains("readme.md"))
                        settings.Url = settings.Url.Replace("readme.md", "Readme.md");
                    else
                        break;
                }
            }

            if (settings.ResponseStatusCode == System.Net.HttpStatusCode.OK || string.IsNullOrEmpty(markdownText))
            {
                mmApp.Model.Window.ShowStatusError($"Couldn't open url: {url}");
                return;
            }


            var doc = new MarkdownDocument();
            doc.CurrentText = markdownText;
            
            SaveMarkdownDocumentToFile(doc);

        }

        public MarkdownDocument OpenMarkdownDocumentFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            var urlToOpen = ParseMarkdownUrl(url);

            if (urlToOpen == null)
                return null;

            string markdownText = null;
            var settings = new HttpRequestSettings { Url = urlToOpen };

            while (true)
            {
                try
                {
                    markdownText = HttpUtils.HttpRequestString(settings);
                    break;
                }
                catch
                {
                    if (settings.Url.Contains("README.md"))
                        settings.Url = settings.Url.Replace("README.md", "readme.md");
                    else if (settings.Url.Contains("readme.md"))
                        settings.Url = settings.Url.Replace("readme.md", "Readme.md");
                    else
                        break;
                }
            }

            if (settings.ResponseStatusCode == System.Net.HttpStatusCode.OK || string.IsNullOrEmpty(markdownText))
            {
                mmApp.Model.Window.ShowStatusError($"Couldn't open url: {url}");
                return null;
            }


            var doc = new MarkdownDocument();
            doc.CurrentText = markdownText;
            return doc;
        }


        /// <summary>
        /// Attempts to parse a title from a Markdown document by 
        /// looking at YAML title header or the first `# ` markdown tag
        /// </summary>
        /// <param name="markdown"></param>
        /// <returns></returns>
        public static string ParseMarkdownTitle(string markdown)
        {
            string title = null;

            var firstLines = StringUtils.GetLines(markdown, 30);
            var firstLinesText = String.Join("\n", firstLines);

            // Assume YAML 
            if (markdown.StartsWith("---"))
            {
                var yaml = StringUtils.ExtractString(firstLinesText, "---", "---", returnDelimiters: true);
                if (yaml != null)
                    title = StringUtils.ExtractString(yaml, "title: ", "\n");
            }

            if (title == null)
            {
                foreach (var line in firstLines.Take(10))
                {
                    if (line.TrimStart().StartsWith("# "))
                    {
                        title = line.TrimStart(new char[] { ' ', '\t', '#' });
                        break;
                    }
                }
            }
        
            return title;
        }

        /// <summary>
        /// Attempts to parse a title from a Markdown document by 
        /// looking at YAML title header or the first `# ` markdown tag
        /// </summary>
        /// <param name="markdown"></param>
        /// <returns></returns>
        public static string ParseMarkdownSafeTitle(string markdown)
        {
            var title = ParseMarkdownTitle(markdown);

            if (string.IsNullOrEmpty(title))
                return null;

            return FileUtils.CamelCaseSafeFilename($"{title}.md");
        }
        #endregion
    }
}
