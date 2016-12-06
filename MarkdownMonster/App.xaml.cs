#region License
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using MahApps.Metro.Controls;
using MarkdownMonster.AddIns;

namespace MarkdownMonster
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public static Mutex Mutex { get; set; }

        public static string initialStartDirectory;

        public App()
        {
            initialStartDirectory = Environment.CurrentDirectory;

            SplashScreen splashScreen = new SplashScreen("assets/markdownmonstersplash.png");
            splashScreen.Show(true);

            if (mmApp.Configuration.UseSingleWindow)
            {
                bool isOnlyInstance = false;
                Mutex = new Mutex(true, @"MarkdownMonster", out isOnlyInstance);
                if (!isOnlyInstance)
                {
                    string filesToOpen = " ";
                    var args = Environment.GetCommandLineArgs();
                    if (args != null && args.Length > 1)
                    {
                        StringBuilder sb = new StringBuilder();
                        for (int i = 1; i < args.Length; i++)
                        {
                            string file = args[i];

                            // check if file exists and fully qualify to 
                            // pass to named pipe
                            if (!File.Exists(file))
                            {
                                file = Path.Combine(initialStartDirectory, file);
                                if (!File.Exists(file))
                                    file = null;                                
                            }

                            if (!string.IsNullOrEmpty(file))                                                            
                                sb.AppendLine(Path.GetFullPath(file));
                            
                        }
                        filesToOpen = sb.ToString();
                    }
                    var manager = new NamedPipeManager("MarkdownMonster");
                    manager.Write(filesToOpen);

                    splashScreen.Close(TimeSpan.MinValue);

                    // this exits the application                    
                    Environment.Exit(0);
                }
            }


            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
#if !DEBUG

            
            //currentDomain.UnhandledException += new UnhandledExceptionEventHandler(GlobalErrorHandler);


            DispatcherUnhandledException += App_DispatcherUnhandledException;
#endif
            mmApp.Started = DateTime.UtcNow;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {            
            if (args.Name.Contains(".resources,")) // ignore resource requests
                return null;

            // check for assemblies already loaded
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (assembly != null)
                return assembly;

            // Try to load by filename - split out the filename of the full assembly name
            // and append the base path of the original assembly (ie. look in the same dir)
            // NOTE: this doesn't account for special search paths but then that never
            //           worked before either.
            string filename = args.Name.Split(',')[0] + ".dll";
            
            // try to load assembly out of path of calling assembly
            if (args.RequestingAssembly != null)
            {
                var path = Path.GetDirectoryName(args.RequestingAssembly.Location);
                var asm = Assembly.LoadFrom(Path.Combine(path, filename));
                if (asm != null)
                    return asm;
            }


            // Last ditch: Try to load out of application base path
            string file = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), filename + ".dll");
            try
            {
                return Assembly.LoadFrom(file);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {

            if (!mmApp.HandleApplicationException(e.Exception as Exception))
                Shutdown(0);
            else
                e.Handled = true; 

            return;            
        }

        
        public static string UserDataPath { get; internal set; }
        public static string VersionCheckUrl { get; internal set; }


        /// TODO: Handle global errors
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        //static void GlobalErrorHandler(object sender, UnhandledExceptionEventArgs args)
        //{
        //    if (!mmApp.HandleApplicationException(args.ExceptionObject as Exception))
        //        Environment.Exit(0);
        //}

        protected override void OnStartup(StartupEventArgs e)
        {
            var dir = Assembly.GetExecutingAssembly().Location;
            
            Directory.SetCurrentDirectory(Path.GetDirectoryName(dir));            

            mmApp.SetTheme(mmApp.Configuration.ApplicationTheme, App.Current.MainWindow as MetroWindow);


            AddinManager.Current.LoadAddins();
            AddinManager.Current.RaiseOnApplicationStart();            
        }

    }
}
