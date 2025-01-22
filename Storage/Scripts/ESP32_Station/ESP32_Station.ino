#include <WiFi.h>
#include <DHT.h>

const char WIFI_SSID[] = "Home&Life SuperWiFi-3451";
const char WIFI_PASSWORD[] = "";
const char CORTANA_IP[] = "192.168.1.117";
const int CORTANA_PORT = 5000;

const int led_blue = 13;
const int led_white = 14;
const int motion_big = 26;
const int motion_small = 14;
const int light_sensor = 19;
const int dht_sensor = 21;

DHT dht11(dht_sensor, DHT11);
WiFiClient client;

int currentMotionBig = LOW;
int currentMotionSmall = LOW;
int avgMotionBig = LOW;
int avgMotionSmall = LOW;
int lastMotionBig = LOW;
int lastMotionSmall = LOW;

unsigned long tcpTime;
const int transmissionTime = 1750;

void setup() {
  Serial.begin(9600);

  analogSetAttenuation(ADC_11db);
  dht11.begin();

  pinMode(motion_big, INPUT);
  pinMode(motion_small, INPUT);
  pinMode(led_blue, OUTPUT);
  pinMode(led_white, OUTPUT);

  Serial.print("Connecting to Wifi...");
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  while(WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.print("\nConnected to WiFi network with IP Address: ");
  Serial.println(WiFi.localIP());

  delay(500);
 
  Serial.print("Connecting to Cortana...");
  while (!client.connect(CORTANA_IP, CORTANA_PORT)) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("Connected to Cortana");
  client.println("esp32");

  tcpTime = millis();
}

void loop() {
  currentMotionBig = digitalRead(motion_big);
  currentMotionSmall = digitalRead(motion_small);

  if(currentMotionBig == HIGH) avgMotionBig = HIGH;
  if(currentMotionSmall == HIGH) avgMotionSmall = HIGH;

  if((millis() - tcpTime >= transmissionTime) || (avgMotionBig == HIGH && lastMotionBig == LOW) || (avgMotionSmall == HIGH && lastMotionSmall == LOW))
  {
    float hum  = dht11.readHumidity();
    float temp = dht11.readTemperature();
    int light = analogRead(light_sensor);

    char buff[100];
    snprintf(buff, 100, "{ \"bigMotion\": %s, \"smallMotion\": %s, \"light\": %d, \"temp\": %d, \"hum\": %d }", avgMotionBig, avgMotionSmall, light, temp, hum);
    client.println(buff);

    lastMotionBig = avgMotionBig;
    lastMotionSmall = avgMotionSmall;
    avgMotionBig = LOW;
    avgMotionSmall = LOW;

    tcpTime = millis();
  }

  digitalWrite(led_blue, currentMotionBig);
  digitalWrite(led_white, currentMotionSmall);
  delay(50);
}