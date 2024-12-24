Dim WinScriptHost
Set WinScriptHost = CreateObject("WScript.Shell")
WinScriptHost.Run Chr(34) & "dotnet run --project ../CortanaClient.csproj" & Chr(34), 0
Set WinScriptHost = Nothing