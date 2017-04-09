#include <SoftwareSerial.h>

const byte rxPin = 2;
const byte txPin = 3;

SoftwareSerial debugSerial (rxPin, txPin);

enum MessageType { Reset = 1, Position = 2, Clear = 3 };
enum ResponseType { Startup = 1, Acknowledge = 2, Completed = 3, Error = 4};
struct RequestPosition
{
  int8_t flags;
  int32_t stepsX;
  int32_t stepsY;
  int32_t stepsZ;
  int64_t stepsE;
  uint32_t duration;
};

struct ResponseConfirmation
{
  int8_t flags;
  int32_t stepsX;
  int32_t stepsY;
  int32_t stepsZ;
  int64_t stepsE;
};

struct RequestHeader
{
  MessageType type;
  uint64_t id;
};

struct CommandPointer
{
  RequestHeader header;
  uint8_t index;
};

struct Response
{
  ResponseType type;
  uint8_t queueLength;
  uint8_t queueAvailable;
  RequestHeader header;
};

const byte MAX_QUEUE_LEN = 20;
RequestPosition positionQueue[MAX_QUEUE_LEN];
CommandPointer commandQueue[MAX_QUEUE_LEN];
byte currentCommand; 
byte currentWritePosition;
byte currentWriteQueue;
byte currentReadQueue;
byte commandQueueLength;

char receiveBuffer[255]; 
byte receiveOffset = 0;

bool isCommandBusy = false;
unsigned long commandStartTime;

void createPointer(RequestHeader* header, byte index)
{
  CommandPointer pointer;
  pointer.header = *header;
  pointer.index = index;  
  auto pos = currentWriteQueue;
  currentWriteQueue++;
  if(currentWriteQueue > MAX_QUEUE_LEN)
    currentWriteQueue = 0;
  commandQueue[pos] = pointer;
  commandQueueLength++;


  debugSerial.println("createPointer");
  debugSerial.print("  pos=");
  debugSerial.println(pos); 
  debugSerial.print("  nextPos=");
  debugSerial.println(currentWriteQueue); 
  debugSerial.print("  index=");
  debugSerial.println(index); 
  debugSerial.print("  command=");
  debugSerial.println(header->type); 
  
  confirm(header);
}
void confirm(RequestHeader* header)
{
  Response response;
  response.type = Acknowledge;
  response.header = *header;
  response.queueLength = commandQueueLength;
  response.queueAvailable = MAX_QUEUE_LEN-commandQueueLength;
  Serial.write("MSG");
  Serial.write((char*)&response, sizeof(Response));
}

void readReset(RequestHeader* header)
{
  debugSerial.println("readReset");
  createPointer(header,0); 
}
void readClear(RequestHeader* header)
{
  debugSerial.println("readClear");
  currentWriteQueue = 0;
  currentReadQueue = 0;
  commandQueueLength = 0;
  isCommandBusy = false;
  confirm(header);
}

void readPosition(RequestHeader* header)
{
  debugSerial.println("readPosition");
  auto pos = currentWritePosition;
  currentWritePosition++;
  if(currentWritePosition >= MAX_QUEUE_LEN)
    currentWritePosition = 0;

  int offset = 0;
  auto pointer = (char*)&positionQueue[pos];
  do{
    int available = Serial.available();    
    if(available > 0){
      auto count = Serial.readBytes(pointer + offset,min(available, sizeof(RequestPosition) - offset));     
      if(count>0)
        offset += count;
    }
  } 
  while(offset < sizeof(RequestPosition));

  for(int i = 0; i < sizeof(RequestPosition); i ++){ 
    debugSerial.print("  ");
    debugSerial.print(i);
    debugSerial.print(" = ");
    debugSerial.println((byte)*(pointer + i), HEX);

  }
  positionQueue[pos] = *(RequestPosition*)pointer;
  
  debugSerial.print("  duration=");
  debugSerial.println(positionQueue[pos].duration); 
  debugSerial.print("  x=");
  debugSerial.println(positionQueue[pos].stepsX); 
  debugSerial.print("  y=");
  debugSerial.println(positionQueue[pos].stepsY); 
  debugSerial.print("  z=");
  debugSerial.println(positionQueue[pos].stepsZ); 
  
  createPointer(header,pos);
}

void invalidHeader()
{ 
  debugSerial.println("invalidHeader");
  Response response;
  response.header.id = 1;
  response.header.type = Clear;
  response.type = Error;
  response.queueLength = commandQueueLength;
  response.queueAvailable = MAX_QUEUE_LEN-commandQueueLength;
  Serial.write("MSG");
  Serial.write((char*)&response, sizeof(Response)); 
}

