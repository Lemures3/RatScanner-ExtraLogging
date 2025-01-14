﻿using Microsoft.Win32;
using SingleInstanceCore;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;

namespace RatScanner;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application, ISingleInstance
{
	private readonly string[] _webview2RegKeys = new[]
	{
		@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
		@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
		@"HKEY_CURRENT_USER\Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
	};

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		// Set current working directory to executable location
		Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

#if !DEBUG
		SetupExceptionHandling();
#endif

		// Install webview2 runtime if it is not already
		var existing = _webview2RegKeys.Any(key => Registry.GetValue(key, "pv", null) != null);
		if (!existing) InstallWebview2Runtime();

		// Setup single instance mode
		var guid = "{a057bb64-c126-4ef4-a4ed-3037c2e7bc89}";
		var isFirstInstance = this.InitializeAsFirstInstance(guid);
		if (!isFirstInstance)
		{
			SingleInstance.Cleanup();
			Current.Shutdown(2);
		}
	}

	public void OnInstanceInvoked(string[] args)
	{
		Current.Dispatcher.Invoke(() =>
		{
			MainWindow.Activate();
			MainWindow.WindowState = WindowState.Normal;

			// Invert the topmost state twice to bring
			// the window on top if it wasn't already
			MainWindow.Topmost = !MainWindow.Topmost;
			MainWindow.Topmost = !MainWindow.Topmost;
		});
	}

	private void InstallWebview2Runtime()
	{
		using var client = new WebClient();
		client.DownloadFile("https://go.microsoft.com/fwlink/p/?LinkId=2124703", "MicrosoftEdgeWebview2Setup.exe");

		var startInfo = new ProcessStartInfo();
		startInfo.CreateNoWindow = false;
		startInfo.UseShellExecute = false;
		startInfo.FileName = "MicrosoftEdgeWebview2Setup.exe";
		startInfo.WindowStyle = ProcessWindowStyle.Hidden;
		startInfo.Arguments = "/install";

		try
		{
			// Start the process with the info we specified.
			// Call WaitForExit and then the using statement will close.
			var exeProcess = Process.Start(startInfo);
			exeProcess.WaitForExit();
		}
		catch (Exception ex)
		{
			Logger.LogError("Could not install Webview2", ex);
		}

		try
		{
			File.Delete("MicrosoftEdgeWebview2Setup.exe");
		}
		catch { }
	}

	private void SetupExceptionHandling()
	{
		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
		{
			LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");
		};

		DispatcherUnhandledException += (s, e) =>
		{
			LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");
			e.Handled = true;
		};

		TaskScheduler.UnobservedTaskException += (s, e) =>
		{
			LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
			e.SetObserved();
		};
	}

	private void LogUnhandledException(Exception exception, string source)
	{
		exception.Data.Add("Source", source);
		Logger.LogError(exception);
	}
}
