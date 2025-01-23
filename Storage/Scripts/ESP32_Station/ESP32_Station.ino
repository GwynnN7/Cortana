#include <WiFi.h>
#include <OneWire.h>
#include <DallasTemperature.h>

const char WIFI_SSID[] = "Home&Life SuperWiFi-3451";
const char WIFI_PASSWORD[] = "3YRC8T4GB3X4A4XA";
const char CORTANA_IP[] = "192.168.1.117";
const int CORTANA_PORT = 5000;

const int led_blue = 32;
const int led_white = 33;
const int motion_big = 13;
const int motion_small = 26;
const int light_sensor = 35;
const int temp_sensor = 27;

OneWire oneWire(temp_sensor);
DallasTemperature DS18B20(&oneWire);
WiFiClient client;

int currentMotionBig = LOW;
int currentMotionSmall = LOW;
int lastMotionBig = LOW;
int lastMotionSmall = LOW;

unsigned long tcpTime;
const int transmissionTime = 2000;

void setup() {
  Serial.begin(9600);

  analogSetAttenuation(ADC_11db);
  DS18B20.begin();

  pinMode(motion_big, INPUT_PULLUP);
  pinMode(motion_small, INPUT_PULLUP);
  pinMode(led_blue, OUTPUT);
  pinMode(led_white, OUTPUT);


  Serial.print("Connecting to Wifi...");
  connectToWiFi();
  Serial.print("\nConnected to WiFi network with IP Address: ");
  Serial.println(WiFi.localIP());
  
  Serial.print("Connecting to Cortana...");
  connectToCortana();
  Serial.println("Connected to Cortana");
  client.print("esp32");

  tcpTime = millis();
}

void loop() {
  currentMotionBig = digitalRead(motion_big);
  currentMotionSmall = digitalRead(motion_small);

  if((millis() - tcpTime >= transmissionTime) || (currentMotionBig == !lastMotionBig) || (currentMotionSmall == !lastMotionSmall))
  {
    DS18B20.requestTemperatures();
    float temp = DS18B20.getTempCByIndex(0);
    int light = analogRead(light_sensor);
    
    connectToCortana();

    char buff[100];
    snprintf(buff, 100, "{ \"bigMotion\": \"%s\", \"smallMotion\": \"%s\", \"light\": %d, \"temperature\": %f }", currentMotionBig == 1 ? "On" : "Off", currentMotionSmall == 1 ? "On" : "Off", light, temp);
    client.print(buff);

    lastMotionBig = currentMotionBig;
    lastMotionSmall = currentMotionSmall;

    tcpTime = millis();
  }

  digitalWrite(led_blue, currentMotionBig);
  digitalWrite(led_white, currentMotionSmall);
   
  delay(50);
}

void connectToWiFi()
{
  int tryCount = 0;
  while (WiFi.status() != WL_CONNECTED)
  {
    if (tryCount == 10)
    {
      WiFi.disconnect();
      ESP.restart();
    }
    
    if(client.connected()) client.stop();
    WiFi.disconnect();
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
    WiFi.setSleep(false);
    tryCount++;
    delay(1500);
  }

  delay(500);
}

void connectToCortana()
{ 
  if(!client.connected())
  {
    while (!client.connect(CORTANA_IP, CORTANA_PORT)) {
      delay(1500);
      connectToWiFi();
    }
    client.print("esp32");
  }
}