void shiftLeft(char* buffer, int bufferLength, byte msgPos)
{   
  for (int i = msgPos; i < bufferLength; i++) 
      buffer[i - msgPos] = buffer[i];  
}

bool findMsg(char* buffer, int bufferLength, byte* pos)
{
  for (byte i = 0; i < bufferLength - 3; i++)
  {
    if (buffer[i] == 'M' && buffer[i + 1] == 'S' && buffer[i + 2] == 'G')
    {
      *pos = i;
      return true;
    }
  }
  return false;
}

        bool led13 = false;
void readCommandBuffer()
{  
  while(Serial.available()> 0)
  { 
    int toRead = (sizeof(RequestHeader) - receiveOffset + 3);
    int count = Serial.readBytes((char*)&receiveBuffer + receiveOffset, toRead); // prevent timeouts!
    if(count >= 0){
      receiveOffset += count;      
      if(receiveOffset > 3)
      {
        byte msgPos;
        if (findMsg((char*)&receiveBuffer, 255, &msgPos) && msgPos != 0)
        {
          shiftLeft((char*)&receiveBuffer, 255, msgPos);
          receiveOffset -= msgPos;
        }
      }
      if(receiveOffset < sizeof(RequestHeader) + 3)
        continue;
  
      auto msgLength = receiveOffset;
      receiveOffset = 0; 
      
      RequestHeader header = *((RequestHeader*)(&receiveBuffer[3]));
      switch(header.type)
       {
          case Reset:
            readReset(&header);
            break;
          case Position:
            readPosition(&header);
            break;
          case Clear:
            readClear(&header);
            break;
          default:
            invalidHeader();
            break;
       } 
       digitalWrite(13, (led13 = !led13) ? HIGH : LOW);
    }
  }
}

void setup() {
  // put your setup code here, to  run once: 
  Serial.begin(9600);
 
  
  Response response;
  response.header.id = 0; 
  response.header.type = Clear;
  response.type = Startup;
  response.queueLength = commandQueueLength;
  response.queueAvailable = MAX_QUEUE_LEN-commandQueueLength;
  Serial.write("MSG");
  Serial.write((char*)&response, sizeof(Response)); 
  pinMode(13, OUTPUT);

  debugSerial.begin(9600);
  debugSerial.println("setup");
}

void completeCommand()
{
  debugSerial.println("completeCommand");
  auto command = commandQueue[currentReadQueue];
    debugSerial.print("  start=");
    debugSerial.println((uint32_t)command.header.id); 
  Response response;
  response.type = Completed;
  response.header = command.header;
  response.queueLength = commandQueueLength;
  response.queueAvailable = MAX_QUEUE_LEN-commandQueueLength;
  Serial.write("MSG");
  Serial.write((char*)&response, sizeof(Response));
  isCommandBusy = false;
  currentReadQueue++;
}

void runReset()
{
  auto now = micros();
  auto delta = now - commandStartTime;
  if(delta > 1000000){
    debugSerial.println("resetComplete");
    completeCommand();
  }
}
bool isMoving;
void runMove(RequestPosition* pos)
{
  auto now = micros();
  auto delta = now - commandStartTime;
  if(!isMoving)
  {
    debugSerial.println("startedMoving");
    debugSerial.print("  start=");
    debugSerial.println(commandStartTime); 
    debugSerial.print("  duration=");
    debugSerial.println(pos->duration); 
    isMoving = true;    
  }
  if(delta > pos->duration){    
    debugSerial.println("moveComplete");
    isMoving = false;
    completeCommand();
  }
}

void runCommand()
{
  auto command = commandQueue[currentReadQueue];
  switch(command.header.type)
  {
    case Reset:
      runReset();
      break;
    case Position:
      runMove(&positionQueue[command.index]);
      break;
    default:
      break;
  }
}

void startNextCommand()
{
  if(commandQueueLength > 0)
  {
    debugSerial.println("startNextCommand");
    debugSerial.print("  queueLength=");
    debugSerial.println(commandQueueLength);
    debugSerial.print("  command=");
    debugSerial.println(commandQueue[currentReadQueue].header.type);
    commandQueueLength--;
    commandStartTime = micros(); 
    isCommandBusy = true;
  } 
}
void processCommandBuffer()
{
  if(isCommandBusy) 
    runCommand(); 
  else
    startNextCommand();
}
void loop() {
  // put your main code here, to run repeatedly:
  readCommandBuffer();
  processCommandBuffer();
//  if(Serial.available()>0)
//  {
//    char buffer[2];
//    int count = Serial.readBytes((char*)&buffer, 2);
//    Serial.write(buffer, count);
//  }
}
