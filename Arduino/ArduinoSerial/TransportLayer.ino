
const byte CRC_SIZE = sizeof(uint16_t);
const byte BUFFER_SIZE = 128;
const byte HEADER_SIZE = 5; // MSG + length + seq
char buffer[BUFFER_SIZE] = {0};
byte bufferOffset = 0;
char headerOffset = -1;
byte payloadLength = 0;
byte sequenceNumber = 0;
byte responseSequence = 0;
char header[3] = {'M', 'S', 'G'};

struct crc
{
  uint16_t crc;
};

uint16_t computeChecksum(uint8_t* arr, byte len)
{ 
  uint16_t crc = 0xFFFF;
  for (byte pos = 0; pos < len ; pos++)
  {
    crc ^= (uint16_t)(*(byte*)(arr+pos));   // XOR byte into least sig. byte of crc
    for (byte i = 8; i != 0; i--)
    {    // Loop over each bit
      if ((crc & 0x0001) != 0)
      {      // If the LSB is set
        crc >>= 1;                    // Shift right and XOR 0xA001
        crc ^= 0xA001;
      }
      else                            // Else LSB is not set
          crc >>= 1;                    // Just shift right
    }
  }
  // Note, this number has low and high bytes swapped, so use it accordingly (or swap bytes)
  return crc;
}
        
bool checkHeader(byte i)
{
  for(byte j = 0; j < 3; j++)
  {
    if(header[j] != buffer[i+j])
    {
      return false;
    }
  }
  return true;
}
bool findHeader()
{
  //debugSerial.println("findHeader");
  for(byte i = 0; i < bufferOffset-HEADER_SIZE; i++)
  {
    if(checkHeader(i))
    {
//      debugSerial.print("foundHeader: ");
//      debugSerial.println(i);
      payloadLength = buffer[i+3];
      sequenceNumber = buffer[i+4];
      headerOffset = i;
      return true;
    }
  }
  return false;
}

void sendResponse(uint8_t* start, byte length)
{
//  debugSerial.print("response: ");
//  debugSerial.print(length);
//  debugSerial.print(" bytes, seq=");
//  debugSerial.print(responseSequence);
//  debugSerial.println("");
  uint16_t crc = computeChecksum(start, length);
  Serial.write((char*)&header, 3); // 0 1 2
  Serial.write(length); // 3
  Serial.write(responseSequence++); // 4
  Serial.write((char*)start, length); // 5 -> 5+length
  Serial.write(crc); // 5 + length + 1,2
}

void sendNACK(byte sequence, bool transportLevel)
{  
  byte nack[5] = {'N','A','C', sequence, transportLevel ? (byte)1 : (byte)0};
  sendResponse((uint8_t*)&nack[0], 5);
}
void sendACK(byte sequence)
{
  byte ack[4] = {'A','C','K', sequence};
  sendResponse((uint8_t*)&ack, 4);
}

void messageReceived(uint8_t* start, byte length, uint16_t crc, byte seq)
{
  auto calcCrc = computeChecksum(start, length);
  
//  debugSerial.print(length);
//  debugSerial.print(" bytes received with crc=");
//  debugSerial.print(crc, HEX);
//  debugSerial.print(", computed crc=");
//  debugSerial.print(calcCrc, HEX);
//  debugSerial.print(", seq=");
//  debugSerial.print(seq);
//  debugSerial.println();
    
  if(calcCrc != crc)
  {
    sendNACK(seq, true);
  }
  else
  {     
    if(!processMessage(start, length))
    {
      sendNACK(seq, false);
    }
    else
    {
      sendACK(seq);  
    }
  }
}
void shiftBuffer(byte start)
{
  for(int i = 0; i < start; i++)
  {
    auto o = start + i;
    if(o < BUFFER_SIZE){
      buffer[i] = buffer[start+i];
      buffer[start] = 0; 
    }else
      buffer[i] = 0;
  }
}
void readLoop()
{
  int available = min(Serial.available(), BUFFER_SIZE-bufferOffset);
  if(available > 0){
    int read = Serial.readBytes((char*)&buffer + bufferOffset, available);
    bufferOffset += read;
    if(read > 0){
      
      if(headerOffset < 0)
      {
        findHeader();
      } 
      if(headerOffset + HEADER_SIZE + payloadLength >= BUFFER_SIZE)
      {
        if(headerOffset > 0)
        { 
          shiftBuffer(headerOffset); // move header to pos 0 to make room
        }
        else
        {
          // message to long, garbage received, reset buffers
          headerOffset = 0;
          payloadLength = 0;
          bufferOffset = 0;
        }
      }
      auto msgEnd = headerOffset + HEADER_SIZE + payloadLength + CRC_SIZE;
     
      if(headerOffset >= 0)
      {
        if(bufferOffset >= msgEnd)
        {
          auto crcPtr = (crc*)&buffer[headerOffset + HEADER_SIZE + payloadLength];
          uint16_t crc = crcPtr->crc; 
          messageReceived((uint8_t*)&buffer[headerOffset + HEADER_SIZE], payloadLength, crc, sequenceNumber);
          shiftBuffer(msgEnd);
          bufferOffset -= msgEnd;
          headerOffset = -1;
          payloadLength = 0; 
          //debugSerial.println("reset buffer");
        }
      }  
    }
  }
}

