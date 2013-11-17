/*
    RNC2 header file
    Tyler Gorton
    2011
    
    Adapted from copywritten Intel Assembler Source
    Not for commercial use
*/

//public exported structure

struct RNCheader
{
    unsigned long identifier;    //must contain 'R', 'N', 'C', method
    unsigned long unpackSize;     //unpacked data size
    unsigned long packSize;      //packed data size (excludes this header)
    unsigned short unpackChecksum;  //unpacked data checksum
    unsigned short packChecksum;   //packed data checksum
    unsigned char leeway;         //not used
    unsigned char blocks;    //number of sections
	//unsigned char start;		//first byte of data
};

#define RNC_HEADER_LENGTH	18

//type 1 huffman tables
/*
    5 bits : number of entries (0-16)
    4x<number> bits : code lengths
*/

//public exported functions

short 
testRNC(unsigned long firstLong);
//test for RNC, return method number or 0 for non RNC

unsigned short 
RNCchecksum(char *dataPointer, unsigned long dataSize);
//return checksum on buffer of size

int 
RNCunpack1(unsigned char *packed, unsigned long srcSize, 
unsigned char *unpacked, unsigned long dstSize,
unsigned short blocks); 
//unpack RNC data returns null on success

int 
RNCunpack2(unsigned char *packed, unsigned long srcSize, 
unsigned char *unpacked, unsigned long dstSize);
//unpack RNC data returns null on success

//END RNC2.h
