namespace WinFormsApp2
{
    public enum AppPage { Login, Dashboard, General, Joker, Search, Summary, Contact, Admin }

    internal sealed class PageRequestedEventArgs : EventArgs
    {
        public AppPage Page { get; }
        public PageRequestedEventArgs(AppPage page) => Page = page;
    }

    internal sealed class LoginSucceededEventArgs : EventArgs
    {
        public string Username { get; }
        public LoginSucceededEventArgs(string username) => Username = username;
    }

    public class SessionForm : Form
    {
        internal event EventHandler<PageRequestedEventArgs>? PageRequested;
        internal event EventHandler? LogoutRequested;

        protected void RequestPage(AppPage page) => PageRequested?.Invoke(this, new PageRequestedEventArgs(page));
        protected void RequestBack() => RequestPage(AppPage.Dashboard);
        protected void RequestLogout() => LogoutRequested?.Invoke(this, EventArgs.Empty);
    }
}
