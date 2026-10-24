using System.ComponentModel;
using System.Reflection;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
public class LoginPreparationTests
{
    private static T Field<T>(Giris form, string name) => (T)typeof(Giris).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;

    [Test]
    public async Task PendingPreparationExplainsWhyLoginIsDisabledAndEnablesItOnCompletion()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var form = new Giris(() => completion.Task, (_, _) => Task.FromResult(true));
        Task<bool> preparation = form.PrepareCredentialsAsync();
        Assert.That(Field<Button>(form, "giris_btn").Enabled, Is.False);
        Assert.That(Field<Label>(form, "statusLabel").Text, Does.Contain("Giriş hazırlanıyor"));
        Assert.That(await form.TrySignInAsync("admin", "secret"), Is.False);
        completion.SetResult();
        Assert.That(await preparation, Is.True);
        Assert.That(Field<Button>(form, "giris_btn").Enabled, Is.True);
        Assert.That(Field<Label>(form, "statusLabel").Text, Does.Contain("Giriş hazır"));
    }

    [Test]
    public async Task FailedPreparationCanBeRetriedWithoutClosingTheApplication()
    {
        int preparations = 0;
        using var form = new Giris(() => ++preparations == 1
            ? Task.FromException(new InvalidOperationException("sensitive database detail"))
            : Task.CompletedTask, (_, _) => Task.FromResult(true));
        Field<TextBox>(form, "username_txt").Text = "admin";
        Field<TextBox>(form, "password_txt").Text = "secret";
        Assert.That(await form.PrepareCredentialsAsync(), Is.False);
        Assert.That(form.LastFailure, Is.Not.Null);
        Assert.That(Field<Button>(form, "giris_btn").Enabled, Is.True);
        Assert.That(Field<Button>(form, "giris_btn").Text, Is.EqualTo("YENİDEN DENE"));
        Assert.That(Field<TextBox>(form, "password_txt").Text, Is.EqualTo("secret"));
        Assert.That(Field<Label>(form, "statusLabel").Text, Does.Not.Contain("sensitive"));
        Assert.That(await form.PrepareCredentialsAsync(), Is.True);
        Assert.That(form.LastFailure, Is.Null);
        Assert.That(await form.TrySignInAsync("admin", "secret"), Is.True);
        Assert.That(preparations, Is.EqualTo(2));
    }

    [Test]
    public async Task PreparationFailureDoesNotAuthenticateOrReportWrongPassword()
    {
        int authentications = 0;
        using var form = new Giris(() => Task.FromException(new IOException("private path")),
            (_, _) => { authentications++; return Task.FromResult(true); });
        Assert.That(await form.TrySignInAsync("admin", "secret"), Is.False);
        Assert.That(authentications, Is.Zero);
        Assert.That(form.LastFailure, Is.Not.Null);
        Assert.That(Field<Label>(form, "statusLabel").Text, Does.Not.Contain("şifre hatalı"));
    }

    [Test]
    public async Task AuthenticationInfrastructureFailureAllowsAnotherAttempt()
    {
        int calls = 0;
        using var form = new Giris(() => Task.CompletedTask, (_, _) => ++calls == 1
            ? Task.FromException<bool>(new IOException("private path")) : Task.FromResult(true));
        Assert.That(await form.TrySignInAsync("admin", "secret"), Is.False);
        Assert.That(form.LastFailure, Is.Not.Null);
        Assert.That(Field<Button>(form, "giris_btn").Enabled, Is.True);
        Assert.That(await form.TrySignInAsync("admin", "secret"), Is.True);
        Assert.That(form.LastFailure, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LatePreparationDoesNotTouchADisposedForm(bool fail)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var form = new Giris(() => completion.Task, (_, _) => Task.FromResult(true));
        Task<bool> preparation = form.PrepareCredentialsAsync();
        form.Dispose();
        if (fail) completion.SetException(new IOException("late failure"));
        else completion.SetResult();
        Assert.That(await preparation, Is.False);
        Assert.That(form.IsDisposed, Is.True);
    }

    [Test]
    public void DiagnosticsDoNotStoreExceptionMessagesWithCredentials()
    {
        string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "LoginDiagnostics_" + Guid.NewGuid().ToString("N"));
        const string sensitive = "password=secret; account=private; hash=private";
        var failure = LoginDiagnostics.Record(new IOException(sensitive, new Win32Exception(53, sensitive)),
            "prepare", directory);
        string log = File.ReadAllText(Path.Combine(directory, "login-errors.log"));
        Assert.That(log, Does.Contain(failure.Code));
        Assert.That(log, Does.Contain("windows.error=53"));
        Assert.That(log, Does.Not.Contain(sensitive));
        Assert.That(failure.Message, Does.Not.Contain(sensitive));
        Directory.Delete(directory, recursive: true);
    }

    [Test]
    public void UnwritableDiagnosticsDoNotPreventShowingTheFailure()
    {
        string file = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(file, "test");
        try
        {
            Assert.DoesNotThrow(() => LoginDiagnostics.Record(new IOException("private"), "prepare", file));
        }
        finally { File.Delete(file); }
    }
}
