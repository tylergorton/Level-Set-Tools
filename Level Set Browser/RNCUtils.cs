/*
 * Created by SharpDevelop.
 * User: tyler.gorton
 * Date: 09/27/2013
 * Time: 22:34
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.IO;

using System.Windows;

namespace Level_Set_Browser
{
	
	public enum RNCMethod : uint
	{
		NONE = 0x00434E52,	//R,N,C,0
		SLOW = 0x01434E52,	//R,N,C,1
		FAST = 0x02434E52	//R,N,C,2
	}
	
	public class RNCHeader
	{
		public RNCMethod Identifier;	//must contain 'R', 'N', 'C', method
		public uint UnpackSize;		//unpacked data size
		public uint PackSize;			//packed data size (excludes this header)
		public ushort UnpackChecksum;	//unpacked data checksum
		public ushort PackChecksum;	//packed data checksum
		public byte Leeway;			//not used
		public byte Blocks;			//number of sections
	}
	
	/// <summary>
	/// BitStreamSLOW RNC1 Method
	/// 16 bit right going stream NO PRE-FETCH
	/// </summary>
	public class BitStreamSLOW
	{
		private BinaryReader source;
		private uint bitStream;
		private int buffSize;
		
		public BitStreamSLOW(BinaryReader bitSource)
		{
			source = bitSource;
			bitStream = 0;
			buffSize = 0;
			GetBits(2); //toss first two bits
		}
	
		public uint GetBits(int count)
		{
			if (buffSize - count < 0) {
				bitStream |= (uint)source.ReadUInt16() << buffSize;
				buffSize += 16;
			}	
			
			uint bitMask = (1U << count) - 1;
			uint theBits = bitStream & bitMask;
			bitStream >>= count;
			buffSize -= count;
		
		    return theBits;
		}
		
	}
	
	/// <summary>
	/// HuffNode
	/// </summary>
	public class HuffTree
	{
		private class HuffNode 
		{
			public uint length;		//number of bits in code, zero for end sentinel
			public uint code;		//code bits
			public uint value;		//defined value
		}
		
		private HuffNode[] tree;
		private BitStreamSLOW source;
		
		public HuffTree(BitStreamSLOW bitSource)
		{
			source = bitSource;
			tree = new HuffNode[32];
			
			int index = tree.Length;
			while(--index >= 0)
				tree[index] = new HuffNode();
		}
		
		public void ReadTable()
		{
		    uint i, j, k;
		    uint num;
		    uint[] depth = new uint[32];
		    uint depthmax;
		    uint codeb;
		
		    num = source.GetBits(5);
		    depthmax = 1;
		    for (i = 0; i < num; i++) {
		        depth[i] = source.GetBits(4);
		        if (depthmax < depth[i])
			        depthmax = depth[i];
		    }
		    codeb = 0;
		    k = 0;
		    for (i = 1; i <= depthmax; i++) {
				for (j = 0; j < num; j++)
					if (depth[j] == i) {
						tree[k].length = i;
						tree[k].code = Mirror(codeb, i);
						tree[k].value = j;
						codeb++;
						k++;
					}
					codeb <<= 1;
			}
			if (k < 32) tree[k].length = 0;	//write zero length sentinel
		}
		
		//mirror rightmost length bits of image
		private uint Mirror(uint image, uint length)
		{
			uint reflection = 0;
			length &= 0xF;
			
			for (uint i = 0; i < length; i++) {
				reflection <<= 1;
				if ((image & 1) != 0) reflection += 1;
				image >>= 1;
			}
			
			return reflection;
		}
		
		public int GetValue()
		{
			int node;
			int length = 0;
			int delta;
			uint code = 0;
			uint value;
		
			for (node = 0; node < 32; node++) {
				delta = (int)tree[node].length - length;
				if (delta > 0)
					code |= source.GetBits(delta) << length;
				length = (int)tree[node].length; //next length
				
				if (length == 0) return -1;	//sentinel reached
				if (code == tree[node].code) break;
			}
		
			if (node == 32) return -1; //array exceeded
			
			if (tree[node].value > 1) {
				value = 1U << (int)(tree[node].value - 1);
				value |= source.GetBits((int)tree[node].value - 1);
			} else value = tree[node].value;
			
			return (int)value;
		}
	}
	
	/* 8 bit left going stream
	 * count is zero to initialze
	 */
	public class BitStreamFAST
	{
		private uint bitStream; //low byte only, promoted to uint for C# left shifts
		private BinaryReader source;
		
		public BitStreamFAST(BinaryReader bitSource)
		{
			source = bitSource;
			bitStream = 0;
			GetBits(2);
		}
		
		public ushort GetBits(int count)
		{
		    uint nextBit = 0;
			uint theBits = 0;
			
			while (count-- > 0) {
				nextBit = (bitStream & 0x80) == 0 ? 0U : 1U;
		    
				bitStream <<= 1;
				if ((bitStream & 0xFF) == 0) {
					bitStream = source.ReadByte(); //fetch new byte
					
					//get first bit
					nextBit = (bitStream & 0x80) == 0 ? 0U : 1U;
					
					bitStream = (bitStream << 1) | 1; //set sentinel
				}
				theBits = (theBits << 1) | nextBit;
			}
			return (ushort)theBits;
		}
		
		public uint GetOffset()
		{
		    uint value = 0;
		    if (GetBits(1) == 1) {
		        value = GetBits(1);
		        
		        if (GetBits(1) == 1) {
		        	value = value * 2U + 4U + GetBits(1);
		        	
		            if (GetBits(1) == 0)
		            	value = value * 2U + GetBits(1);
		            
		        } else if (value == 0)
		        	value = GetBits(1) + 2U;
		    }
		    return (value << 8) + (uint)source.ReadByte() + 1U;
		}
	}
	
	//____________________________________________________________________________
	/// <summary>
	/// Description of RNCUtils.
	/// </summary>
	public static class RNCUtils
	{
		/// <summary>
		/// IsHeader
		/// </summary>
		/// <param name="ds"></param>
		/// <param name="size"></param>
		/// <returns></returns>
		public static RNCHeader IsHeader(Stream ds, uint size)
		{
			
			BinaryReader rncReader = new BinaryReader(ds);
			
			uint type = rncReader.ReadUInt32();
			if((type & 0x00FFFFFF) != (uint)RNCMethod.NONE) return null;
			
			RNCHeader header = new RNCHeader();
			header.Identifier = (RNCMethod)type;
			
			header.UnpackSize = rncReader.ReadUInt32();
			header.PackSize = rncReader.ReadUInt32();
			
			header.UnpackChecksum = rncReader.ReadUInt16();
			header.PackChecksum = rncReader.ReadUInt16();
			
			header.Leeway = rncReader.ReadByte();
			header.Blocks = rncReader.ReadByte();
			
			return header;
		}
		
		/// <summary>
		/// Unpack
		/// </summary>
		/// <param name="source"></param>
		/// <param name="destination"></param>
		/// <param name="head"></param>
		/// <returns></returns>
		public static bool Unpack(Stream source, Stream destination, RNCHeader head)
		{
			BinaryReader rncReader = new BinaryReader(source);
			BinaryWriter rncWriter = new BinaryWriter(destination);
			
			uint position = (uint)source.Position;
			if(head.PackChecksum != CheckSum(source, head.PackSize)) return false;
			source.Position = position;
			destination.Position = 0;
			
			bool success = false;
			switch(head.Identifier)
			{
				case RNCMethod.SLOW :
					success = UnpackTypeSLOW(rncReader, head.PackSize, 
					                         rncWriter, head.UnpackSize, head.Blocks);
					break;
					
				case RNCMethod.FAST :
					success = UnpackTypeFAST(rncReader, head.PackSize, 
					                         rncWriter, head.UnpackSize);
					break;
			}
			
			if(success) {
				destination.Position = 0;
				success = head.UnpackChecksum == CheckSum(destination, head.UnpackSize);
			}
			
			return success;
		}
		
		//____________________________________________________________________________
		//Checksum table
		private static ushort[] table;
		public static ushort[] Table
		{
			get {
				//load table values on first call
			    if (table == null) {
					uint index, shift, checksum; 
			    	table = new ushort[256];
			    	
			        for (index = 0; index < 256; index++) {
			            checksum = index;
			            for (shift = 0; shift < 8; shift++) {
			            	if ((checksum & 1U) == 1U)
			            		checksum = (checksum >> 1) ^ 0xA001;
			                else
			                	checksum = checksum >> 1;
			            }
			            table[index] = (ushort)checksum;
			        }
			    }
				return table;
			}
		}
		
		//Checksum calculation
		public static ushort CheckSum(Stream ds, uint size)
		{
			BinaryReader rncReader = new BinaryReader(ds);
		    uint checksum = 0;
		    
		    while (size > 0) {
		    	size--;
		    	checksum ^= rncReader.ReadByte();  //apply next byte
		        checksum = (checksum >> 8) ^ Table[checksum & 0xFF];
		    }
		    return (ushort)checksum;
		}
		
		//____________________________________________________________________________
		/// <summary>
		/// MemCopy
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="fromOffset"></param>
		/// <param name="count"></param>
		private static void MemCopy(BinaryWriter writer, int fromOffset, int count)
		{
			Stream baseStream = writer.BaseStream;
			BinaryReader reader = new BinaryReader(baseStream);
			
			while (count-- > 0) {
				baseStream.Position -= fromOffset;
				byte aByte = reader.ReadByte();
				baseStream.Position += fromOffset - 1;
				writer.Write(aByte);
			}
		}
		
		//____________________________________________________________________________
		//RNC 1 slow method more compact
		
		/// <summary>
		/// UnpackTypeSLOW
		/// </summary>
		/// <param name="source"></param>
		/// <param name="sourceSize"></param>
		/// <param name="destination"></param>
		/// <param name="destSize"></param>
		/// <param name="blocks"></param>
		/// <returns></returns>
		private static bool UnpackTypeSLOW(BinaryReader source, uint sourceSize, 
		                                   BinaryWriter destination, uint destSize, 
		                                   ushort blocks)
		{
			uint sourceEnd = (uint)source.BaseStream.Position + sourceSize;
		    uint destEnd = (uint)destination.BaseStream.Position + destSize;
		    int counts, copies, offset, length;
		    
		    BitStreamSLOW bitStream = new BitStreamSLOW(source);
		    
		    HuffTree copyTable = new HuffTree(bitStream);
			HuffTree offsetTable = new HuffTree(bitStream);
			HuffTree lengthTable = new HuffTree(bitStream);
		    
		    do {
				copyTable.ReadTable(); 
				offsetTable.ReadTable(); 
				lengthTable.ReadTable(); 
		        
				counts = (int)bitStream.GetBits(16);
		 	       
		        while (counts != 0) {
		            copies = copyTable.GetValue();
					if (copies < 0) return false;
					
					while (copies-- > 0) destination.Write(source.ReadByte());
		            
		            if (--counts > 0) {
						
		                offset = offsetTable.GetValue();
						if (offset < 0) return false;
						offset += 1;
						
		                length = lengthTable.GetValue();
						if (length < 0) return false;
						length += 2;
						
						MemCopy(destination, offset, length);
		            }// end if
		        }//end while
		    } while (--blocks > 0);//blocks from header
		
		    //bitstream may fetch one extra unused byte
			if(source.BaseStream.Position > (sourceEnd + 1) || 
		       destination.BaseStream.Position > destEnd) return false;
			
		    return true; //successful unpack
		}//end unpack1
		
		//____________________________________________________________________________
		//RNC 2 fast method less compact
		
		/// <summary>
		/// UnpackTypeFAST
		/// </summary>
		/// <param name="source"></param>
		/// <param name="sourceSize"></param>
		/// <param name="destination"></param>
		/// <param name="destSize"></param>
		/// <returns></returns>
		private static bool UnpackTypeFAST(BinaryReader source, uint sourceSize, 
		                                   BinaryWriter destination, uint destSize)
		{
			uint sourceEnd = (uint)source.BaseStream.Position + sourceSize;
		    uint destEnd = (uint)destination.BaseStream.Position + destSize;
		    uint length, offset, index;
			bool done = false;
			
			BitStreamFAST bitStream = new BitStreamFAST(source);
			
		    while (!done) {
		        if (bitStream.GetBits(1) == 0) {
					destination.Write(source.ReadByte()); //pack bits
					continue;
				}
				length = 2;
	            if (bitStream.GetBits(1) == 0) {
					length = 4U + bitStream.GetBits(1); //pack length
	                if (bitStream.GetBits(1) != 0) {
						length = (length - 1) * 2 + bitStream.GetBits(1);
	                    if (length == 9) {
							length = (uint)(bitStream.GetBits(4) + 3) * 4;
	                        for (index = 0; index < length; index++)
	                        	destination.Write(source.ReadByte());
	                        continue;
	                    } 
	                } 
	                offset = bitStream.GetOffset();
	            } else {
	                if (bitStream.GetBits(1) != 0) {
	                    if (bitStream.GetBits(1) != 0) {
							length = (uint)source.ReadByte() + 8;
	                        if (length == 8) {
	                            if (bitStream.GetBits(1) == 0) 
									done = true;
	                            continue; //restart if length was zero
	                        } 
	                    } else {
	                        length = 3;
	                    }
	                offset = bitStream.GetOffset();
	                } else {
						offset = (uint)source.ReadByte() + 1;
	                }
	            }//end secondary else
				
				MemCopy(destination, (int)offset, (int)length);
				
		    }//end while
		    
		    if(source.BaseStream.Position > sourceEnd || 
			   destination.BaseStream.Position > destEnd)
		        return false;
		    else
		        return true;
		}//end unpack2
	}
	
	
	
}
