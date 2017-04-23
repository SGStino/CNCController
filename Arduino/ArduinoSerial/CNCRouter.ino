struct CommandPointer
{
  MessageType type;
  uint32_t id;
  byte position;
};
struct CommandResponse
{
  char header[3];
  uint32_t id;
  uint32_t pos[3];
  uint64_t posE;
  byte currentQueueLength;
};

const char COMMANND_QUEUE_LENGTH = 30;
CommandPointer commandQueue[COMMANND_QUEUE_LENGTH];
RequestPosition positionQueue[COMMANND_QUEUE_LENGTH];
char currentReadCommand = -1;
char currentWriteCommand = 0;
char currentWritePosition = 0;
byte currentQueueLength = 0;
uint32_t iteration; 
bool commandComplete = false;

uint32_t currentPositionXYZ[3] = {0};
uint64_t currentPositionE = 0;
uint32_t targetPositionXYZ[3] = {0};
uint64_t targetPositionE = 0;
uint32_t currentTimeXYZ[3]= {0,0,0};
uint32_t currentTimeE = 0;
uint32_t lastTimeXYZ[3];
uint32_t lastTimeE;

char writePosition(RequestPosition* position)
{
  char writePos = currentWritePosition;
  positionQueue[writePos] = *position;
  currentWritePosition++;
  if(currentWritePosition >= COMMANND_QUEUE_LENGTH)
    currentWritePosition = 0;
  return writePos;
}

bool createCommand(MessageType type, uint32_t id, byte position)
{
  if(currentQueueLength < COMMANND_QUEUE_LENGTH)
  {
    CommandPointer ptr;
    ptr.type = type;
    ptr.id = id;
    ptr.position = position;
    commandQueue[currentWriteCommand] = ptr;
    
    currentWriteCommand++;
    if(currentWriteCommand >= COMMANND_QUEUE_LENGTH)
      currentWriteCommand = 0;
    currentQueueLength++;
    return true;
  }
  return false;
}
void sendCommandResponse(uint32_t id, char header[3])
{
  CommandResponse resp;
  for(int i = 0; i < 3; i++){
    resp.header[i] = header[i];
    resp.pos[i] = currentPositionXYZ[i];
  }
  resp.id = id;
  resp.posE = currentPositionE;
  resp.currentQueueLength = currentQueueLength;
  sendResponse((uint8_t*)&resp, sizeof(CommandResponse));
}
void sendStart(uint32_t id)
{
  sendCommandResponse(id, "STA");
}
void sendEnd(uint32_t id)
{
  sendCommandResponse(id, "STO");
}

bool DoClear(uint32_t id)
{
//  debugSerial.println("Clear");
  currentWriteCommand = 0;
  currentReadCommand = -1;
  currentWritePosition = 0;
  currentQueueLength = 0;
  return true;
}

bool DoReset(uint32_t id)
{
//  debugSerial.println("Reset");
  return createCommand(Reset, id, -1);
}

bool DoMove(uint32_t id, RequestPosition* position)
{
//    debugSerial.println("Move");
//    debugSerial.print("  x=");
//    debugSerial.println(position->steps[0]);
//    debugSerial.print("  y=");
//    debugSerial.println(position->steps[1]);
//    debugSerial.print("  z=");
//    debugSerial.println(position->steps[2]);
//    debugSerial.print("  e=");
//    debugSerial.println((uint32_t)position->stepsE);
//    debugSerial.print("  t=");
//    debugSerial.println(position->duration);
    return createCommand(Position, id, writePosition(position));
}

void completeCommand(uint32_t id)
{
    debugSerial.print("stop ID");
    debugSerial.println(id);
    //TODO: send confirmation
    commandComplete = true;
    nextCommand();
    sendEnd(id);
}

template <typename type>static inline int8_t dir(type a, type b) {
  if (a < b) return -1;
  if (a==b) return 0;
  return 1;
}

