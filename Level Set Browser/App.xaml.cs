using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Data;
using System.Xml;
using System.Configuration;

namespace Level_Set_Browser
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private void LSStartup(Object sender, StartupEventArgs e)
		{
			LSBrowser browser = new LSBrowser();
			browser.Show();
			
			if (e.Args.Length == 1) browser.OpenFile(e.Args[0]);

			if (e.Args.Length > 1) foreach(string arg in e.Args) {
				if(arg == e.Args[0]) browser.OpenFile(arg);
				else Open(arg);
			}
		}
		
		public void Open(string filePath)
		{
			Process.Start(
				Process.GetCurrentProcess().ProcessName,
				'\"' + filePath + '\"');
		}
		
	}
}