#include <SoftwareSerial.h>

const byte rxPin = A0;
const byte txPin = A1;

SoftwareSerial debugSerial (rxPin, txPin);

//
// PIN CONFIGURATION
//
const byte PIN_SENSE_ZERO = 12;
const int PIN_MXYZ_EN = 3; 
const int PIN_ME_EN = 2;
const int PIN_MOTOR_DIR[4] = {7, 5, 9, 11};
const int PIN_MOTOR_CLK[4] = {8, 6, 10, 4};


enum MessageType { Reset = 1, Position = 2, Clear = 3 };
enum ResponseType { Startup = 1, Acknowledge = 2, Completed = 3, Error = 4};

struct RequestHeader
{
  MessageType type;
  uint32_t id;
};
struct RequestPosition
{
  int8_t flags; // 1 0
  int32_t steps[3]; // 12 1
  int64_t stepsE; // 8 13
  uint32_t duration; // 4 21
};

void setup() {
  // put your setup code here, to run once:
  for(int i = 0; i < 4; i++){
    pinMode(PIN_MOTOR_CLK[i], OUTPUT);
    pinMode(PIN_MOTOR_DIR[i], OUTPUT);
  }
  pinMode(13, OUTPUT);
  
  Serial.begin(9600);
  debugSerial.begin(9600);
  debugSerial.println("setup");
  
}



void loop() {
  // put your main code here, to run repeatedly:
  readLoop();
  routerLoop();
}
