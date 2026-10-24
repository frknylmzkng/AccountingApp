namespace WinFormsApp2
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            AppDomain.CurrentDomain.SetData("DataDirectory", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            Application.ThreadException += (_, e) => ShowFailure(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Failure(e.ExceptionObject as Exception ?? new Exception(), "Unhandled");
            TaskScheduler.UnobservedTaskException += (_, e) => { AppLog.Failure(e.Exception, "UnobservedTask"); e.SetObserved(); };
            using var context = new AccountingApplicationContext();
            Application.Run(context);
        }
        private static void ShowFailure(Exception exception)
        {
            string id = AppLog.Failure(exception, "ThreadException");
            MessageBox.Show("Beklenmeyen bir hata oluştu. Kod: " + id, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
