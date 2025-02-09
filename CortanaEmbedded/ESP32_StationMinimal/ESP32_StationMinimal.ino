#include <WiFi.h>
#include <OneWire.h>
#include <DallasTemperature.h>

const char WIFI_SSID[] = "Jonny Jr";
const char WIFI_PASSWORD[] = "ProcioneSpazialeMistico";
const char CORTANA_IP[] = "192.168.178.117";
const int CORTANA_PORT = 5116;

const int led = 33;
const int light_sensor = 25;
const int motion_sensor = 26;
const int temp_sensor = 27;

OneWire oneWire(temp_sensor);
DallasTemperature DS18B20(&oneWire);
WiFiClient client;

int currentMotion = LOW;
int lastMotion = LOW;

unsigned long tcpTime;
const int transmissionTime = 2000;

void setup() {
  analogSetAttenuation(ADC_11db);
  DS18B20.begin();

  pinMode(motion_sensor, INPUT);
  pinMode(led, OUTPUT);

  connectToWiFi();
  connectToCortana();

  client.print("esp32");

  tcpTime = millis();
}

void loop() {
  currentMotion = digitalRead(motion_sensor);
  digitalWrite(led, currentMotion);
  delay(100);
  return;
  if((millis() - tcpTime >= transmissionTime) || (currentMotion == !lastMotion))
  {
    DS18B20.requestTemperatures();
    float temp = DS18B20.getTempCByIndex(0);
    int light = analogRead(light_sensor);
    
    connectToCortana();

    char buff[100];
    snprintf(buff, 100, "{ \"motion\": \"%d\", \"light\": %d, \"temperature\": %f }", currentMotion, light, temp);
    client.print(buff);

    lastMotion = currentMotion;

    tcpTime = millis();
  }
   
  delay(50);
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

