#include <NewPing.h>

#define SONAR_NUM     3 // Broj senzora
#define MAX_DISTANCE 250 // Maksimalna udaljenost (u cm) koju senzor doseze
#define PING_INTERVAL 33 // Milisekundi izmedju svakog prozivanja senzora.

unsigned long pingTimer[SONAR_NUM]; // Cuva vremena kad bi se prozivanje za svaki senzor trebalo desiti
unsigned int cm[SONAR_NUM];         // Niz pinganih udaljenosti
uint8_t currentSensor = 0;          // Prati aktivni senzor

String sides [3] = {"l ", "c ", "r "};
int value = 9999;
String side = "x ";

NewPing sonar[SONAR_NUM] = {     // Niz senzora
  NewPing(4, 5, MAX_DISTANCE), // Svaki senzor ima trig (trigger) pin, echo pin i maksimalnu udaljenost
  NewPing(8, 9, MAX_DISTANCE),
  NewPing(12, 13, MAX_DISTANCE),

};

//===================================VARIJABLE===============================================


void setup() {
  Serial.begin(9600);
  pingTimer[0] = millis() + 75;           // Prvi ping pocinje na 75 ms
  for (uint8_t i = 1; i < SONAR_NUM; i++) // Postavi pocetke za sve senzore
    pingTimer[i] = pingTimer[i - 1] + PING_INTERVAL;
}

void loop() {
  for (uint8_t i = 0; i < SONAR_NUM; i++) { // Prodji kroz sve senzore
    if (millis() >= pingTimer[i]) {         // Da li je ovo senzor koji se treba pingat
      pingTimer[i] += PING_INTERVAL * SONAR_NUM;  // Postavi sljedeci senzor koji treba pingat
      if (i == 0 && currentSensor == SONAR_NUM - 1) sendResults(); // // Jedan ciklus pinganja gotov, pozovi funkciju za slanje najmanjeg
      currentSensor = i;      
      cm[currentSensor] = sonar[currentSensor].ping_median(10) / US_ROUNDTRIP_CM;
    }
  }
  // Other code that *DOESN'T* analyze ping results can go here.
}

void sendResults()
{
  
  for (uint8_t i = 0; i < SONAR_NUM; i++) {
    if(cm[i]!=0 && cm[i]<value)
    {
      value = cm[i];
      side = sides[i];
    }
  }
    if(value <=99 && value >=10)
   {
    Serial.print(side + value+ "x");
    restartValues();
   }
   else if(value <=120 && value >=100)
  {
    Serial.print(side + value);
    restartValues();
  }
   else if(value <=9 && value >=1)
   {
    Serial.print(side + value + "xx");
       restartValues();
   }
  
}

void restartValues()
{
  value = 999;
  side = "x ";
}

