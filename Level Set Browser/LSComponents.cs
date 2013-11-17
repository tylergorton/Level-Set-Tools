/*
 * Created by SharpDevelop.
 * User: tyler.gorton
 * Date: 09/27/2013
 * Time: 22:33
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.IO;
using System.Threading;
using System.ComponentModel;

using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Level_Set_Browser
{	
	public static class LSUtils
	{
		/// <summary>
		/// Parses data block
		/// </summary>
		/// <param name="fs"></param>
		/// <param name="size"></param>
		/// <returns>header int array if valid</returns>
		public static uint[] IsHeader(Stream ds, uint size)
		{
			BinaryReader lsReader = new BinaryReader(ds);
			
			//large enough to contain count and size is multiple of 8 bytes
			if(size < sizeof(uint) || (size % (sizeof(uint) * 2)) != 0) return null;
			uint sectionCount = lsReader.ReadUInt32();
			
			//section count too large?
			if(sectionCount > uint.MaxValue/16) return null;
			
			//large enough to contain reported number of pointers?
			//32bit unsigned * number of sections + section count + end pointer + padding to 8 byte boundary
			uint headSize = sizeof(uint) * (sectionCount + 2 + sectionCount % 2); 
			if(size < headSize) return null;
			
			//first pointer points just past head?
			uint firstPointer = lsReader.ReadUInt32();
			if(firstPointer != headSize) return null;
			
			uint[] header = new uint[sectionCount + 2];
			header[0] = sectionCount;
			header[1] = firstPointer;
			
			for(uint index = 2; index < sectionCount + 2; index++) {
				header[index] = lsReader.ReadUInt32();
			}
			//final pointer end of section pointer?
			if(header[header.Length - 1] != size) return null;
			
			return header;
		}
	}
	
	//____________________________________________________________________________
	/// <summary>
	/// Description of LSItem
	/// </summary>
	public class LSItem : INotifyPropertyChanged
	{
		//Properties
		public string Name {set; get;}
		public LSSection Parent {set; get;}
		public uint Offset {set; get;}
		public uint Size {set; get;}
		
		//for controlling bound treeview item
		private bool expanded;
		public bool Expanded {
			get{return expanded;}
			set{
				if(value != expanded) {
					expanded = value;
					NotifyPropertyChanged("Expanded");
				}
			}
		}
		private bool selected;
		public bool Selected {
			get{return selected;}
			set{
				if(value != selected) {
					selected = value;
					NotifyPropertyChanged("Selected");
				}
			}
		}
		
		
		public event PropertyChangedEventHandler PropertyChanged;
		public void NotifyPropertyChanged(string propName)
		{
			if(this.PropertyChanged != null)
				this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
		}
		
		//Constructors
		private LSItem() {}
		
		public LSItem(LSSection parent, string name, uint offset, uint size)
		{
			Parent = parent;
			Name = name;
			Offset = offset;
			Size = size;
		}
		
		//override for treeview expand event
		public virtual void Load() {}
	}
	
	//____________________________________________________________________________
	/// <summary>
	/// Description of LSRoot
	/// </summary>
	public class LSRoot : LSSection
	{
		//State Variables
		public LSBrowser Browser;
		
		//Constructors
		public LSRoot(LSBrowser browser, FileStream fileStream) :
			base(null, "Root", 0, (uint)fileStream.Length, 
			     LSUtils.IsHeader(fileStream, (uint)fileStream.Length))
		{
			Browser = browser;
			DataStream = fileStream;
			
			Load();
		}
	}
	
	//____________________________________________________________________________
	/// <summary>
	/// Description of LSPlaceholder
	/// </summary>
	public class LSPlaceHolder : LSItem
	{
		public uint[] Pointers {get; set;}
		
		public LSPlaceHolder(LSSection parent, uint[] lsHeader) :
			base(parent, "Loading...", 0, 0)
		{
			Pointers = lsHeader;
		}
	}
	
	//____________________________________________________________________________
	/// <summary>
	/// Description of RNCPlaceholder
	/// </summary>
	public class RNCPlaceHolder : LSItem
	{
		public RNCHeader Header { get; set; }
		
		public RNCPlaceHolder(LSSection parent, RNCHeader head) :
			base(parent, "Unpacking...", 0, 0)
		{
			Header = head;
		}
	}
	
	//____________________________________________________________________________
	/// <summary>
	/// Description of LSDataBlock
	/// </summary>
	public class LSDataBlock : LSItem
	{
		//Properties
		//Data stream to hold modified data or byte[] array
		public byte[] Data {get; set;}
		public DataView dataViewer;
		
		//Constructors
		public LSDataBlock(LSSection parent, string name, uint offset, uint size) :
			base(parent, name, offset, size)
		{
			//Data = new byte[size];
		}
		
		public override void Load()
		{
			if(dataViewer != null) {
				dataViewer.Activate();
				dataViewer.Focus();
				return;
			}
			//resolve offset
			uint offset = Offset;
			LSSection ancestor = Parent;
			
			while(!(ancestor is LSRoot)) {
				if(ancestor.DataStream != null) break;
				offset += ancestor.Offset;
				ancestor = ancestor.Parent;
			}
			//get stream
			Stream lsStream = ancestor.DataStream;
			lsStream.Position = offset;
			
			while(!(ancestor is LSRoot)) ancestor = ancestor.Parent;
			LSRoot root = ancestor as LSRoot;
			
			//load new Data View window
			dataViewer = new DataView(lsStream, (int)Size);
			dataViewer.Source = this;
			dataViewer.Title = Name;
			dataViewer.Owner = root.Browser;
			dataViewer.Show();
		}
	}
	
	//____________________________________________________________________________
	/// <summary>
	/// Description of LSSection
	/// </summary>
	public class LSSection : LSItem
	{
		//Properties
		public Stream DataStream;
		public ObservableCollection<LSItem> Children {set; get;}
		
		//Constructors
		public LSSection(LSSection parent, string name, uint offset, uint size, uint[] head) :
			base(parent, name, offset, size)
		{
			Children = new ObservableCollection<LSItem>();
			Children.Add(new LSPlaceHolder(this, head));
		}
		public LSSection(LSSection parent, string name, uint offset, uint size, RNCHeader head) :
			base(parent, name, offset, size)
		{
			Children = new ObservableCollection<LSItem>();
			Children.Add(new RNCPlaceHolder(this, head));
		}
		
		//Methods
		public void Kill(Dispatcher dispatch) 
		{
			dispatch.BeginInvoke(DispatcherPriority.Background, 
				                new KillChildrenDelegate(KillChildren));
		}
		public delegate void KillChildrenDelegate();
		public void KillChildren()
		{
			Children.Clear();
		}
		
		public void Add(Dispatcher dispatch, LSItem child) 
		{
			dispatch.BeginInvoke(DispatcherPriority.Background, 
				                new AddChildDelegate(AddChild), child);
		}
		public delegate void AddChildDelegate(LSItem child);
		public void AddChild(LSItem child)
		{
			Children.Add(child);
		}
		
		public void Remove(Dispatcher dispatch, LSItem child)
		{
			dispatch.BeginInvoke(DispatcherPriority.Background, 
			                new RemoveChildDelegate(RemoveChild), child);
		}
		public delegate void RemoveChildDelegate(LSItem child);
		public void RemoveChild(LSItem child)
		{
			Children.Remove(child);
		}
		
		//asynchronous load startpoint
		public override void Load()
		{
			Thread loadThread = null;
			//check for RNC placeholder
			if(Children[0] is RNCPlaceHolder) 
				loadThread = new Thread(LoadRNC);
			
			//check for LS placeholder
			else if(Children[0] is LSPlaceHolder) 
				loadThread = new Thread(LoadSections);
			
			if(loadThread != null) loadThread.Start();
		}
		
		//COMMON LOAD RESOURCES:
			//resolve offset
			//get:
			// data stream
			// root
			// browser
			// dispatcher
			
		private void LoadRNC()
		{
			//resolve offset
			uint offset = 0;
			LSSection ancestor = this;
			
			//drill down to a stream
			while(!(ancestor is LSRoot)) {
				offset += ancestor.Offset;
				if(ancestor.DataStream != null) break;
				ancestor = ancestor.Parent;
			}
			
			Stream lsStream = ancestor.DataStream;
			lsStream.Seek(offset + 18, SeekOrigin.Begin);
			
			//drill dwn to the root
			while(!(ancestor is LSRoot)) ancestor = ancestor.Parent;
			LSRoot root = ancestor as LSRoot;
			
			LSBrowser browser = root.Browser;
			Dispatcher dispatch = browser.Dispatcher;
			
			RNCPlaceHolder placeHolder = Children[0] as RNCPlaceHolder;
			Remove(dispatch, placeHolder);
			
			RNCHeader head = placeHolder.Header;
			DataStream = new MemoryStream((int)head.UnpackSize);
			
			bool unpacked = RNCUtils.Unpack(lsStream, DataStream, head);
			if(!unpacked) {
				Add(dispatch, placeHolder); //reinsert RNC placeholder
				return;
			}
			
			DataStream.Position = 0;
			uint[] lsHead = LSUtils.IsHeader(DataStream, head.UnpackSize);
			
			if(lsHead != null) {
				//new section with placeholder
				LSPlaceHolder section = 
					new LSPlaceHolder(this, lsHead);
				Add(dispatch, section); //wait for add
				
				while (!Children.Contains(section));
				
				LoadSections();
			} else {
				//else add LSDataBlock
				LSDataBlock data = 
					new LSDataBlock(this, "Data Block", 0, head.UnpackSize);
				Add(dispatch, data);
			}
			
		}
		
		//create sections from temporary header data
		private void LoadSections()
		{
			//resolve offset
			uint offset = 0;
			LSSection ancestor = this;
			
			while(!(ancestor is LSRoot)) {
				if(ancestor.DataStream != null) break;
				offset += ancestor.Offset;
				ancestor = ancestor.Parent;
			}
			//get stream
			Stream lsStream = ancestor.DataStream;
			
			while(!(ancestor is LSRoot)) ancestor = ancestor.Parent;
			LSRoot root = ancestor as LSRoot;
			
			LSBrowser browser = root.Browser;
			Dispatcher dispatch = browser.Dispatcher;
			
			LSPlaceHolder placeHolder = Children[0] as LSPlaceHolder;
			
			Remove(dispatch, placeHolder);
			uint[] header = placeHolder.Pointers;
			
			if(header.Length == 2) {
				Add(dispatch, new LSItem(this, "Empty", 0, 0));
				return;
			}
			
			//do this on UI thread
			browser.ProgressLoadAmount(header[0]);
			
			//load for each placeholder section
			for(int index = 1; index <= header[0]; index++) {
				
				LSItem newItem;
				
				uint pointer = header[index];
				uint size = header[index + 1] - pointer;
				
				//test for header data
				lsStream.Seek(offset + pointer, SeekOrigin.Begin);
				uint[] lsHead = LSUtils.IsHeader(lsStream, size);
				if(lsHead != null) {
					
					//new section with placeholder
					LSSection section = 
						new LSSection(this, "Section " + (index - 1).ToString(), pointer, size, lsHead);
					newItem = section;
					
				} else {
					//test for RNC
					lsStream.Seek(offset + pointer, SeekOrigin.Begin);
					RNCHeader rncHead = RNCUtils.IsHeader(lsStream, size);
					if(rncHead != null) {
						string name = rncHead.Identifier.ToString() + " RNC Section " + (index - 1).ToString();
						LSSection rnc = new LSSection(this, name, pointer, size, rncHead);
						
						//if(rncHead.PackChecksum == RNCUtils.CheckSum(lsStream, rncHead.PackSize))
						//	rnc.Name += " checksum verified!";
						
						newItem = rnc;
						
					} else {
						//data block
						LSDataBlock data = 
							new LSDataBlock(this, "Data Block " + (index - 1).ToString(), pointer, size);
						newItem = data;
					}
				}
				
				Add(dispatch, newItem);
				
				//do this on UI thread
				browser.ProgressIncreaseAmount(1);
			}
		}
		
	}
	
}
