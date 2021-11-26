using System;
using System.ComponentModel.Design;

using EnvDTE;
using EnvDTE80;

using Microsoft.VisualStudio.Shell;

using Newtonsoft.Json;

using Task = System.Threading.Tasks.Task;

namespace JsonFormatter
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class JsonFormatterCommand
    {
        /// <summary>
        /// Minify/compress JSON command ID.
        /// </summary>
        public const int cmdidMinifyId = 0x0100;

        /// <summary>
        /// Pretty/format JSON command ID.
        /// </summary>
        public const int cmdidPrettyId = 0x0200;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("25e9462e-b849-4317-bcfd-8cdea538f8b3");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonFormatterCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private JsonFormatterCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            // Add minify item to the command set
            CommandID menuCommandID = new CommandID(CommandSet, cmdidMinifyId);
            OleMenuCommand menuItem = new OleMenuCommand(this.MinifyExecute, menuCommandID)
            {
                Supported = false
            };
            commandService.AddCommand(menuItem);

            // Add pretty item to the command set
            menuCommandID = new CommandID(CommandSet, cmdidPrettyId);
            menuItem = new OleMenuCommand(this.PrettyExecute, menuCommandID)
            {
                Supported = false
            };
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static JsonFormatterCommand Instance { get; private set; }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in JsonFormatterCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new JsonFormatterCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MinifyExecute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Document doc = GetActiveDocument();

            string jsonString = FormatJsonString(doc);
            OverwriteAllJsonTextToDocument(doc, jsonString);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void PrettyExecute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Document doc = GetActiveDocument();

            string jsonString = FormatJsonString(doc, Formatting.Indented);
            OverwriteAllJsonTextToDocument(doc, jsonString);
        }

        #region Helper Methods
        private Document GetActiveDocument()
        {
            DTE2 dte = Package.GetGlobalService(typeof(Microsoft.VisualStudio.Shell.Interop.SDTE)) as DTE2;
            Document doc = dte.ActiveDocument;
            if (doc == null)
            {
                return null;
            }

            return doc;
        }

        private string FormatJsonString(Document doc, Formatting formatting = Formatting.None)
        {
            string jsonText = ReadAllJsonTextFromDocument(doc);
            object obj = JsonConvert.DeserializeObject(jsonText);
            return JsonConvert.SerializeObject(obj, formatting);
        }

        private string ReadAllJsonTextFromDocument(Document doc)
        {
            if (doc.Language != "JSON")
            {
                return "";
            }

            TextDocument txt = doc.Object() as TextDocument;
            EditPoint editPoint = txt.StartPoint.CreateEditPoint();
            return editPoint.GetText(txt.EndPoint);
        }

        private void OverwriteAllJsonTextToDocument(Document doc, string jsonString)
        {
            if (doc.Language != "JSON")
            {
                return;
            }

            TextDocument txt = doc.Object() as TextDocument;
            EditPoint editPoint = txt.StartPoint.CreateEditPoint();
            EditPoint movePoint = txt.EndPoint.CreateEditPoint();
            editPoint.ReplaceText(movePoint, jsonString, (int)vsEPReplaceTextOptions.vsEPReplaceTextAutoformat);
        }
        #endregion
    }
}
