#include <SoftwareSerial.h>

const byte rxPin = A0;
const byte txPin = A1;

SoftwareSerial debugSerial (rxPin, txPin);

const byte PIN_SENSE_ZERO = 12;
const int PIN_MXYZ_EN = 3; 
const int PIN_ME_EN = 2;
const int PIN_MOTOR_DIR[4] = {7, 5, 9, 11};
const int PIN_MOTOR_CLK[4] = {8, 6, 10, 4};

enum MessageType { Reset = 1, Position = 2, Clear = 3 };
enum ResponseType { Startup = 1, Acknowledge = 2, Completed = 3, Error = 4};
struct RequestPosition
{
  int8_t flags;
  int32_t steps[3]; 
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

uint32_t positionXYZ[3] = {0,0,0};
int64_t positionExtruder = 0;
uint32_t positionTargetXYZ[3] = {0,0,0};
int64_t positionTargetExtruder = 0;
uint32_t timeXYZ[3]= {0,0,0};
uint32_t timeExtruder = 0;
uint32_t lastTimeXYZ[3];
uint32_t lastTimeExtruder;
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


  debugSerial.print("write \t\t\t");
  debugSerial.print(header->type);
  debugSerial.print("\t");
  debugSerial.print((uint32_t)header->id);
  debugSerial.print("\t");
  debugSerial.println(pos);
//  debugSerial.print("  pos=");
//  debugSerial.println(pos); 
//  debugSerial.print("  nextPos=");
//  debugSerial.println(currentWriteQueue); 
//  debugSerial.print("  index=");
//  debugSerial.println(index); 
//  debugSerial.print("  command=");
//  debugSerial.println(header->type); 
  
  confirm(header);
}
void confirm(RequestHeader* header)
{
  debugSerial.print("confirm \t\t");
  debugSerial.print(header->type);
  debugSerial.print("\t");
  debugSerial.println((uint32_t)header->id);
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
//  debugSerial.println("readReset");
  createPointer(header,0); 
}
void readClear(RequestHeader* header)
{
//  debugSerial.println("readClear");
  currentWriteQueue = 0;
  currentReadQueue = 0;
  commandQueueLength = 0;
  isCommandBusy = false;
  confirm(header);
}

void readPosition(RequestHeader* header)
{
//  debugSerial.println("readPosition");
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
//    debugSerial.print("  ");
//    debugSerial.print(i);
//    debugSerial.print(" = ");
//    debugSerial.println((byte)*(pointer + i), HEX);

  }
  positionQueue[pos] = *(RequestPosition*)pointer;
  
//  debugSerial.print("  duration=");
//  debugSerial.println(positionQueue[pos].duration); 
//  debugSerial.print("  x=");
//  debugSerial.println(positionQueue[pos].steps[0]); 
//  debugSerial.print("  y=");
//  debugSerial.println(positionQueue[pos].steps[1]); 
//  debugSerial.print("  z=");
//  debugSerial.println(positionQueue[pos].steps[2]); 
  
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
  if(Serial.available()> 0)
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
      debugSerial.print("read\t\t\t");
      debugSerial.print(header.type);
      debugSerial.print("\t");
      debugSerial.println((uint32_t)header.id);
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

  for(int i = 0; i < 4; i++){
    pinMode(PIN_MOTOR_CLK[i], OUTPUT);
    pinMode(PIN_MOTOR_DIR[i], OUTPUT);
  }
}

void completeCommand()
{
  debugSerial.println("completeCommand");
  auto command = commandQueue[currentReadQueue];
//    debugSerial.print("  start=");
//    debugSerial.println((uint32_t)command.header.id); 
  Response response;
  response.type = Completed;
  response.header = command.header;
  response.queueLength = commandQueueLength;
  response.queueAvailable = MAX_QUEUE_LEN-commandQueueLength;
  Serial.write("MSG");
  Serial.write((char*)&response, sizeof(Response));
  isCommandBusy = false;
  currentReadQueue++;
  if(currentReadQueue > MAX_QUEUE_LEN)
    currentReadQueue = 0;
}

void runReset()
{
  auto now = micros();
  auto delta = now - commandStartTime;
  
  positionTargetXYZ[0] = 0;
  positionTargetXYZ[1] = 0;
  positionTargetXYZ[2] = 0; 
  positionTargetExtruder = 0;

  for(int i = 0; i < 3; i++)
  {
    digitalWrite(PIN_MOTOR_DIR[i],LOW);
    pinMode(PIN_MOTOR_CLK[i], delta % 2000 < 1000 ? HIGH : LOW);
  }
    
  bool isHome = digitalRead(PIN_SENSE_ZERO);
  if(isHome){ 
    positionXYZ[0] = 0;
    positionXYZ[1] = 0;
    positionXYZ[2] = 0;    
    positionExtruder = 0;
//    debugSerial.println("resetComplete");
    completeCommand();
  }
}
template <typename type>static inline int8_t dir(type a, type b) {
  if (a < b) return -1;
  if (a==b) return 0;
  return 1;
}

