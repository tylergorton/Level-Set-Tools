/*
    RNC2 source file
    Tyler Gorton
    2011
    
    Adapted from copywritten Intel Assembler Source and others
    Not for commercial use
*/
#include <stdio.h>
#include <stdlib.h>

//encryption key
/*
    byte ^= key; key = _rotr(key, 1);
*/

/*____________________________________________________________________________*/
short 
testRNC(unsigned long firstLong)
{
    int method = 0;
    method = (firstLong & 0xFF000000) >> 24;      //get low byte
    firstLong &= 0x00FFFFFF;        //mask off low byte
    if (firstLong != 0x00434E52)    //test for 'R','N','C'
        method = -1;				//error return value
    return method;
}

/*____________________________________________________________________________*/
unsigned char
get_byte(unsigned char **byteStreamPtr) //modifies data source ptr
{
	return *(*byteStreamPtr)++;
}

unsigned short 
get_word(unsigned char **byteStreamPtr) //modifies data source ptr
{
    return get_byte(byteStreamPtr) | get_byte(byteStreamPtr) << 8;
}

//modifies only data destination ptr
void 
mem_move(unsigned char **put, unsigned char *start, int count)
{
    unsigned char *get = start;
    while (count--) *(*put)++ = *get++;
}

/*____________________________________________________________________________*/
unsigned short 
RNCchecksum(char *dataPointer, unsigned long dataSize)
{
    static unsigned short table[256];
    static unsigned short initialized = 0;
    unsigned char *data = dataPointer;
    unsigned short index, shift, checksum; 

    //load table values on first call
    if (!initialized) {
        for (index=0; index<256; index++) {
            checksum = index;
            for (shift=0; shift<8; shift++) {
                if (checksum & 1)
                    checksum = (checksum >> 1)^0xA001;
                else
                    checksum = (checksum >> 1);
            }
            table[index] = checksum;
        }
		initialized = 1;
    }
    
    //calculate checksum
    checksum = 0;
    while (dataSize--) {
        checksum ^= *data++;  //apply next byte
        checksum = (checksum >> 8) ^ table[checksum & 0xFF];
    }
    return checksum;
}

/*____________________________________________________________________________*/
/*____________________________________________________________________________*/
// RNC 1 huffman code tools

struct huffnode {
	unsigned short length;	//number of bits in code, zero for end sentinel
	unsigned short code;		//code bits
	unsigned short value;	//defined value
};

/* 16 bit right going stream NO PRE-FETCH
 * count is zero to initialze
 */
unsigned short 
get_bits1(unsigned char **byteStreamPtr, unsigned short count)
{
    static unsigned long bitStream;
    static short buffSize;
	unsigned long bitMask;
	unsigned short theBits;

	if (!count) {
		bitStream = 0;	//flush stream on zero
		buffSize = 0; //reset bit count
	}

	if (buffSize - count < 0) {
		bitStream |= (unsigned long)get_word(byteStreamPtr) << buffSize;
		buffSize += 16;
	}	

	bitMask = (1 << count) - 1;
	theBits = (unsigned short)(bitStream & bitMask);
	bitStream >>= count;
	buffSize -= count;

    return theBits;
}

//mirror rightmost length bits
unsigned short 
mirror(unsigned short image, short length)
{
	unsigned short reflection = 0;
	short i;

	for (i=0;i<length;i++) {
		reflection <<= 1;
		if (image & 1L) reflection += 1;
		image >>= 1;
	}
	return reflection;
}

void 
read_table(struct huffnode tree[], unsigned char **src)
{
    short i, j, k, num;
    short depth[32];
    short depthmax;
    unsigned short codeb;

    num = get_bits1(src, 5);
    depthmax = 1;
    for (i=0; i<num; i++) {
        depth[i] = get_bits1(src, 4);
        if (depthmax < depth[i])
	        depthmax = depth[i];
    }
    codeb = 0;
    k = 0;
    for (i=1; i<=depthmax; i++) {
		for (j=0; j<num; j++)
			if (depth[j] == i) {
				tree[k].length = i;
				tree[k].code = mirror(codeb, i);
				tree[k].value = j;
				codeb++;
				k++;
			}
			codeb <<= 1;
	}
	if (k < 32) tree[k].length = 0;	//write zero length sentinel
}

long
get_value(struct huffnode tree[], unsigned char **src)
{
	short node;
	short length = 0;
	short delta;
	unsigned short code = 0;
	long value;

	for (node = 0; node < 32; node++) {
		delta = tree[node].length - length;
		if (delta > 0)
			code |= get_bits1(src, delta) << length;
		length = tree[node].length; //next length
		if (length == 0) return -1;	//sentinel reached
		if (code == tree[node].code) break;
	}

	if (node == 32) return -1; //array exceeded
	if (tree[node].value > 1) {
		value = 1 << (tree[node].value - 1);
		value |= get_bits1(src, tree[node].value - 1);
	} else value = tree[node].value;
	return value;
}

/*____________________________________________________________________________*/
//RNC1 unpack

