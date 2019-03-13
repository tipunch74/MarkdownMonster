﻿#region License
/*
 **************************************************************
 *  Author: Rick Strahl 
 *          © West Wind Technologies, 2016
 *          http://www.west-wind.com/
 * 
 * Created: 04/28/2016
 *
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 **************************************************************  
*/
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interactivity;
using System.Windows.Media;
using FontAwesome.WPF;
using MarkdownMonster.Windows.PreviewBrowser;
using Westwind.Utilities;
using MarkdownMonster.Utilities;

namespace MarkdownMonster.AddIns
{
    /// <summary>
    /// This class manages loading of addins and 
    /// raising various application events passed
    /// to all addins that they can respond to
    /// </summary>
    public class AddinManager
    {
        /// <summary>
        /// Singleton to get access to Addin Manager
        /// </summary>
        public static AddinManager Current { get; set; }

        /// <summary>
        /// The full list of add ins registered
        /// </summary>
        public List<MarkdownMonsterAddin> AddIns;

        /// <summary>
        /// Set after addins have completed load
        /// </summary>
        public bool AddinsLoadingComplete { get; set; }


        /// <summary>
        /// Fired when addins complete loading. Action
        /// fires out of band from Main UI thread, so
        /// if you do UI operations make sure to use
        /// a Dispatcher.       
        /// </summary>
        public Action AddinsLoaded { get; set; }

        /// <summary>
        /// Add in manager error message  - set when loading addins
        /// if there is a failure.
        /// </summary>
        public string ErrorMessage { get; set; }

        static AddinManager()
        {
            Current = new AddinManager();
        }

        public AddinManager()
        {
            AddIns = new List<MarkdownMonsterAddin>();
            
            // TODO: REMOVE in future Versions - added to deal with internalized Snippets Addin - added in 1.11.3            
            var snippetsAddin = Path.Combine(mmApp.Configuration.AddinsFolder, "Snippets");
            if (Directory.Exists(snippetsAddin))
            {
                try
                {
                    Directory.Delete(snippetsAddin, true);
                }
                catch { }
            }
        }


        private static bool AddinInterfaceFilter(Type typeObj, Object criteriaObj)
        {
            if (typeObj.ToString() == criteriaObj.ToString())
                return true;

            return false;
        }


