using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;

using UnityEngine;
using KSP.IO;
using System.Runtime.InteropServices;

namespace KSPArduinoBridge
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VesselData
    {
        public byte id;             //1
        public float AP;            //2
        public float PE;            //3
        public float SemiMajorAxis; //4
        public float SemiMinorAxis; //5
        public float VVI;           //6
        public float e;             //7
        public float inc;           //8
        public float G;             //9
        public int TAp;             //10
        public int TPe;             //11
        public float TrueAnomaly;   //12
        public float Density;       //13
        public int period;          //14
        public float RAlt;          //15
        public float Alt;           //16
        public float Vsurf;         //17
        public float Lat;           //18
        public float Lon;           //19
        public float LiquidFuelTot; //20
        public float LiquidFuel;    //21
        public float OxidizerTot;   //22
        public float Oxidizer;      //23
        public float EChargeTot;    //24
        public float ECharge;       //25
        public float MonoPropTot;   //26
        public float MonoProp;      //27
        public float IntakeAirTot;  //28
        public float IntakeAir;     //29
        public float SolidFuelTot;  //30
        public float SolidFuel;     //31
        public float XenonGasTot;   //32
        public float XenonGas;      //33
        public float LiquidFuelTotS;//34
        public float LiquidFuelS;   //35
        public float OxidizerTotS;  //36
        public float OxidizerS;     //37
        public UInt32 MissionTime;  //38
        public float deltaTime;     //39
        public float VOrbit;        //40
        public UInt32 MNTime;       //41
        public float MNDeltaV;      //42
        public float Pitch;         //43
        public float Roll;          //44
        public float Heading;       //45
        public UInt16 ActionGroups; //46  status bit order:SAS, RCS, Light, Gear, Brakes, Abort, Custom01 - 10 
        public byte SOINumber;      //47  SOI Number (decimal format: sun-planet-moon e.g. 130 = kerbin, 131 = mun)
        public byte MaxOverHeat;    //48  Max part overheat (% percent)
        public float MachNumber;    //49
        public float IAS;           //50  Indicated Air Speed
        public byte CurrentStage;   //51  Current stage number
        public byte TotalStage;     //52  TotalNumber of stages
    }

    //main menu is ksp buildings select screen
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class Settings : MonoBehaviour
    {
        public static string ipAddress;
        public static int port;
        public static bool receivedHandshake = false;

        void Awake()
        {
            print("KSPArduinoBridge: Starting up in main menu");
            //read settings from file here

            ipAddress = "localhost";
            port = 5241;
        }

    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KSPArduinoBridge : MonoBehaviour
    {
        public static Socket javaprogram;

        private ScreenMessageStyle KSPIOScreenStyle = ScreenMessageStyle.UPPER_CENTER;

        void Awake()
        {
            Debug.Log("KSPArduinoBridge: Awoken...");
            //if not setup, setup here then begin

            javaprogram = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            javaprogram.Connect(Settings.ipAddress, Settings.port);
            int counter = 100;
            while (!javaprogram.Connected && counter > 0)
            {
                Thread.Sleep(1000);
                counter--;
            }
            if (javaprogram.Connected)
            {
                Begin();
            }

        }

        private void Begin()
        {
            ScreenMessages.PostScreenMessage("Starting serial KSPArduino plugin ", 10f, KSPIOScreenStyle);
            //test event here
            Debug.Log("KSPArduinoBridge: Beginning...");
            //must wait for arduino connection signal from program
            var handShakeInformation = new Dictionary<string, double>();
            handShakeInformation["id"] = 0;
            handShakeInformation["M1"] = 1;
            handShakeInformation["M2"] = 2;
            handShakeInformation["M3"] = 3;
            sendPacketToKSPMiddleMan(ConvertDictionaryToSend(handShakeInformation));

            Debug.Log("KSPArduinoBridge: waiting");
            Thread.Sleep(1000);

            Debug.Log("KSPArduinoBridge: Making new thread for listening");
            //MiddleManListener mml = new MiddleManListener();
            Thread oThread = new Thread(ListenToMiddleMan);

            Debug.Log("KSPArduinoBridge: Starting thread: ");
            oThread.IsBackground = true;
            oThread.Start();
            
            Debug.Log("KSPArduinoBridge: past thread: ");
            Thread.Sleep(5000);
        }

        private static void sendPacketToKSPMiddleMan(string data)
        {
            if (javaprogram.Connected)
            {
                int toSendLen = System.Text.Encoding.ASCII.GetByteCount(data);
                byte[] toSendBytes = System.Text.Encoding.ASCII.GetBytes(data);
                byte[] toSendLenBytes = System.BitConverter.GetBytes(toSendLen);

                javaprogram.Send(toSendLenBytes);
                javaprogram.Send(toSendBytes);
            }
        }

        private static string ConvertDictionaryToSend(Dictionary<string, double> array)
        {
            string result = "";
            foreach (KeyValuePair<string, double> entry in array)
            {
                // do something with entry.Value or entry.Key
                result += entry.Key + "=" + entry.Value + ";";
            }
            return result;
        }

        public static Dictionary<string, double> ConvertToDictionary(string input)
        {
            Dictionary<string, double> result = new Dictionary<string, double>();
            foreach (string value in input.Split(';'))
            {
                result[value.Split('=')[0]] = Convert.ToDouble(value.Split('=')[1]);
            }
            return result;
        }

        public void ListenToMiddleMan()
        {
            Debug.Log("KSPArduinoBridge: thread started");
            Socket javaProgram = KSPArduinoBridge.javaprogram;

            Debug.Log("KSPArduinoBridge: java propgram grabbed");
            while (true)
            {
                if (javaProgram.Available > 0)
                {
                    byte[] rcvLenBytes = new byte[4];
                    javaProgram.Receive(rcvLenBytes);
                    int rcvLen = System.BitConverter.ToInt32(rcvLenBytes, 0);
                    byte[] rcvBytes = new byte[rcvLen];
                    javaProgram.Receive(rcvBytes);
                    String rcv = System.Text.Encoding.ASCII.GetString(rcvBytes);

                    Debug.Log("KSPArduinoBridge: Received input: " + rcv);

                    interpretInput(rcv);

                }
            }
        }

        public void interpretInput(string input)
        {
            Dictionary<string, double> interpretedInput = KSPArduinoBridge.ConvertToDictionary(input);

            if (interpretedInput.ContainsKey("id"))
            {
                if (interpretedInput["id"] == 0) //handshake
                {
                    if (interpretedInput["M1"] == 1 && interpretedInput["M2"] == 2 && interpretedInput["M3"] == 3)
                    {
                        Settings.receivedHandshake = true;
                        Debug.Log("KSPArduinoBridge: Handshake Received");
                    }
                }
                else if (interpretedInput["id"] == 1) //controls?
                {
                    proccessControls(interpretedInput);
                }
                else if (interpretedInput["id"] == 2) //other
                {

                }
            }
        }

        private void proccessControls(Dictionary<string, double> interpretedInput)
        {
            //check controls here
        }
    }
}
