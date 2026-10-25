using System.Reflection;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
public class SessionManagementTests
{
    private sealed class TestScreen : SessionForm
    {
        public TestScreen()
        {
            ShowInTaskbar = false;
            Opacity = 0;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-30000, -30000);
        }

        public void GoBack() => RequestBack();
        public void Go(AppPage page) => RequestPage(page);
        public void SignOut() => RequestLogout();
        public Task<bool> Run(Func<CancellationToken, Task> operation) => RunOperationAsync(operation);
    }

    private sealed class FailingScreen : Form
    {
        protected override void SetVisibleCore(bool value)
        {
            if (value) throw new InvalidOperationException("Deliberate show failure");
            base.SetVisibleCore(value);
        }
    }

    private sealed class Screens
    {
        public List<Form> Created { get; } = new List<Form>();
        public Func<string, string, Task<bool>> Authenticate { get; set; } = (_, _) => Task.FromResult(true);
        public AppPage? FailCreation { get; set; }
        public AppPage? FailShow { get; set; }

        public Form Create(AppPage page)
        {
            if (page == FailCreation) throw new InvalidOperationException("Deliberate factory failure");
            Form form;
            if (page == FailShow) form = new FailingScreen();
            else if (page == AppPage.Login)
                form = new Giris(() => Task.CompletedTask, (user, password) => Authenticate(user, password))
                {
                    ShowInTaskbar = false,
                    Opacity = 0,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-30000, -30000)
                };
            else form = new TestScreen();
            Created.Add(form);
            return form;
        }
    }

    private static TextBox LoginField(Giris login, string name) =>
        (TextBox)typeof(Giris).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(login)!;

    [Test]
    public async Task RunningOperationBlocksToolbarNavigationLogoutAndWindowClose()
    {
        var screens = new Screens(); using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("admin"); var screen = (TestScreen)context.MainForm!;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = screen.Run(_ => completion.Task);
        try
        {
            Assert.That(screen.IsOperationRunning, Is.True);
            Assert.That(screen.Controls.OfType<ToolStrip>().All(t => !t.Enabled), Is.True);
            screen.Go(AppPage.Payments); screen.SignOut(); screen.Close();
            Assert.That(context.Navigate(AppPage.General), Is.False); context.Logout();
            Assert.That(context.IsAuthenticated, Is.True); Assert.That(context.MainForm, Is.SameAs(screen));
            Assert.That(screen.IsDisposed, Is.False);
        }
        finally { completion.SetResult(); await running; }
        Assert.That(screen.Controls.OfType<ToolStrip>().All(t => t.Enabled), Is.True);
        Assert.That(context.Navigate(AppPage.Payments), Is.True); context.Logout();
        Assert.That(context.IsAuthenticated, Is.False);
    }
    [Test]
    public async Task RevokedSessionReturnsToLoginAndDisposesProtectedScreen()
    {
        var screens = new Screens(); using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("operator", UserRole.PersonnelManager); var previous = context.MainForm!;
        context.SessionValidator = (_, _) => Task.FromResult(false);
        await context.CheckSessionAsync();
        Assert.That(context.IsAuthenticated, Is.False); Assert.That(context.Session, Is.Null);
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.Login)); Assert.That(previous.IsDisposed, Is.True);
        Assert.That(AppTheme.Descendants(context.MainForm!).OfType<Label>().Any(l => l.Visible && l.Text.Contains("oturumunuz sona erdi")), Is.True);
    }
    [Test]
    public async Task ServerRevocationDuringOperationLogsOutWithoutAnErrorDialog()
    {
        var screens = new Screens(); using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("operator", UserRole.PersonnelManager); var screen = (TestScreen)context.MainForm!;
        bool result = await screen.Run(_ => throw new SessionRevokedException());
        Assert.That(result, Is.False); Assert.That(context.IsAuthenticated, Is.False); Assert.That(screen.IsDisposed, Is.True);
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.Login));
    }
    [Test]
    public async Task TransientSessionCheckFailureDoesNotPretendAccountWasRevoked()
    {
        var screens = new Screens(); using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("operator"); var previous = context.MainForm;
        context.SessionValidator = (_, _) => throw new IOException("Temporary network failure");
        await context.CheckSessionAsync();
        Assert.That(context.IsAuthenticated, Is.True); Assert.That(context.MainForm, Is.SameAs(previous));
    }
    [Test]
    public async Task LateRevocationResultCannotLogoutNewSession()
    {
        var screens = new Screens(); using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("old-user");
        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.SessionValidator = (_, _) => result.Task;
        var check = context.CheckSessionAsync(); context.Logout(); context.StartSession("new-user");
        result.SetResult(false); await check;
        Assert.That(context.IsAuthenticated, Is.True); Assert.That(context.Username, Is.EqualTo("new-user"));
    }
    [Test]
    public async Task DetectedRevocationWaitsForRunningOperationThenClosesSession()
    {
        var screens = new Screens(); using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("operator"); var screen = (TestScreen)context.MainForm!;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = screen.Run(_ => release.Task);
        try
        {
            context.SessionValidator = (_, _) => Task.FromResult(false); await context.CheckSessionAsync();
            Assert.That(context.IsAuthenticated, Is.True); Assert.That(context.Navigate(AppPage.General), Is.False);
        }
        finally { release.SetResult(); await operation; }
        await context.CheckSessionAsync();
        Assert.That(context.IsAuthenticated, Is.False); Assert.That(screen.IsDisposed, Is.True);
    }
    [Test]
    public void StartingAuthenticatedSessionPreservesSecurityVersion()
    {
        var screens = new Screens(); using var context = new AccountingApplicationContext(screens.Create);
        var session = new UserSession("operator", UserRole.PersonnelManager) { SecurityVersion = [0,0,0,0,0,0,0,1] };
        context.StartSession(session);
        Assert.That(context.Session, Is.SameAs(session));
        Assert.That(((SessionForm)context.MainForm!).Session!.SecurityVersion, Is.EqualTo(session.SecurityVersion));
    }
    [TestCase(UserRole.Admin)]
    [TestCase(UserRole.PersonnelManager)]
    [TestCase(UserRole.Viewer)]
    public void TemporaryLoginCanOnlyNavigateToPasswordChange(UserRole role)
    {
        var screens = new Screens(); using var context = new AccountingApplicationContext(screens.Create);
        var restricted = new UserSession("temporary", role) { MustChangePassword = true };
        Assert.That(context.StartSession(restricted), Is.True);
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.PasswordChange));
        foreach (var page in new[] { AppPage.Dashboard, AppPage.General, AppPage.Joker, AppPage.Payments, AppPage.Admin, AppPage.Settings, AppPage.Search })
            Assert.That(context.Navigate(page), Is.False);
        Assert.That(context.Navigate(AppPage.PasswordChange), Is.True);
        context.Logout(); Assert.That(context.Session, Is.Null);
    }
    [Test]
    public void RestrictedAdminDoesNotStartAutomaticBackup()
    {
        var screens = new Screens(); using var context = new AccountingApplicationContext(screens.Create);
        context.SessionValidator = (_, _) => Task.FromResult(true);
        typeof(AccountingApplicationContext).GetField("backgroundServices", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(context, true);
        context.StartSession(new UserSession("temporary", UserRole.Admin) { MustChangePassword = true });
        Assert.That(typeof(AccountingApplicationContext).GetField("backupTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context), Is.Null);
        Assert.That(typeof(AccountingApplicationContext).GetField("sessionTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context), Is.Not.Null);
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.PasswordChange));
    }

    [Test]
    public void StartsWithOnlyUnauthenticatedLogin()
    {
        int baseline = Application.OpenForms.Count;
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        Assert.That(context.IsAuthenticated, Is.False);
        Assert.That(context.Username, Is.Null);
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.Login));
        Assert.That(context.MainForm, Is.TypeOf<Giris>());
        Assert.That(Application.OpenForms.Count, Is.EqualTo(baseline + 1));
    }

    [Test]
    public async Task SuccessfulLoginDisposesLoginAndOpensOneDashboard()
    {
        int baseline = Application.OpenForms.Count;
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        var login = (Giris)context.MainForm!;
        Assert.That(await login.TrySignInAsync(" admin ", "secret"), Is.True);
        Assert.That(login.IsDisposed, Is.True);
        Assert.That(context.IsAuthenticated, Is.True);
        Assert.That(context.Username, Is.EqualTo("admin"));
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.Dashboard));
        Assert.That(Application.OpenForms.Count, Is.EqualTo(baseline + 1));
    }

    [Test]
    public async Task FailedLoginCannotOpenProtectedScreens()
    {
        var screens = new Screens { Authenticate = (_, _) => Task.FromResult(false) };
        using var context = new AccountingApplicationContext(screens.Create);
        Form login = context.MainForm!;
        Assert.That(await ((Giris)login).TrySignInAsync("admin", "wrong password"), Is.False);
        Assert.That(context.MainForm, Is.SameAs(login));
        Assert.That(context.IsAuthenticated, Is.False);
        Assert.That(context.Navigate(AppPage.Admin), Is.False);
    }

    [TestCase(AppPage.Dashboard)]
    [TestCase(AppPage.General)]
    [TestCase(AppPage.Joker)]
    [TestCase(AppPage.Search)]
    [TestCase(AppPage.Summary)]
    [TestCase(AppPage.Contact)]
    [TestCase(AppPage.Admin)]
    public void ProtectedNavigationRequiresLogin(AppPage page)
    {
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        Assert.That(context.Navigate(page), Is.False);
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.Login));
        Assert.That(screens.Created.Count, Is.EqualTo(1));
    }

    [Test]
    public void RepeatedNavigationDisposesEveryPreviousWindowWithoutEndingTheLoop()
    {
        int baseline = Application.OpenForms.Count;
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        int threadExits = 0;
        context.ThreadExit += (_, _) => threadExits++;
        context.StartSession("admin");
        for (int round = 0; round < 10; round++)
        {
            foreach (AppPage page in new[] { AppPage.Admin, AppPage.General, AppPage.Joker,
                AppPage.Search, AppPage.Summary, AppPage.Contact, AppPage.Dashboard })
            {
                Form previous = context.MainForm!;
                Assert.That(context.Navigate(page), Is.True);
                Assert.That(previous.IsDisposed, Is.True);
                Assert.That(Application.OpenForms.Count, Is.EqualTo(baseline + 1));
                Assert.That(screens.Created.Count(form => !form.IsDisposed), Is.EqualTo(1));
            }
        }
        Assert.That(threadExits, Is.EqualTo(0));
    }

    [TestCase(AppPage.General)]
    [TestCase(AppPage.Joker)]
    [TestCase(AppPage.Search)]
    [TestCase(AppPage.Summary)]
    [TestCase(AppPage.Contact)]
    [TestCase(AppPage.Admin)]
    public void BackReturnsToDashboardAndDisposesTheModule(AppPage page)
    {
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("admin");
        context.Navigate(page);
        var module = (TestScreen)context.MainForm!;
        module.GoBack();
        Assert.That(module.IsDisposed, Is.True);
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.Dashboard));
        Assert.That(context.Username, Is.EqualTo("admin"));
    }

    [TestCase(AppPage.Dashboard)]
    [TestCase(AppPage.General)]
    [TestCase(AppPage.Joker)]
    [TestCase(AppPage.Search)]
    [TestCase(AppPage.Summary)]
    [TestCase(AppPage.Contact)]
    [TestCase(AppPage.Admin)]
    public void LogoutDisposesProtectedScreenClearsSessionAndCreatesBlankLogin(AppPage page)
    {
        int baseline = Application.OpenForms.Count;
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("admin");
        context.Navigate(page);
        var oldScreen = (TestScreen)context.MainForm!;
        oldScreen.SignOut();
        Assert.That(oldScreen.IsDisposed, Is.True);
        Assert.That(context.Username, Is.Null);
        Assert.That(context.IsAuthenticated, Is.False);
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.Login));
        var login = (Giris)context.MainForm!;
        Assert.That(LoginField(login, "username_txt").Text, Is.Empty);
        Assert.That(LoginField(login, "password_txt").Text, Is.Empty);
        Assert.That(context.Navigate(AppPage.Admin), Is.False);
        Assert.That(Application.OpenForms.Count, Is.EqualTo(baseline + 1));
        Assert.That(screens.Created.Count(form => !form.IsDisposed), Is.EqualTo(1));
    }

    [Test]
    public async Task UserCanSignInAgainAfterLogout()
    {
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        await ((Giris)context.MainForm!).TrySignInAsync("first-user", "secret");
        context.Logout();
        await ((Giris)context.MainForm!).TrySignInAsync("second-user", "other secret");
        Assert.That(context.Username, Is.EqualTo("second-user"));
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.Dashboard));
    }

    [Test]
    public void LogoutAlsoClosesOwnedChildWindows()
    {
        int baseline = Application.OpenForms.Count;
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("admin");
        using var child = new TestScreen();
        child.Show(context.MainForm);
        context.Logout();
        Assert.That(child.IsDisposed, Is.True);
        Assert.That(context.IsAuthenticated, Is.False);
        Assert.That(Application.OpenForms.Count, Is.EqualTo(baseline + 1));
    }

    [TestCase(AppPage.Login)]
    [TestCase(AppPage.Dashboard)]
    [TestCase(AppPage.Admin)]
    public void ClosingCurrentWindowEndsSessionAndMessageLoop(AppPage page)
    {
        int baseline = Application.OpenForms.Count;
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        int exits = 0;
        context.ThreadExit += (_, _) => exits++;
        if (page != AppPage.Login)
        {
            context.StartSession("admin");
            context.Navigate(page);
        }
        Form current = context.MainForm!;
        current.Close();
        Assert.That(current.IsDisposed, Is.True);
        Assert.That(context.MainForm, Is.Null);
        Assert.That(context.IsAuthenticated, Is.False);
        Assert.That(context.CurrentPage, Is.Null);
        Assert.That(exits, Is.EqualTo(1));
        Assert.That(Application.OpenForms.Count, Is.EqualTo(baseline));
    }

    [Test]
    public void DisposedWindowsCannotNavigateOrLogOutANewSession()
    {
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("first-user");
        var stale = (TestScreen)context.MainForm!;
        context.Logout();
        context.StartSession("second-user");
        Form current = context.MainForm!;
        stale.Go(AppPage.Admin);
        stale.SignOut();
        Assert.That(context.MainForm, Is.SameAs(current));
        Assert.That(context.Username, Is.EqualTo("second-user"));
    }

    [Test]
    public void FactoryFailureLeavesCurrentScreenUsable()
    {
        var screens = new Screens { FailCreation = AppPage.Admin };
        using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("admin");
        Form dashboard = context.MainForm!;
        Assert.Throws<InvalidOperationException>(() => context.Navigate(AppPage.Admin));
        Assert.That(context.MainForm, Is.SameAs(dashboard));
        Assert.That(dashboard.IsDisposed, Is.False);
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.Dashboard));
        Assert.That(context.IsAuthenticated, Is.True);
        Assert.That(context.Navigate(AppPage.General), Is.True);
    }

    [Test]
    public void FailedLogoutCannotLeaveAProtectedScreenOpen()
    {
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("admin");
        Form dashboard = context.MainForm!;
        screens.FailCreation = AppPage.Login;
        Assert.Throws<InvalidOperationException>(() => context.Logout());
        Assert.That(dashboard.IsDisposed, Is.True);
        Assert.That(context.MainForm, Is.Null);
        Assert.That(context.IsAuthenticated, Is.False);
    }

    [Test]
    public void ShowFailureDisposesBothScreensAndEndsTheSession()
    {
        var screens = new Screens { FailShow = AppPage.Admin };
        using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("admin");
        Assert.Throws<InvalidOperationException>(() => context.Navigate(AppPage.Admin));
        Assert.That(context.MainForm, Is.Null);
        Assert.That(context.IsAuthenticated, Is.False);
        Assert.That(screens.Created.All(form => form.IsDisposed), Is.True);
    }

    [Test]
    public void FailedLoginScreenShowDuringLogoutEndsTheLoopOnlyOnce()
    {
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("admin");
        screens.FailShow = AppPage.Login;
        int exits = 0;
        context.ThreadExit += (_, _) => exits++;
        Assert.Throws<InvalidOperationException>(() => context.Logout());
        context.ExitThread();
        Assert.That(exits, Is.EqualTo(1));
        Assert.That(context.MainForm, Is.Null);
        Assert.That(context.IsAuthenticated, Is.False);
    }

    [Test]
    public async Task LateAuthenticationCannotReopenAClosedApplication()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var screens = new Screens { Authenticate = (_, _) => completion.Task };
        using var context = new AccountingApplicationContext(screens.Create);
        var login = (Giris)context.MainForm!;
        Task<bool> attempt = login.TrySignInAsync("admin", "secret");
        Assert.That(await login.TrySignInAsync("admin", "secret"), Is.False);
        login.Close();
        completion.SetResult(true);
        Assert.That(await attempt, Is.False);
        Assert.That(context.MainForm, Is.Null);
        Assert.That(context.IsAuthenticated, Is.False);
        Assert.That(screens.Created.Count, Is.EqualTo(1));
    }

    [Test]
    public void RepeatedCurrentPageRequestDoesNotCreateAnotherWindow()
    {
        var screens = new Screens();
        using var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("admin");
        Form dashboard = context.MainForm!;
        Assert.That(context.Navigate(AppPage.Dashboard), Is.True);
        Assert.That(context.MainForm, Is.SameAs(dashboard));
        Assert.That(screens.Created.Count, Is.EqualTo(2));
    }

    [Test]
    public void DisposingContextClosesTheRemainingWindow()
    {
        int baseline = Application.OpenForms.Count;
        var screens = new Screens();
        var context = new AccountingApplicationContext(screens.Create);
        context.StartSession("admin");
        context.Navigate(AppPage.Admin);
        context.Dispose();
        Assert.That(screens.Created.All(form => form.IsDisposed), Is.True);
        Assert.That(context.Navigate(AppPage.Dashboard), Is.False);
        Assert.That(Application.OpenForms.Count, Is.EqualTo(baseline));
    }

    [TestCase("button4", AppPage.Admin)]
    [TestCase("button1", AppPage.General)]
    [TestCase("button2", AppPage.Joker)]
    [TestCase("button3", AppPage.Search)]
    [TestCase("button5", AppPage.Summary)]
    [TestCase("button6", AppPage.Contact)]
    public void ActualDashboardButtonsRequestTheCorrectPage(string fieldName, AppPage expected)
    {
        using var dashboard = new Dashboard { Session = new("admin", UserRole.Admin), ShowInTaskbar = false, Opacity = 0 };
        AppPage? requested = null;
        dashboard.PageRequested += (_, e) => requested = e.Page;
        dashboard.Show();
        var button = (Button)typeof(Dashboard).GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dashboard)!;
        button.PerformClick();
        Assert.That(requested, Is.EqualTo(expected));
    }

    [Test]
    public void ActualDashboardShowsUsernameAndHasAWorkingLogoutButton()
    {
        using var dashboard = new Dashboard { Session = new("admin", UserRole.Admin), ShowInTaskbar = false, Opacity = 0 };
        dashboard.SetUsername("admin");
        int logouts = 0;
        dashboard.LogoutRequested += (_, _) => logouts++;
        dashboard.Show();
        var button = (Button)typeof(Dashboard).GetField("logout_btn",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dashboard)!;
        var label = (Label)typeof(Dashboard).GetField("userLabel",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dashboard)!;
        button.PerformClick();
        Assert.That(logouts, Is.EqualTo(1));
        Assert.That(label.Text, Does.Contain("admin"));
    }
}
