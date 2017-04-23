

bool processMessage(uint8_t* start, byte length)
{
//    debugSerial.print(" length=");
//    debugSerial.print(length);
//    debugSerial.print("/");
//    debugSerial.println(sizeof(RequestHeader));
  if(length >= sizeof(RequestHeader))
  {
    auto header = (RequestHeader*)start;
//    debugSerial.println("processing payload");
//
//    debugSerial.print("header->type=");
//    debugSerial.println(header->type);
//    
    switch(header->type)
    {
      case Reset:
        return DoReset(header->id); 
      case Clear:
        return DoClear(header->id);
      case Position:
        return processPosition(header->id, start+sizeof(RequestHeader), length - sizeof(RequestHeader));        
    }
  }
//  debugSerial.println("unknown type");
  return false; 
}

bool processPosition(uint32_t id, uint8_t* start, byte length)
{
//  debugSerial.print(" pos length=");
//  debugSerial.print(length);
//  debugSerial.print("/");
//  debugSerial.println(sizeof(RequestPosition));
  if(length == sizeof(RequestPosition)){ 
    return DoMove(id, (RequestPosition*)start);
  }
  return false;
}