unsigned long commandStartTime;
void runReset(uint32_t id, uint32_t iteration)
{
  auto now = micros();
  if(iteration == 0)
    commandStartTime = now;
  auto delta = now - commandStartTime;
  
  targetPositionXYZ[0] = 0;
  targetPositionXYZ[1] = 0;
  targetPositionXYZ[2] = 0; 
  targetPositionE= 0;


  for(int i = 0; i < 3; i++)
  {
    digitalWrite(PIN_MOTOR_DIR[i],LOW);
    pinMode(PIN_MOTOR_CLK[i], delta % 2000 < 1000 ? HIGH : LOW);
  }
    
  bool isHome = digitalRead(PIN_SENSE_ZERO);
  if(isHome){ 
    currentPositionXYZ[0] = 0;
    currentPositionXYZ[1] = 0;
    currentPositionXYZ[2] = 0; 
    currentPositionE = 0;
    completeCommand(id);
  }
}

void runPosition(uint32_t id, uint32_t iteration, RequestPosition* pos)
{
  auto now = micros();
  if(iteration == 0)
  { 
    debugSerial.print("Move to ");
    for(int i = 0; i<3; i++){
      int32_t stepCount = 0;
      byte flag = 1<<i;
      if(pos->flags & flag){ // if flag is set: relative movement
        stepCount = pos->steps[i];

        if(pos->steps[i] < 0 && targetPositionXYZ[i] < -pos->steps[i])
          targetPositionXYZ[i] = 0; // can't go more left 
        else
        targetPositionXYZ[i] += pos->steps[i];      
      }else{ 
        int32_t delta = pos->steps[i] - currentPositionXYZ[i];
        targetPositionXYZ[i] = max(0, pos->steps[i]);
        stepCount = delta; 
      }
      currentTimeXYZ[i] = pos->duration / abs(stepCount);
      lastTimeXYZ[i] = now;

      digitalWrite(PIN_MOTOR_CLK[i], HIGH);
      digitalWrite(PIN_MOTOR_DIR[i], targetPositionXYZ[i] > currentPositionXYZ[i] ? HIGH : LOW); // set direction
      debugSerial.print(targetPositionXYZ[i]);
      debugSerial.print(" ");
    }
    debugSerial.println();
  }
  
  byte atTargetXYZ = 0; 

  for(int i = 0; i < 3; i++)
  {
    if(targetPositionXYZ[i] == currentPositionXYZ[i]){
      atTargetXYZ |= 1<<i;
    }else
    {
      auto delta = now - lastTimeXYZ[i];
      if(delta > currentTimeXYZ[i]) 
      {  
        lastTimeXYZ[i] += currentTimeXYZ[i]; 
        digitalWrite(PIN_MOTOR_CLK[i], HIGH);
        
        auto d = dir(targetPositionXYZ[i], currentPositionXYZ[i]);
        if(d > 0 || currentPositionXYZ[i] > 0)
        {
          currentPositionXYZ[i] = currentPositionXYZ[i] + d;
        }
      }
      else if(delta > currentTimeXYZ[i] / 2) // 50% duty cycle
      {
        digitalWrite(PIN_MOTOR_CLK[i], LOW);
      }
    }
  }  
  
  if(atTargetXYZ == 7 && targetPositionE == currentPositionE){    
    debugSerial.println("moveComplete"); 
    completeCommand(id);
  }
}
void runCommand()
{
  auto command = commandQueue[currentReadCommand];
  if(iteration == 0){
    debugSerial.print("start ID");
    debugSerial.print(command.id);
    debugSerial.print(" pos");
    debugSerial.println(command.position);
    sendStart(command.id);
  }
  auto currentIteration = iteration;
  iteration++;
  switch(command.type)
  {
    case Reset:
      runReset(command.id, currentIteration);
      break;
    case Position:
      runPosition(command.id, currentIteration, &positionQueue[command.position]);
      break;
  }
}

void nextCommand()
{
  if(currentQueueLength > 0){
    debugSerial.println("next command");
    currentQueueLength--;
    currentReadCommand++;
    if(currentReadCommand >= COMMANND_QUEUE_LENGTH)
      currentReadCommand = 0;
    iteration = 0;
    commandComplete = false;
  }
}
void routerLoop()
{
   if(currentReadCommand>=0 && !commandComplete){
      runCommand();
   }
   else if(currentQueueLength>=0)
   {
      nextCommand();
     // wait for command;
   }
}

