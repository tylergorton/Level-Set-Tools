/*
 * Created by SharpDevelop.
 * User: tyler.gorton
 * Date: 10/8/2013
 * Time: 12:21 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Data;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Level_Set_Browser
{
	/// <summary>
	/// Interaction logic for DataView.xaml
	/// </summary>
	public partial class DataView : Window
	{
		public LSDataBlock Source;
		byte[] data;
		uint[] numData;
		DataTable tableData;
		
		public DataView(Stream source, int size)
		{
			InitializeComponent();
			
			BinaryReader reader = new BinaryReader(source);
			data = reader.ReadBytes(size);
			
			DataTextBox.Text = BitConverter.ToString(data).Replace('-',' ');
			//DataList.ItemsSource = DataBox.Text.Split(' ');
			
			int intSize = sizeof(uint);
			numData = new uint[size/intSize];
			for(int index = 0; index < size; index += intSize)
				numData[index / intSize] = BitConverter.ToUInt32(data, index);
			
			DataListBox.ItemsSource = numData;
			
			int columns = (int)numData[0];
			int rows = (int)numData[1];
			int offset = 8;
			
			tableData = new DataTable("LSDATA");
			
			int tooMuch = (int)Math.Pow(2, 16);
			if(columns > tooMuch || rows > tooMuch || columns*rows > data.Length || columns > 256 || columns == 0) {
				columns = 8;
				rows = data.Length / 8;
				offset = 0;
			}
			
			int column = 0;
			while(column < columns) {
				DataColumn newColumn = new DataColumn(column.ToString(), typeof(byte));
				tableData.Columns.Add();
				column++;
			}
			
			int row = 0;
			while(row < rows) {
				DataRow newRow = tableData.NewRow();
				column = 0;
				while(column < columns) {
					newRow[column] = data[row*columns + column + offset];
					column++;
				}
				tableData.Rows.Add(newRow);
				row++;
			}
			
			LSDataTable.ItemsSource = tableData.DefaultView;
		}
		
		void DataView_Closed(object sender, EventArgs e)
		{
			Source.dataViewer = null;
		}
	}
}