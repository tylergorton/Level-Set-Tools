/*
 * Created by SharpDevelop.
 * User: tyler.gorton
 * Date: 9/19/2013
 * Time: 6:39 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using Microsoft.Win32;

namespace Level_Set_Browser
{
	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>
	public partial class LSBrowser : Window
	{
		public LSRoot Root;
		
		public LSBrowser()
		{
			InitializeComponent();
			
		}
		
		//Events
		void LSTree_Expanded(object sender, RoutedEventArgs e)
		{
			TreeViewItem treeItem = e.OriginalSource as TreeViewItem;
			if(!treeItem.IsSelected) treeItem.IsSelected = true;

			LSItem item = LSTree.SelectedItem as LSItem;
			if(item != null) item.Load();
		}
		
		void LSTree_Collapsed(object sender, RoutedEventArgs e)
		{
			//MessageBox.Show("caught collapsed event");
		}
		
		void LSTree_Selected(object sender, RoutedEventArgs e)
		{
			//MessageBox.Show("caught select event");
		}
		
		void LSTree_Unselected(object sender, RoutedEventArgs e)
		{
			//MessageBox.Show("caught unselect event");
		}
		
		public void ProgressLoadAmount(double amount) 
		{
			this.Dispatcher.BeginInvoke(DispatcherPriority.Background, 
				                new AddMaximumDelegate(AddMaximum), amount);
		}
		private delegate void AddMaximumDelegate(double amount);
		private void AddMaximum(double amount)
		{
			if(amount != 0) {
				LSProgress.Value = 0;
				LSProgress.Maximum = amount;
				LSMessage.Text = "Loading...";
				LSMessage.Foreground = Brushes.Red;
				LSProgAmt.Text = String.Format(
					"{0:P0}", Math.Min(1, LSProgress.Value / LSProgress.Maximum)
				);
			}
		}
		public void ProgressIncreaseAmount(double amount) 
		{
			this.Dispatcher.BeginInvoke(DispatcherPriority.Background, 
				                new AddValueDelegate(AddValue), amount);
		}
		private delegate void AddValueDelegate(double amount);
		private void AddValue(double amount)
		{
			LSProgress.Value += amount;
			double percent = Math.Min(1, LSProgress.Value / LSProgress.Maximum);
			if(percent >= 1) {
				LSMessage.Text = "Complete";
				LSMessage.Foreground = Brushes.Green;
			}
			LSProgAmt.Text = String.Format("{0:P0}", percent);
		}
		
		void DataBlock_KeyUp(object sender, KeyEventArgs e)
		{
			if(e.Key == Key.Enter) MessageBox.Show("ENTER!");
		}
		
	}
	
	/// <summary>
	/// Progress bar level to percentage string
	/// </summary>
	public class ProgressToPercentConverter : IValueConverter
	{
		//Text="{Binding ElementName=LSProgress, Converter={StaticResource ProgressToPercentConverter}}"
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			double percent = 0;
			if(value != null) {
				ProgressBar prog = value as ProgressBar;
				percent = Math.Min(1, prog.Value / prog.Maximum);
			}
			return String.Format("{0:P0}", percent);
		}
		
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return Binding.DoNothing;
		}
	}
	
}