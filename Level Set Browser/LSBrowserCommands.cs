/*
 * Created by SharpDevelop.
 * User: tyler.gorton
 * Date: 9/29/2013
 * Time: 12:25 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using Microsoft.Win32;

namespace Level_Set_Browser
{
	/// <summary>
	/// Description of LSBrowserCommands.
	/// </summary>
	public partial class LSBrowser
	{
		public void OpenFile(string filePath)
		{
			string pathExt = Path.GetExtension(filePath);
			if(pathExt != ".lss" && pathExt != ".lsm") {
				MessageBox.Show("File extension must be either \".lss\" or \".lsm\".");
				return;
			}
			if(!File.Exists(filePath)) return;
			
			if(Root != null) {
				if((Root.DataStream as FileStream).Name == filePath) return;
				(Application.Current as App).Open(filePath);
				return;
			}
			
			FileStream lsFileStream;
			try {
				lsFileStream = new FileStream(filePath, FileMode.Open);
				
			} catch (Exception e) {
				MessageBox.Show("File error:\n" + e.Message);
				return;
			}
			
			this.Title = "Level Set Browser - " + Path.GetFileNameWithoutExtension(lsFileStream.Name);
			
			Root = new LSRoot(this, lsFileStream);
			LSTree.ItemsSource = Root.Children;
		}
		
		void Open_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			//open file dialog .lsm,.lss only; multiselect off by default
			OpenFileDialog LSOpenFile = new OpenFileDialog();
			LSOpenFile.Title = "Select Text File";
			LSOpenFile.Filter = "Level Set (*.lss, *.lsm)|*.lss;*.lsm";
			
			Nullable<bool> dlgResult = LSOpenFile.ShowDialog();
			if(!dlgResult.HasValue || dlgResult == false) return;
			
			//if a file is open in this window invoke App.Open()
			OpenFile(LSOpenFile.FileName);
		}
		
		void Close_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			//prompt for save
			foreach(Window childWin in OwnedWindows) childWin.Close();
			
			//close file stream
			LSTree.ItemsSource = null;
			Root.DataStream.Close();
			Root.DataStream.Dispose();
			Root = null;
			
			//reset window values
			this.Title = "Level Set Browser";
			LSMessage.Text = "";
			LSProgress.Maximum = 1;
			LSProgress.Value = 0;
			LSProgAmt.Text = "";
		}
		
		void Save_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			//adjust headers & compress sections
			//save to file
		}
		
		void Exit_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			//prompt to save
			//close file streams
			
			//save context: open data view windows and their modes for reopen
			
			//close open windows
			//close application
			Application.Current.Shutdown();
		}
		
		void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
	}
	
	/// <summary>
	/// Commands
	/// </summary>
	public static class LSBrowserCommands
	{
		public static readonly RoutedUICommand Exit = new RoutedUICommand
		(
			"Exit", "Exit", typeof(LSBrowserCommands), 
			new InputGestureCollection() {new KeyGesture(Key.Q, ModifierKeys.Control)}
		);
		
		public static readonly RoutedUICommand New = new RoutedUICommand
		(
			"New", "New", typeof(LSBrowserCommands), 
			new InputGestureCollection() {new KeyGesture(Key.N, ModifierKeys.Control)}
		);
		
		public static readonly RoutedUICommand Close = new RoutedUICommand
		(
			"Close", "Close", typeof(LSBrowserCommands), 
			new InputGestureCollection() {new KeyGesture(Key.F4, ModifierKeys.Control)}
		);
		
		public static readonly RoutedUICommand UpperCase = new RoutedUICommand
		(
			"UpperCase", "UpperCase", typeof(LSBrowserCommands), 
			new InputGestureCollection() {new KeyGesture(Key.U, ModifierKeys.Control | ModifierKeys.Shift)}
		);
	}
	
}