        /// <summary>
        /// Loads the add-in menu and toolbar buttons
        /// </summary>
        /// <param name="window"></param>
        public void InitializeAddinsUi(MainWindow window, List<MarkdownMonsterAddin> addins = null)
        {
            if (addins == null)
                addins = AddIns;

            foreach (var addin in addins)
            {
                addin.Model = window.Model;                

                foreach (var addinMenuItem in addin.MenuItems)
                {
                    try
                    {
                        var mitem = new MenuItem
                        {
                            Header = addinMenuItem.Caption,
                            Name=StringUtils.ToCamelCase(addinMenuItem.Caption)
                        };

                        if (string.IsNullOrEmpty(addin.Name))
                            addin.Name = addinMenuItem.Caption;
                        
                        Action<object, ICommand> xAction = (s, c) =>
                        {
                            try
                            {
                                addinMenuItem.Execute?.Invoke(mitem);
                            }
                            catch (Exception ex)
                            {
                                mmApp.Log($"Addin {addin.Name ?? addin.Id} Execute failed", ex);
                                string msg = $"The '{addin.Name ?? addin.Id}' addin failed:\r\n\r\n{ex.GetBaseException().Message}\r\n\r\n" + 
                                             "You can check to see if there is an update for the Addin available in the Addin Manager. We also recommend you update to the latest version of Markdown Monster.";
                                MessageBox.Show(msg, "Addin Execution Failed",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        };
                        Func<object, ICommand, bool> cxAction = null;
                        if (addinMenuItem.CanExecute != null)
                            cxAction = (s, e) =>
                            {
                                try
                                {
                                    return addinMenuItem.CanExecute.Invoke(s);
                                }
                                catch (Exception ex)
                                {
                                    mmApp.Log($"Addin {addin.Name} CanExecute failed", ex);
                                    return true;
                                }
                            };                    
                        addinMenuItem.Command = new CommandBase(xAction,cxAction);                    
                        mitem.Command = addinMenuItem.Command;

                        addin.Model.Window.MenuAddins.Items.Add(mitem);
                    
                        // if an icon is provided also add to toolbar
                        if (addinMenuItem.FontawesomeIcon != FontAwesomeIcon.None || addinMenuItem.IconImageSource != null)
                        {
                            var hasConfigMenu = addinMenuItem.ExecuteConfiguration != null;

                            Brush colorBrush;
                            if (string.IsNullOrEmpty(addinMenuItem.FontawesomeIconColor))
                                colorBrush = mmApp.Model.Window.Foreground;
                            else
                            {                                
                                colorBrush = new BrushConverter().ConvertFrom(addinMenuItem.FontawesomeIconColor) as Brush;
                            }

                            var titem = new Button();

                            var source  = addinMenuItem.IconImageSource ??
                                          ImageAwesome.CreateImageSource(addinMenuItem.FontawesomeIcon, colorBrush); 
                            
                            titem.Content = new Image()
                            {
                                Source = source,
                                ToolTip = addinMenuItem.Caption + 
                                          (!string.IsNullOrEmpty(addinMenuItem.KeyboardShortcut) ?
                                              $" ({addinMenuItem.KeyboardShortcut})" :
                                              string.Empty),
                                Height = addinMenuItem.IconImageSource == null ? 18 : 19,
                                Width = addinMenuItem.IconImageSource == null ? 18 : 19,                           
                                Margin = new Thickness(5, 0, hasConfigMenu ? 0 : 5, 0)
                            };
                            addinMenuItem.MenuItemButton = titem;
                        

                            if (addinMenuItem.Execute != null)
                            {
                                titem.Command = addinMenuItem.Command;
                                AddKeyboardShortcut(addinMenuItem, addin);
                            }
                        
                            addin.Model.Window.ToolbarAddIns.Visibility = Visibility.Visible;
                            int toolIndex = addin.Model.Window.ToolbarAddIns.Items.Add(titem);
                    
                            // Add configuration dropdown if configured
                            if (hasConfigMenu)
                            {
                                var tcitem = new Button
                                {
                                    FontSize = 10F,
                                    Content = new Image()
                                    {
                                        Source =
                                            ImageAwesome.CreateImageSource(FontAwesomeIcon.CaretDown,
                                                addin.Model.Window.Foreground),                                    
                                        Height = 16,
                                        Width = 8,
                                        Margin = new Thickness(0, 0, 0, 0),
                                    },
                                    ToolTip = addinMenuItem.Caption + " Configuration",                                
                                };

                                var ctxm = new ContextMenu();
                                tcitem.ContextMenu = ctxm;

                                // create context menu and add drop down behavior
                                var behaviors = Interaction.GetBehaviors(tcitem);
                                behaviors.Add(new DropDownButtonBehavior());

                                tcitem.Click += (sender, args) =>
                                {
                                    ctxm.Items.Clear();
                                    var configMenuItem = new MenuItem()
                                    {
                                        Header = addinMenuItem.Caption
                                    };
                                    configMenuItem.Command = addinMenuItem.Command;                                
                                    if (addinMenuItem.CanExecute != null)
                                        configMenuItem.IsEnabled = addinMenuItem.CanExecute.Invoke(sender);
                                    ctxm.Items.Add(configMenuItem);

                                    configMenuItem = new MenuItem()
                                    {
                                        Header = $"{addinMenuItem.Caption} Configuration",
                                    };
                                    if (addinMenuItem.ExecuteConfiguration != null)
                                        configMenuItem.Click += (s, e) => addinMenuItem.ExecuteConfiguration?.Invoke(s);

                                    addinMenuItem.ConfigurationMenuItem = configMenuItem;

                                    ctxm.Items.Add(configMenuItem);

                                    // Uninstall Addin
                                    if (!addin.Id.Equals("screencapture",
                                            StringComparison.InvariantCultureIgnoreCase) &&
                                        !addin.Id.Equals("weblog", StringComparison.InvariantCultureIgnoreCase) &&
                                        !addin.Id.Equals("snippets", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        configMenuItem = new MenuItem()
                                        {
                                            Header = $"Uninstall {addinMenuItem.Caption}",
                                        };
                                        if (addinMenuItem.ExecuteConfiguration != null)
                                            configMenuItem.Click += (s, e) =>
                                            {
                                                if (AddinManager.Current.UninstallAddin(addin.Id))
                                                {
                                                    mmApp.Model.Window.ShowStatusSuccess(
                                                        $"{addin.Name} addin has been removed.");
                                                    addinMenuItem.MenuItemButton.Visibility = Visibility.Collapsed;
                                                    tcitem.Visibility = Visibility.Collapsed;
                                                }
                                                else
                                                    mmApp.Model.Window.ShowStatusError(
                                                        $"Unable to uninstall {addin.Name} addin.");
                                            };

                                        addinMenuItem.ConfigurationMenuItem = configMenuItem;
                                        ctxm.Items.Add(configMenuItem);
                                    }

                                };

                                addin.Model.Window.ToolbarAddIns.Items.Add(tcitem);
                            };

                        }
                    }
                    catch (Exception ex)
                    {
                        var msg = $"Unable to load add-in: {Path.GetFileNameWithoutExtension(addin.Name)}";
                        mmApp.Log(msg, ex);
                        AddinLoadErrors.AppendLine(msg + "\r\n");                                                                        
                    }
                }
            }

            if (AddinLoadErrors.Length > 0)
            {
                MessageBox.Show(AddinLoadErrors.ToString() +
                    "\r\n\r\n" +
                    "Try updating each failing addin and Markdown Monster to the latest versions or uninstall each failing addins.",
                    "The following addins failed to load",                    
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            _addinLoadErrors = null;
        }


        /// <summary>
        /// adds a keyboard 
        /// </summary>
        /// <param name="menuItem"></param>
        /// <param name="addin"></param>
        private static void AddKeyboardShortcut(AddInMenuItem menuItem, MarkdownMonsterAddin addin)
        {
            if (!string.IsNullOrEmpty(menuItem.KeyboardShortcut))
            {
                KeyBinding kb = KeyBindingsManager.CreateKeyboardShortcutBinding(
                    menuItem.KeyboardShortcut,
                    menuItem.Command);
                if (kb != null)
                    addin.Model.Window.InputBindings.Add(kb);
            }
        }

        #region Addin Manager

        /// <summary>
        /// Loads add-ins into the application from the add-ins folder.
        /// 
        /// Note: This method is called twice: Once for the install
        /// Addins folder for built-in addins and once for the
        /// %AppData% folder for user installed addins.
        /// </summary>
        internal void LoadAddins(string addinPath)
        {
            if (!Directory.Exists(addinPath))
                return;

            // Check for Addins to install or delete
            try
            {
                if (Directory.Exists(addinPath + "\\Install"))
                    InstallAddinFiles(addinPath + "\\Install\\");
            }
            catch (Exception ex)
            {
                mmApp.Log($"Addin update failed: {ex.Message}");
                return;
            }

            var dirs = Directory.GetDirectories(addinPath);
            foreach (var dir in dirs)
            {
                var files = Directory.GetFiles(dir, "*addin.dll");
                foreach (var file in files)
                {
                    string fname = Path.GetFileName(file).ToLower();
                    if (fname.EndsWith("addin.dll"))
                    {
                        try
                        {
                            LoadAddinClasses(file);
                        }
                        catch (Exception e)
                        {
                            mmApp.Log($"Load Addin Classes failed for: " + fname, e);                            
                        }
                    }
                }
            }

        }

        public void UnloadAddins()
        {
            AddIns.Clear();
        }

        public bool InstallAddin(string addinId)
        {
            string addinPath = Path.Combine(mmApp.Configuration.CommonFolder, "Addins", addinId);
            if (!Directory.Exists(addinPath))
                return false;

            var files = Directory.GetFiles(addinPath, "*.dll");
            foreach (var file in files)
            {
                string fname = Path.GetFileName(file).ToLower();
                if (fname.EndsWith("addin.dll"))
                    LoadAddinClasses(file,addinId);

                AddIns.FirstOrDefault(a => a.Id == addinId)?.OnInstall();                
            }

            return true;
        }

        private static StringBuilder AddinLoadErrors => _addinLoadErrors ?? (_addinLoadErrors = new StringBuilder());
        private static StringBuilder _addinLoadErrors;
            
        /// <summary>
        /// Load all add in classes in an assembly
        /// </summary>
        /// <param name="assemblyFile"></param>
        public void LoadAddinClasses(string assemblyFile,string addinId = null)
        {
            Assembly asm = null;
            Type[] types = null;

            try
            {
                asm = Assembly.LoadFrom(assemblyFile);
                types = asm.GetTypes();
            }
            catch(Exception ex)
            {
                var msg = $"Unable to load addin {Path.GetFileNameWithoutExtension(assemblyFile)}";

                mmApp.Log(msg, ex);
                AddinLoadErrors.AppendLine(msg + "\r\n");

                if(!string.IsNullOrEmpty(addinId))
                    UninstallAddin(addinId);

                return;
            }

            foreach (var type in types)
            {
                var typeList = type.FindInterfaces(AddinInterfaceFilter, typeof(IMarkdownMonsterAddin));
                if (typeList.Length > 0)
                {
                    var ai = Activator.CreateInstance(type) as MarkdownMonsterAddin;
                    if (addinId != null)
                        ai.Id = addinId;
                    AddIns.Add(ai);
                }
            }
        }

        /// <summary>
        /// Retrieves a list of addins from the addin repository. Note this list
        /// is retrieved in chunks - first the summary list is retrieved and the
        /// remaining data is filled in later from individual repos.
        /// </summary>        
        /// <returns></returns>
        public List<AddinItem> GetAddinList()
        {            
            var settings = new HttpRequestSettings
            {
                Url = mmApp.Urls.AddinRepositoryUrl,
                Timeout = 5000
            };

            List<AddinItem> addinList;
            try
            {
                addinList = HttpUtils.JsonRequest<List<AddinItem>>(settings);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return null;
            }

            addinList
                .AsParallel()
                .ForAll(ai =>
                {
                    try
                    {
                        var dl = HttpUtils.JsonRequest<AddinItem>(new HttpRequestSettings
                        {
                            Url = ai.gitVersionUrl
                        });
                        DataUtils.CopyObjectData(dl, ai, "id,name,gitVersionUrl,gitUrl");

                        if (Directory.Exists(".\\Addins\\" + ai.id) ||
                            Directory.Exists(".\\Addins\\Installs\\" + ai.id))
                            ai.isInstalled = true;

                        if (File.Exists(".\\Addins\\Installs\\" + ai.id + ".delete"))
                            ai.isInstalled = false;
                    }
                    catch { /* ignore error */}
                });



            return addinList;
        }


        /// <summary>
        /// Retrieves an initial minimal list of addins which is supplemented later
        /// with data from individual repos.
        /// </summary>
        /// <returns></returns>
        public async Task<List<AddinItem>> GetInitialAddinListAsync()
        {

            var settings = new HttpRequestSettings
            {
                Url = mmApp.Urls.AddinRepositoryUrl,
                Timeout = 5000
            };

            List<AddinItem> addinList;
            try
            {
                addinList = await HttpUtils.JsonRequestAsync<List<AddinItem>>(settings);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return null;
            }

            return addinList;
        }


        /// <summary>
        /// Retrieves a list of addins from the addin repository. Note this list
        /// is retrieved in chunks - first the summary list is retrieved and the
        /// remaining data is filled in later from individual repos.
        /// </summary>
        /// <param name="addinList"></param>
        /// <returns></returns>
        public async Task<List<AddinItem>> GetAddinListAsync(List<AddinItem> addinList = null)
        {
      
            if (addinList == null)
                addinList = await GetInitialAddinListAsync();

            if (addinList == null)
                return null;

            Parallel.ForEach(addinList,
                new ParallelOptions { MaxDegreeOfParallelism = 20 },
            //addinList.ForEach(
                ai =>                
                {
                    try
                    {
                        // not using async here so we can wait for final list result          
                        // before returning
                        var dl = HttpUtils.JsonRequest<AddinItem>(new HttpRequestSettings
                        {
                            Url = ai.gitVersionUrl
                        });
                            
                        DataUtils.CopyObjectData(dl, ai, "id,name,gitVersionUrl,gitUrl");

                        string addinFolder = mmApp.Configuration.AddinsFolder;

                        if (Directory.Exists(Path.Combine(addinFolder, ai.id)) ||
                            Directory.Exists(Path.Combine(addinFolder,"Install", ai.id)))
                        {
                            ai.isInstalled = true;                              
                        }

                        try
                        {
                            var versionFile = Path.Combine(addinFolder, ai.id, "version.json");
                            if (File.Exists(versionFile))
                            {
                                var addinItem = JsonSerializationUtils.DeserializeFromFile(
                                        versionFile, typeof(AddinItem), false)
                                    as AddinItem;

                                if (addinItem != null)
                                {
                                    var verInstalled = new Version(addinItem.version);
                                    var verAddin = new Version(ai.version);
                                        
                                    ai.installedVersion = addinItem.version;
                                    if (verInstalled.CompareTo(verAddin) < 0)
                                        ai.updateAvailable = true;
                                }
                            }
                        }
                        catch { }


                        if (File.Exists(".\\Addins\\Install\\" + ai.id + ".delete"))
                            ai.isInstalled = false;
                    }
                    catch (Exception ex)
                    {
                        mmApp.Log($"Addin {ai.name} version failed", ex);
                    }
                });
            
            return addinList
                .Where(ai => ai.updated > new DateTime(2015, 1, 1))
                .OrderBy(ai => ai.isInstalled ? 0 : 1)  
                .ThenByDescending(ai => ai.updated)
                .ToList();
        }

        /// <summary>
        /// This downloads and installs a single addin to the Addins folder.
        /// Note the addin still needs to be in initialized with:
        /// OnApplicationStart() and InializeAddinUi()
        /// 
        /// The addin-loader then moves the files.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="targetFolder">Addins folder</param>
        /// <param name="addin"></param>
        /// <returns></returns>
        public DownloadAndInstallResult DownloadAndInstallAddin(string url, string targetFolder, AddinItem addin)
        {

            var result = new DownloadAndInstallResult() {ExistingAddin = false};

            if (string.IsNullOrEmpty(targetFolder))
            {
                ErrorMessage = "No target folder provided";
                result.IsError = true;
                return result;
            }

            string ver = mmApp.GetVersion();

            var verInstalled = new Version(ver);
            var verMinVersion = new Version(addin.minVersion);

            if (verInstalled.CompareTo(verMinVersion) < 0)
            {
                ErrorMessage = "This addin requires v" + addin.minVersion + " of Markdown Monster to run. You are on v" +  ver + ".\r\n\r\nPlease update to the latest version of Markdown Monster if you want to install this addin.";
                result.IsError = true;
                return result;
            }

            var addinName = Path.GetFileName(targetFolder);
            if (!Directory.Exists(Path.Combine(targetFolder, addin.id))) 
                targetFolder = Path.Combine(targetFolder, addin.id);
            else
            {
                result.ExistingAddin = true;
                targetFolder = Path.Combine(targetFolder, "Install", addin.id);
            }

            string file = Path.GetTempFileName();
            file = Path.ChangeExtension(file, "zip");

            try
            {
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(url, file);
                }
                if (Directory.Exists(targetFolder))
                    Directory.Delete(targetFolder, true);
                
                using (ZipArchive archive = ZipFile.OpenRead(file))
                {
                    foreach (ZipArchiveEntry zipfile in archive.Entries)
                    {
                        string fullName = Path.Combine(targetFolder, zipfile.FullName);

                        //Calculates what the new full path for the unzipped file should be
                        string fullPath = Path.GetDirectoryName(fullName);

                        //Creates the directory (if it doesn't exist) for the new path
                        Directory.CreateDirectory(fullPath);

                        //Extracts the file to (potentially new) path
                        zipfile.ExtractToFile(fullName, true);                    
                    }
                }


                addin.isInstalled = true;
                addin.isEnabled = true;
                addin.installedVersion = addin.version;
                result.NeedsRestart = false;


                if (!result.ExistingAddin)
                {
                    if (!InstallAddin(addin.id))
                        ErrorMessage = addin.name + "  installation  failed.";
                    else
                    {
                        var installedAddin = AddIns.FirstOrDefault(ai => ai.Id == addin.id);
                        if (installedAddin != null)
                        {
                            installedAddin.OnApplicationStart();

                            // trigger parser addins to refresh MarkdownParsers if they are provided
                            if (installedAddin.GetMarkdownParser(false, false) != null)
                                mmApp.Model.OnPropertyChanged(nameof(AppModel.MarkdownParserColumnWidth));

                            InitializeAddinsUi(mmApp.Model.Window,
                                new List<MarkdownMonsterAddin>()
                                {
                                    installedAddin
                                });                            
                        }
                    }
                }
                else
                {
                    result.NeedsRestart = true;
                }

                return result;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                result.IsError = true;
                return result;
            }

        }


        /// <summary>
        /// Result value for Addin Download and Install operation
        /// </summary>
        public class DownloadAndInstallResult
        {
            /// <summary>
            /// Determines if an error occurred during download/install
            /// </summary>
            public bool IsError;

            /// <summary>
            /// If true an existing addin was updated
            /// </summary>
            public bool ExistingAddin = true;

            /// <summary>
            /// Determines if the addin needs a restart to install
            /// </summary>
            public bool NeedsRestart;
        }

        /// <summary>
        /// Uninstalls an addin by removing the addin folder.
        /// </summary>
        /// <param name="addinId"></param>
        /// <param name="addinPath"></param>
        /// <returns></returns>
        public bool UninstallAddin(string addinId, string addinPath = null)
        {
            // try to fire uninstall code if addin is loaded
            AddIns.FirstOrDefault(a => a.Id.Equals(addinId,StringComparison.InvariantCultureIgnoreCase))?.OnUninstall();

            if (string.IsNullOrEmpty(addinPath))
                addinPath = mmApp.Configuration.AddinsFolder;

            var directory = Directory.GetDirectories(addinPath)
                                     .FirstOrDefault(dir => Path.GetFileName(dir).Equals(addinId,StringComparison.InvariantCultureIgnoreCase));
            if (!string.IsNullOrEmpty(directory))
            {
                try
                {
                    if (!Directory.Exists(Path.Combine(addinPath, "Install")))
                        Directory.CreateDirectory(Path.Combine(addinPath, "Install"));

                    File.WriteAllText(Path.Combine(addinPath, "Install", addinId + ".delete"), "to be deleted");
                    return true;
                }
                catch(Exception ex)
                {
                    ErrorMessage = $"Unable to uninstall addin {addinId}: {ex.Message}";
                    mmApp.Log(ErrorMessage, ex);
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Installs pending Addins from the Install folder into the Addins folder
        /// This is required because addins can be already loaded and can't be copied
        /// over.
        /// </summary>
        /// <param name="path">Temporary install path</param>
        public bool InstallAddinFiles(string path = null)
        {
            if (path == null)
                path = Path.Combine(mmApp.Configuration.AddinsFolder, "Install");

            string dirName = null;

            ErrorMessage = null;

            // delete addins flagged for deletion
            var files = Directory.GetFiles(path, "*.delete");
            foreach (var file in files)
            {
                try
                {
                    var deleteFolder = Path.Combine(path + "..\\", Path.GetFileNameWithoutExtension(file));

                    if (Directory.Exists(deleteFolder))
                        Directory.Delete(deleteFolder, true);

                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    ErrorMessage +=  $"Couldn't remove existing addin {Path.GetFileNameWithoutExtension(file)}: {ex.Message}\r\n";
                }
            }

            // install new addins
            var dirs = Directory.GetDirectories(path);
            foreach (var addinInstallFolder in dirs)
            {
                try
                {
                    dirName = Path.GetFileName(addinInstallFolder); // folder name
                    var addinPath = Path.Combine(addinInstallFolder, "..\\..", dirName);
                    if (Directory.Exists(addinPath))
                        Directory.Delete(addinPath, true);

                    Directory.Move(addinInstallFolder, addinPath);
                }
                catch (Exception ex)
                {
                    ErrorMessage += $"Addin installation failed for {dirName}: {ex.Message}\r\n";
                }
            }

            Directory.Delete(path);


            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                mmApp.Log(ErrorMessage);
                return false;
            }

            return true;
        }

        #endregion

        #region Raise Events

        public void RaiseOnApplicationStart()
        {
            foreach (var addin in AddIns)
            {
                try
                {
                    addin?.OnApplicationStart();
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::OnApplicationStart Error: " + ex.GetBaseException().Message);
                }
            }
        }

        public void RaiseOnModelLoaded(AppModel model)
        {
            foreach (var addin in AddIns)
            {
                try
                {
                    addin?.OnModelLoaded(model);
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::OnModelLoaded Error: " + ex.GetBaseException().Message);
                }
            }
        }

        public void RaiseOnWindowLoaded()
        {
            foreach (var addin in AddIns)
            {
                try
                {
                    addin?.OnWindowLoaded();
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::OnWindowLoaded Error: " + ex.GetBaseException().Message);
                }
            }

            UnlockKey.Startup();
        }
        
        public void RaiseOnApplicationShutdown()
        {
            foreach (var addin in AddIns)
            {
                try
                {
                    addin?.OnApplicationShutdown();
                }
                catch (Exception ex)
                {

                    mmApp.Log(addin.Id + "::AddIn::OnApplicationShutdown Error: " + ex.GetBaseException().Message);
                }
            }

            UnlockKey.Shutdown();
        }

        public bool RaiseOnBeforeOpenDocument(string filename)
        {
            foreach (var addin in AddIns)
            {
                if (addin == null)
                    continue;
                try
                {
                    if (!addin.OnBeforeOpenDocument(filename))
                        return false;
                }
                catch (Exception ex)
                {

                    mmApp.Log(addin.Id + "::AddIn::OnBeforeOpenDocument Error: " + ex.GetBaseException().Message);
                }
            }

            return true;
        }


        public void RaiseOnAfterOpenDocument(MarkdownDocument doc)
        {
            foreach (var addin in AddIns)
            {
                try
                {
                    addin?.OnAfterOpenDocument(doc);
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::nAfterOpenDocument Error: " + ex.GetBaseException().Message);
                }
            }
        }

        public bool RaiseOnBeforeSaveDocument(MarkdownDocument doc)
        {
            foreach (var addin in AddIns)
            {
                if (addin == null)
                    continue;
                try
                {
                    if (!addin.OnBeforeSaveDocument(doc))
                        return false;
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::OnBeforeSaveDocument Error: " + ex.GetBaseException().Message);
                }
            }

            return true;
        }


        public void RaiseOnAfterSaveDocument(MarkdownDocument doc)
        {
            foreach (var addin in AddIns)
            {
                try
                {
                    addin?.OnAfterSaveDocument(doc);
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::OnAfterSaveDocument Error: " + ex.GetBaseException().Message);
                }
            }
        }

      


        public string RaiseOnSaveImage(object image)
        {
            string url = null;

            foreach (var addin in AddIns)
            {
                try
                {
                    url = addin?.OnSaveImage(image);
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::OnAfterSaveDocument Error: " + ex.GetBaseException().Message);
                }
            }

            return url;
        }

        public void RaiseOnDocumentActivated(MarkdownDocument doc)
        {
            foreach (var addin in AddIns)
            {
                try
                {
                    addin?.OnDocumentActivated(doc);
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::OnDocumentActivated Error: " + ex.GetBaseException().Message);
                }
            }
        }


        public string RaiseOnModifyPreviewHtml(string html, string markdownHtml )
        {
            foreach (var addin in AddIns)
            {
                try
                {
                    html = addin?.OnModifyPreviewHtml(html, markdownHtml) ?? html;
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::ModifyPreviewHtml Error: " + ex.GetBaseException().Message);
                }
            }

            return html;
        }

        public void RaiseOnNotifyAddin(string command, object parameter)
        {
            foreach (var addin in AddIns)
            {
                try
                {
                    addin?.OnNotifyAddin(command, parameter);
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::OnNotifyAddin Error: " + ex.GetBaseException().Message);
                }
            }
        }

        public string RaiseOnEditorCommand(string action, string input)
        {
            foreach (var addin in AddIns)
            {
                try
                {
                    string html = addin?.OnEditorCommand(action, input);
                    if (string.IsNullOrEmpty(html))
                        return html;
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::OnDocumentActivated Error: " + ex.GetBaseException().Message);
                }
            }

            return null;
        }

        public void RaiseOnDocumentChanged()
        {
            foreach (var addin in AddIns)
            {
                try
                {                    
                    addin?.OnDocumentUpdated();                    
                }
                catch (Exception ex)
                {
                    mmApp.Log(addin.Id + "::AddIn::OnDocumentUpdated Error: " + ex.GetBaseException().Message);
                }
            }

        }

        public bool RaiseOnPreviewLinkNavigation(string url, string src)
        {
            foreach (var addin in AddIns)
            {
                // First one wins!
                bool? handled = addin?.OnPreviewLinkNavigation(url,src);
                if (handled.HasValue && handled.Value)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks all addins for a custom Preview Browser control to be used 
        /// for previewing documents. First match wins. Returns null if
        /// no custom controls are found.
        /// 
        /// This allows overriding the default preview browser.       
        /// </summary>
        /// <returns></returns>
        public IPreviewBrowser RaiseGetPreviewBrowserControl()
        {
            foreach (var addin in AddIns)
            {
                try
                {
                    var preview = addin?.GetPreviewBrowserUserControl();
                    if (preview != null)
                        return preview;
                }
                catch (Exception ex)
                {
                    string msg = addin.Id + "::AddIn::GetPreviewBrowserControl Error: " + ex.GetBaseException().Message;
                    mmApp.Log(msg);
                    mmApp.Model.Window.ShowStatusError(msg);
                }
            }

            return null;
        }

        #endregion
    }
}
