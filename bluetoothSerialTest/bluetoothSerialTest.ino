// A serial loop back app

#include <NewPing.h>

#define TRIGGER_PIN  12  // Arduino pin tied to trigger pin on the ultrasonic sensor.
#define ECHO_PIN     11  // Arduino pin tied to echo pin on the ultrasonic sensor.
#define MAX_DISTANCE 200 // Maximum distance we want to ping for (in centimeters). Maximum sensor distance is rated at 400-500cm.
NewPing sonar(TRIGGER_PIN, ECHO_PIN, MAX_DISTANCE); // NewPing setup of pins and maximum distance.

void setup() {
  Serial.begin(9600);
  delay(333);
  while (!Serial)
    ;
   delay(1000);

}

void loop() {
  delay(100);
  Serial.print(sonar.ping()/US_ROUNDTRIP_CM);
  Serial.print(" ");
 /* char ch = Serial.read();
  while ( 255 == (byte) ch)
  {
        ch = Serial.read();
  }
  Serial.print(ch)*/


}