int RNCunpack1(unsigned char *packed, unsigned long srcSize, 
unsigned char *unpacked, unsigned long dstSize,
unsigned short blocks)
{
	unsigned char *src = packed;
    unsigned char *dst = unpacked;
    unsigned char *srcEnd = src + srcSize;
    unsigned char *dstEnd = dst + dstSize;
    long counts, copies, offset, length;

	struct huffnode copyTable[32];
	struct huffnode offsetTable[32];
	struct huffnode lengthTable[32];

	get_bits1(&src, 0); //resets bit stream
    get_bits1(&src, 2); //toss first two bits
    
    do {
        read_table(copyTable, &src); 
        read_table(offsetTable, &src); 
        read_table(lengthTable, &src); 
        counts = get_bits1(&src, 16);
        while (counts) {
            copies = get_value(copyTable, &src);
			if (copies < 0) return 1;
            while (copies--) *dst++ = *src++;
			//bit stream adjust not needed
            if (--counts) {
                offset = get_value(offsetTable, &src);
				if (offset < 0) return 1;
				offset += 1;
                length = get_value(lengthTable, &src);
				if (length < 0) return 1;
				length += 2;
                mem_move(&dst, dst - offset, length);//memcpy
            }// end if
        }//end while
    } while (--blocks);//blocks from header

    //bitstream may fetch one extra unused byte
	if(src > (srcEnd + 1) || dst > dstEnd) return 1;
    return 0; //successful unpack
}//end unpack1

/*____________________________________________________________________________*/
/*____________________________________________________________________________*/
// RNC 2 huffman code tools

/* 8 bit left going stream
 * count is zero to initialze
 */
unsigned short 
get_bits2(unsigned char **byteStreamPtr, unsigned short count)
{
    static unsigned char bitStream = 0;
    unsigned short nextBit = 0;
	unsigned short theBits = 0;

	if (!count) 
		bitStream = 0;	//flush stream on zero
	
	while (count--) {
		if (bitStream & 0x80)
			nextBit = 1;
		else
			nextBit = 0;
    
		bitStream <<= 1;//shift
		if (!bitStream) {
			bitStream = get_byte(byteStreamPtr);//fetch new byte
			if (bitStream & 0x80)//get first bit
				nextBit = 1;
			else
				nextBit = 0;
			bitStream <<= 1;//shift
			bitStream |= 1;//set sentinel
		}
		theBits = (theBits << 1) + nextBit;
	}
    return theBits;
}

unsigned short 
get_offset(unsigned char **byteStreamPtr)
{
    unsigned short value = 0;
    if (get_bits2(byteStreamPtr, 1)) {
        value = get_bits2(byteStreamPtr, 1);
        if (get_bits2(byteStreamPtr, 1)) {
            value = value * 2 + 4 + get_bits2(byteStreamPtr, 1);
            if (!get_bits2(byteStreamPtr, 1))
                value = value * 2 + get_bits2(byteStreamPtr, 1);
        } else if (value == 0)
            value = get_bits2(byteStreamPtr, 1) + 2;
    }
    return (value << 8) + get_byte(byteStreamPtr) + 1;
}

/*____________________________________________________________________________*/
//RNC2 unpack

int RNCunpack2(unsigned char *packed, unsigned long srcSize, 
unsigned char *unpacked, unsigned long dstSize)
{
    unsigned char *src = packed;
    unsigned char *dst = unpacked;
    unsigned char *srcEnd = src + srcSize;
    unsigned char *dstEnd = dst + dstSize;
    unsigned short length, offset, index;
	unsigned short end = 0;
    
	get_bits2(&src, 0); //resets bit stream
    get_bits2(&src, 2); //toss first two bits

    while (!end) {
        if (!get_bits2(&src, 1)) {
            *dst++ = get_byte(&src); //pack bits
        } else {
			length = 2;
            if (!get_bits2(&src, 1)) {
                length = 4 + get_bits2(&src, 1); //pack length
                if (get_bits2(&src, 1)) {
                    length = (length - 1) * 2 + get_bits2(&src, 1);
                    if (length == 9) {
                        length = (get_bits2(&src, 4) + 3) * 4;
                        for (index = 0; index < length; index++)
                        *dst++ = get_byte(&src);
                        continue;
                    } 
                } 
                offset = get_offset(&src);
            } else {
                if (get_bits2(&src, 1)) {
                    if (get_bits2(&src, 1)) {
                        length = get_byte(&src) + 8;
                        if (length == 8) {
                            if (!get_bits2(&src, 1)) 
								end = 1;
                            continue; //restart if length was zero
                        } 
                    } else {
                        length = 3;
                    }
                offset = get_offset(&src);
                } else {
                    offset = get_byte(&src) + 1;
                }
            }//end secondary else
        mem_move(&dst, dst - offset, length);
        }//end primary else
    }//end while
    
    if(src > srcEnd || dst > dstEnd)
        return 1;
    else
        return 0;
}//end unpack2

/*____________________________________________________________________________*/
//END RNC2.c
