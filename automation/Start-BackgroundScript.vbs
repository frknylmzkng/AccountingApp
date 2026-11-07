Option Explicit
Dim files, shell, script, powershell, result
If WScript.Arguments.Count <> 1 Then WScript.Quit 2
Set files = CreateObject("Scripting.FileSystemObject")
script = files.GetAbsolutePathName(WScript.Arguments(0))
If Not files.FileExists(script) Then WScript.Quit 3
If LCase(files.GetExtensionName(script)) <> "ps1" Then WScript.Quit 4
Set shell = CreateObject("WScript.Shell")
powershell = shell.ExpandEnvironmentStrings("%SystemRoot%") & "\System32\WindowsPowerShell\v1.0\powershell.exe"
' GUI host hides the console before PowerShell is created, rather than hiding it after startup.
result = shell.Run("""" & powershell & """ -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File """ & script & """", 0, True)
WScript.Quit result