void runMove(RequestPosition* pos, uint32_t iteration)
{
  auto now = micros();
  auto delta = now - commandStartTime;
  if(iteration == 0)
  {
    debugSerial.println("started moving");
    for(int i = 0; i<3; i++){
      int32_t stepCount = 0;
      byte flag = 1<<i;
      if(pos->flags & flag){ // if flag is set: relative movement
        stepCount = pos->steps[i];

        if(pos->steps[i] < 0 && positionTargetXYZ[i] < -pos->steps[i])
          positionTargetXYZ[i] = 0; // can't go more left 
        else
        positionTargetXYZ[i] += pos->steps[i];      
      }else{ 
        int32_t delta = pos->steps[i] - positionXYZ[i];
        positionTargetXYZ[i] = max(0, pos->steps[i]);
        stepCount = delta; 
      }
      timeXYZ[i] = pos->duration / abs(stepCount);
      lastTimeXYZ[i] = commandStartTime;

      digitalWrite(PIN_MOTOR_CLK[i], HIGH);
      digitalWrite(PIN_MOTOR_DIR[i], positionTargetXYZ[i] > positionXYZ[i] ? HIGH : LOW); // set direction
 
      debugSerial.print("  time[");
      debugSerial.print(i);
      debugSerial.print("]=");
      debugSerial.println(timeXYZ[i]); 
      debugSerial.print("  steps[");
      debugSerial.print(i);
      debugSerial.print("]=");
      debugSerial.println(stepCount); 
      debugSerial.print("  target[");
      debugSerial.print(i);
      debugSerial.print("]=");
      debugSerial.println(positionTargetXYZ[i]);  
      debugSerial.print("  pos[");
      debugSerial.print(i);
      debugSerial.print("]=");
      debugSerial.println(positionXYZ[i]);  
    }
  }

  byte atTargetXYZ = 0; 

  for(int i = 0; i < 3; i++)
  {
    if(positionTargetXYZ[i] == positionXYZ[i]){
      atTargetXYZ |= 1<<i;
    }else
    {
      auto delta = now - lastTimeXYZ[i];
      if(delta > timeXYZ[i]) 
      { 
        lastTimeXYZ[i] += timeXYZ[i]; 
        digitalWrite(PIN_MOTOR_CLK[i], HIGH);
        auto d = dir(positionTargetXYZ[i], positionXYZ[i]);
        if(d > 0 || positionXYZ[i] > 0)
        {
          positionXYZ[i] = positionXYZ[i] + d;
        }

//        debugSerial.print("pos["); 
//        debugSerial.print(i); 
//        debugSerial.print("]="); 
//        debugSerial.println(positionXYZ[i]);

      }
      else if(delta > timeXYZ[i] / 2) // 50% duty cycle
      {
        digitalWrite(PIN_MOTOR_CLK[i], LOW);
      }
    }
  }

  
  
  if(atTargetXYZ == 7 && positionTargetExtruder == positionExtruder){    
    debugSerial.println("moveComplete"); 
    completeCommand();
  }
}

void runCommand(uint32_t iteration)
{
  auto command = commandQueue[currentReadQueue];
  if(iteration == 0){
    debugSerial.print("runCommand\t\t");
    debugSerial.print(command.header.type);    
    debugSerial.print("\t");
    debugSerial.print((uint32_t)command.header.id);
    debugSerial.print("\t");
    debugSerial.println(currentReadQueue);
  }
  switch(command.header.type)
  {
    case Reset:
      runReset();
      break;
    case Position:
      runMove(&positionQueue[command.index], iteration);
      break;
    default:
      break;
  }
}

void startNextCommand()
{
  if(commandQueueLength > 0)
  {
//    debugSerial.println("startNextCommand");
//    debugSerial.print("  queueLength=");
//    debugSerial.println(commandQueueLength);
//    debugSerial.print("  command=");
//    debugSerial.println(commandQueue[currentReadQueue].header.type);
    commandQueueLength--;
    commandStartTime = micros(); 
    isCommandBusy = true;
  } 
}
uint32_t commandIteration;
void processCommandBuffer()
{
  if(isCommandBusy) 
    runCommand(commandIteration++); 
  else{
    startNextCommand();
    commandIteration = 0;
  }
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
